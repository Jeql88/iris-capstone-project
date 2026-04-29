using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Serilog;

namespace IRIS.Agent.Logic
{
    internal static class RemoteMessageDialog
    {
        // Set when a MessageForm is visible so FreezeOverlayForm can yield to it.
        internal static volatile IntPtr ActiveWindowHandle = IntPtr.Zero;

        public static Task ShowInfoAsync(string title, string message)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var uiThread = new Thread(() =>
            {
                try
                {
                    ShowDialogOnThread(tcs, title, message);
                }
                catch (Exception ex)
                {
                    // No interactive desktop session available.
                    Log.Warning("Cannot show remote message dialog (no desktop session): {Message}", ex.Message);
                    tcs.TrySetResult(false);
                }
            });

            uiThread.IsBackground = true;
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();

            return tcs.Task;
        }

        private static void ShowDialogOnThread(TaskCompletionSource<bool> tcs, string title, string message)
        {
            using var form = new MessageForm(title, message);

            form.FormClosed += (_, _) =>
            {
                tcs.TrySetResult(true);
                Application.ExitThread();
            };

            Application.Run(form);
        }

        // The freeze overlay re-asserts itself as TOPMOST on a 100 ms timer
        // (FreezeOverlayController.FreezeOverlayForm), so a one-shot Activate()
        // gets out-raced. This form mirrors the same pattern: WS_EX_TOPMOST in
        // CreateParams, a WndProc clamp on WM_WINDOWPOSCHANGING, and a periodic
        // SetWindowPos to re-assert topmost. AttachThreadInput is used on Shown
        // so SetForegroundWindow actually takes effect even when the foreground
        // belongs to another thread (Windows foreground-lock rule).
        private sealed class MessageForm : AgentDialogBase
        {
            [DllImport("user32.dll")]
            private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

            [DllImport("user32.dll")]
            private static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            private static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("user32.dll")]
            private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

            [DllImport("kernel32.dll")]
            private static extern uint GetCurrentThreadId();

            private static readonly IntPtr HWND_TOPMOST = new(-1);
            private const uint SWP_NOMOVE = 0x0002;
            private const uint SWP_NOSIZE = 0x0001;
            private const uint SWP_NOACTIVATE = 0x0010;

            private const int WS_EX_TOPMOST = 0x00000008;
            private const int WM_WINDOWPOSCHANGING = 0x0046;

            private readonly System.Windows.Forms.Timer _topMostTimer;

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_TOPMOST;
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
                // Clamp Z-order changes so any peer that pushes us down (e.g. the
                // freeze overlay re-applying HWND_TOPMOST to itself) doesn't end
                // up sitting above us.
                if (m.Msg == WM_WINDOWPOSCHANGING && m.LParam != IntPtr.Zero)
                {
                    var wp = Marshal.PtrToStructure<WINDOWPOS>(m.LParam);
                    wp.hwndInsertAfter = HWND_TOPMOST;
                    Marshal.StructureToPtr(wp, m.LParam, fDeleteOld: false);
                }
                base.WndProc(ref m);
            }

            public MessageForm(string title, string message)
            {
                Text = title;
                ClientSize = new Size(464, 221);

                var okButton = CreateStyledButton("OK", isPrimary: true);

                var iconLabel = CreateIconLabel("ℹ", AccentRed);
                iconLabel.Left = 24;
                iconLabel.Top = 18;

                var titleLabel = new Label
                {
                    Text = title,
                    Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                    ForeColor = TextPrimary,
                    BackColor = Color.Transparent,
                    Left = 62,
                    Top = 20,
                    Width = ClientSize.Width - 86,
                    Height = 28,
                    AutoSize = false
                };

                var messageTop = 60;
                var messageLabel = CreateStyledLabel(message, 11F);
                messageLabel.Left = 24;
                messageLabel.Top = messageTop;
                messageLabel.MaximumSize = new Size(ClientSize.Width - 48, 0);
                messageLabel.AutoSize = true;
                messageLabel.TextAlign = ContentAlignment.TopLeft;

                okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                okButton.DialogResult = DialogResult.OK;
                okButton.Click += (_, _) => Close();

                AcceptButton = okButton;
                Controls.Add(iconLabel);
                Controls.Add(titleLabel);
                Controls.Add(messageLabel);
                Controls.Add(okButton);

                // Grow the form if the (possibly long) wrapped message needs more room.
                var neededClientHeight = messageLabel.Bottom + 16 + okButton.Height + 16;
                if (ClientSize.Height < neededClientHeight)
                {
                    ClientSize = new Size(ClientSize.Width, neededClientHeight);
                }

                okButton.Left = ClientSize.Width - okButton.Width - 16;
                okButton.Top = ClientSize.Height - okButton.Height - 16;

                Shown += (_, _) =>
                {
                    RemoteMessageDialog.ActiveWindowHandle = Handle;
                    ForceForeground();
                };

                // Re-assert TOPMOST every 100 ms. Don't grab foreground here —
                // that would steal focus while the user types or hovers other
                // apps. The WndProc clamp + periodic SetWindowPos is enough to
                // win Z-order against the freeze overlay's own re-assertion.
                _topMostTimer = new System.Windows.Forms.Timer { Interval = 100 };
                _topMostTimer.Tick += (_, _) =>
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                };
                _topMostTimer.Start();

                FormClosed += (_, _) =>
                {
                    _topMostTimer.Stop();
                    RemoteMessageDialog.ActiveWindowHandle = IntPtr.Zero;
                };
            }

            private void ForceForeground()
            {
                // SetForegroundWindow is restricted by Windows when the calling
                // thread doesn't own the current foreground window. Attach this
                // thread's input queue to the foreground thread's queue first so
                // the call actually takes effect.
                var foreground = GetForegroundWindow();
                var foregroundThread = GetWindowThreadProcessId(foreground, out _);
                var currentThread = GetCurrentThreadId();

                bool attached = false;
                if (foregroundThread != 0 && foregroundThread != currentThread)
                {
                    attached = AttachThreadInput(currentThread, foregroundThread, true);
                }

                try
                {
                    SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    SetForegroundWindow(Handle);
                    Activate();
                    BringToFront();
                }
                finally
                {
                    if (attached)
                    {
                        AttachThreadInput(currentThread, foregroundThread, false);
                    }
                }
            }
        }
    }
}
