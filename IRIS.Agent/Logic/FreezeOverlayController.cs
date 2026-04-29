using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Serilog;

namespace IRIS.Agent.Logic
{
    public sealed class FreezeOverlayController : IDisposable
    {
        public const string DefaultFreezeMessage = "This PC is temporarily frozen by the administrator.";

        private readonly object _stateLock = new();
        private readonly System.Threading.Timer _autoUnfreezeTimer;
        private readonly ManualResetEventSlim _overlayReady = new(false);

        private Thread? _overlayThread;
        private List<FreezeOverlayForm> _overlayForms = new();
        private DateTime? _freezeUntilUtc;
        private string _currentFreezeMessage = DefaultFreezeMessage;
        private bool _isFrozen;
        private bool _disposed;

        public FreezeOverlayController()
        {
            _autoUnfreezeTimer = new System.Threading.Timer(_ => CheckAutoUnfreeze(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        public bool IsFrozen
        {
            get
            {
                lock (_stateLock)
                {
                    return _isFrozen;
                }
            }
        }

        public void Freeze(int autoUnfreezeMinutes, string? freezeMessage = null)
        {
            var timeoutMinutes = Math.Clamp(autoUnfreezeMinutes, 1, 120);
            var message = string.IsNullOrWhiteSpace(freezeMessage)
                ? DefaultFreezeMessage
                : freezeMessage.Trim();
            List<FreezeOverlayForm>? formsToUpdate = null;

            lock (_stateLock)
            {
                ThrowIfDisposed();
                _freezeUntilUtc = DateTime.UtcNow.AddMinutes(timeoutMinutes);
                _currentFreezeMessage = message;
                if (_isFrozen)
                {
                    formsToUpdate = _overlayForms.ToList();
                    Log.Information("Freeze already active; extended auto-unfreeze to {FreezeUntilUtc}", _freezeUntilUtc);
                }
            }

            if (formsToUpdate != null)
            {
                foreach (var form in formsToUpdate)
                {
                    form.SetMessage(message);
                }
                return;
            }

            StartOverlayThread();
            Log.Warning("Freeze overlay enabled. Auto-unfreeze in {TimeoutMinutes} minute(s)", timeoutMinutes);
        }

        public void Unfreeze()
        {
            List<FreezeOverlayForm> formsSnapshot;

            lock (_stateLock)
            {
                if (!_isFrozen)
                {
                    _freezeUntilUtc = null;
                    return;
                }

                _freezeUntilUtc = null;
                formsSnapshot = _overlayForms.ToList();
            }

            if (formsSnapshot.Count == 0)
            {
                lock (_stateLock)
                {
                    _isFrozen = false;
                }
                return;
            }

            try
            {
                var primary = formsSnapshot[0];
                if (primary.IsHandleCreated)
                {
                    primary.BeginInvoke(new Action(() =>
                    {
                        foreach (var form in formsSnapshot)
                        {
                            try
                            {
                                form.AllowCloseFlag = true;
                                form.Close();
                            }
                            catch
                            {
                                // Ignore form close failures
                            }
                        }

                        Application.ExitThread();
                    }));
                }
            }
            catch
            {
                // Ignore UI thread teardown issues
            }

            lock (_stateLock)
            {
                _isFrozen = false;
                _overlayForms.Clear();
            }

            Log.Information("Freeze overlay disabled");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Unfreeze();
            _autoUnfreezeTimer.Dispose();
            _overlayReady.Dispose();
        }

        private void StartOverlayThread()
        {
            _overlayReady.Reset();
            _overlayThread = new Thread(OverlayThreadMain)
            {
                IsBackground = true,
                Name = "IRIS-FreezeOverlay"
            };
            _overlayThread.SetApartmentState(ApartmentState.STA);
            _overlayThread.Start();

            if (!_overlayReady.Wait(TimeSpan.FromSeconds(2)))
            {
                Log.Warning("Freeze overlay thread did not signal ready state in time");
            }
        }

        private void OverlayThreadMain()
        {
            try
            {
                var forms = Screen.AllScreens
                    .Select(screen => new FreezeOverlayForm(screen.Bounds, _currentFreezeMessage))
                    .ToList();

                lock (_stateLock)
                {
                    _overlayForms = forms;
                    _isFrozen = true;
                }

                _overlayReady.Set();

                foreach (var form in forms)
                {
                    form.Show();
                }

                Application.Run();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Freeze overlay thread failed");
            }
            finally
            {
                lock (_stateLock)
                {
                    _isFrozen = false;
                    _overlayForms.Clear();
                }

                _overlayReady.Set();
            }
        }

        private void CheckAutoUnfreeze()
        {
            DateTime? freezeUntil;
            bool isFrozen;

            lock (_stateLock)
            {
                freezeUntil = _freezeUntilUtc;
                isFrozen = _isFrozen;
            }

            if (!isFrozen || !freezeUntil.HasValue)
            {
                return;
            }

            if (DateTime.UtcNow < freezeUntil.Value)
            {
                return;
            }

            Log.Information("Auto-unfreeze timeout reached");
            Unfreeze();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FreezeOverlayController));
            }
        }

        private sealed class FreezeOverlayForm : Form
        {
            [DllImport("user32.dll")]
            private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

            [DllImport("user32.dll")]
            private static extern bool SetForegroundWindow(IntPtr hWnd);

            private static readonly IntPtr HWND_TOPMOST = new(-1);
            private const uint SWP_NOMOVE = 0x0002;
            private const uint SWP_NOSIZE = 0x0001;
            private const uint SWP_NOACTIVATE = 0x0010;

            private const int WS_EX_TOPMOST = 0x00000008;
            private const int WS_EX_TOOLWINDOW = 0x00000080;
            private const int WM_WINDOWPOSCHANGING = 0x0046;

            private readonly Label _messageLabel;
            private readonly System.Windows.Forms.Timer _topMostTimer;
            private int _tickCount;
            internal bool AllowCloseFlag;

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW;
                    return cp;
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct WINDOWPOS
            {
                public IntPtr hwnd;
                public IntPtr hwndInsertAfter;
                public int x;
                public int y;
                public int cx;
                public int cy;
                public uint flags;
            }

            protected override void WndProc(ref Message m)
            {
                // Only clamp z-order when no message dialog is visible. When a
                // message is showing we let Windows place us below it so the
                // message always has the highest priority.
                if (m.Msg == WM_WINDOWPOSCHANGING && m.LParam != IntPtr.Zero
                    && RemoteMessageDialog.ActiveWindowHandle == IntPtr.Zero)
                {
                    var wp = Marshal.PtrToStructure<WINDOWPOS>(m.LParam);
                    wp.hwndInsertAfter = HWND_TOPMOST;
                    Marshal.StructureToPtr(wp, m.LParam, fDeleteOld: false);
                }
                base.WndProc(ref m);
            }

            public FreezeOverlayForm(Rectangle bounds, string message)
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                Bounds = bounds;
                TopMost = true;
                ShowInTaskbar = false;
                BackColor = Color.Black;
                ForeColor = Color.White;

                _messageLabel = new Label
                {
                    Text = message,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 24, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.Black
                };

                Controls.Add(_messageLabel);

                Shown += (_, _) =>
                {
                    SetForegroundWindow(Handle);
                };

                _topMostTimer = new System.Windows.Forms.Timer { Interval = 100 };
                _topMostTimer.Tick += (_, _) =>
                {
                    if (IsDisposed || !IsHandleCreated)
                    {
                        return;
                    }

                    var msgHwnd = RemoteMessageDialog.ActiveWindowHandle;
                    if (msgHwnd != IntPtr.Zero)
                        // Yield to the message dialog: stay topmost but sit just below it.
                        SetWindowPos(Handle, msgHwnd, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    else
                        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

                    // Pull focus back every ~1s (every 10th tick) so we recover from hijacks
                    // without stealing focus so aggressively that it breaks Ctrl+Alt+Del.
                    _tickCount++;
                    if (_tickCount >= 10)
                    {
                        _tickCount = 0;
                        if (msgHwnd == IntPtr.Zero)
                            SetForegroundWindow(Handle);
                    }
                };
                _topMostTimer.Start();
            }

            internal void SetMessage(string message)
            {
                if (IsDisposed)
                {
                    return;
                }

                if (InvokeRequired)
                {
                    BeginInvoke(new Action<string>(SetMessage), message);
                    return;
                }

                _messageLabel.Text = string.IsNullOrWhiteSpace(message)
                    ? DefaultFreezeMessage
                    : message;
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                if (!AllowCloseFlag)
                {
                    e.Cancel = true;
                    return;
                }

                _topMostTimer.Stop();
                _topMostTimer.Dispose();
                base.OnFormClosing(e);
            }
        }
    }
}
