using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Serilog;

namespace IRIS.Agent.Logic
{
    public sealed class FreezeOverlayController : IDisposable
    {
        private readonly object _stateLock = new();
        private readonly System.Threading.Timer _autoUnfreezeTimer;
        private readonly ManualResetEventSlim _overlayReady = new(false);

        private Thread? _overlayThread;
        private List<FreezeOverlayForm> _overlayForms = new();
        private DateTime? _freezeUntilUtc;
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

        public void Freeze(int autoUnfreezeMinutes)
        {
            var timeoutMinutes = Math.Clamp(autoUnfreezeMinutes, 1, 120);

            lock (_stateLock)
            {
                ThrowIfDisposed();
                _freezeUntilUtc = DateTime.UtcNow.AddMinutes(timeoutMinutes);
                if (_isFrozen)
                {
                    Log.Information("Freeze already active; extended auto-unfreeze to {FreezeUntilUtc}", _freezeUntilUtc);
                    return;
                }
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
                    .Select(screen => new FreezeOverlayForm(screen.Bounds))
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
            internal bool AllowCloseFlag;

            public FreezeOverlayForm(Rectangle bounds)
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                Bounds = bounds;
                TopMost = true;
                ShowInTaskbar = false;
                BackColor = Color.Black;
                ForeColor = Color.White;

                var message = new Label
                {
                    Text = "This PC is temporarily frozen by the administrator.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 24, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.Black
                };

                Controls.Add(message);
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                if (!AllowCloseFlag)
                {
                    e.Cancel = true;
                    return;
                }

                base.OnFormClosing(e);
            }
        }
    }
}
