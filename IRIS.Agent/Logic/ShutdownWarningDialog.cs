using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Serilog;

namespace IRIS.Agent.Logic
{
    internal static class ShutdownWarningDialog
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        public static Task<bool> ShowCancelOnlyWarningAsync(string title, string baseMessage, int timeoutMs)
        {
            Log.Information(
                "ShutdownWarningDialog requested: Title={Title}, ProcSessionId={ProcSessionId}, WTSActiveSession={WtsSession}, UserInteractive={Interactive}",
                title,
                Process.GetCurrentProcess().SessionId,
                WTSGetActiveConsoleSessionId(),
                Environment.UserInteractive);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var uiThread = new Thread(() =>
            {
                try
                {
                    ShowDialogOnThread(tcs, title, baseMessage, timeoutMs);
                }
                catch (Exception ex)
                {
                    // No interactive desktop session (e.g., running as SYSTEM via Task Scheduler).
                    // Treat as cancelled — we refuse to shut down a machine without warning the user.
                    Log.Warning("Cannot show shutdown warning dialog (no desktop session): {Message}. Cancelling operation.", ex.Message);
                    tcs.TrySetResult(true);
                }
            });

            uiThread.IsBackground = true;
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();

            return tcs.Task;
        }

        private static void ShowDialogOnThread(TaskCompletionSource<bool> tcs, string title, string baseMessage, int timeoutMs)
        {
            var wasCancelled = false;
            var secondsRemaining = timeoutMs / 1000;
            var messageTemplate = ExtractMessageTemplate(baseMessage);

            using var form = new WarningForm(title, FormatMessage(messageTemplate, secondsRemaining));

            var cancelButton = AgentDialogBase.CreateStyledButton("Cancel", isPrimary: false);
            cancelButton.Left = form.ClientSize.Width - cancelButton.Width - 16;
            cancelButton.Top = form.ClientSize.Height - cancelButton.Height - 16;
            cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancelButton.DialogResult = DialogResult.Cancel;

            using var countdownTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };

            using var closeTimer = new System.Windows.Forms.Timer
            {
                Interval = timeoutMs,
                Enabled = false
            };

            cancelButton.Click += (_, _) =>
            {
                wasCancelled = true;
                form.Close();
            };

            countdownTimer.Tick += (_, _) =>
            {
                secondsRemaining--;
                if (secondsRemaining > 0)
                {
                    form.UpdateMessage(FormatMessage(messageTemplate, secondsRemaining));
                    form.UpdateCountdown(secondsRemaining);
                }
                else
                {
                    countdownTimer.Stop();
                    form.Close();
                }
            };

            closeTimer.Tick += (_, _) =>
            {
                closeTimer.Stop();
                form.Close();
            };

            form.FormClosed += (_, _) =>
            {
                countdownTimer.Stop();
                closeTimer.Stop();
                tcs.TrySetResult(wasCancelled);
                Application.ExitThread();
            };

            form.Controls.Add(cancelButton);

            form.Shown += (_, _) =>
            {
                form.Activate();
                form.BringToFront();
                SetForegroundWindow(form.Handle);
                Log.Information("ShutdownWarningDialog form shown for title {Title}", title);
            };

            countdownTimer.Start();
            closeTimer.Start();
            Application.Run(form);
        }

        private static string ExtractMessageTemplate(string message)
        {
            var isRestart = message.Contains("restart", StringComparison.OrdinalIgnoreCase);
            var isIdlePolicy = message.Contains("idle time policy", StringComparison.OrdinalIgnoreCase);

            if (isRestart)
                return isIdlePolicy ? "restart_idle" : "restart_remote";
            return isIdlePolicy ? "shutdown_idle" : "shutdown_remote";
        }

        private static string FormatMessage(string template, int seconds)
        {
            return template switch
            {
                "shutdown_idle" => $"This PC will shut down due to idle time policy in {seconds} seconds.\n\nClick Cancel to prevent shutdown.",
                "shutdown_remote" => $"This PC will shut down due to a remote command in {seconds} seconds.\n\nClick Cancel to prevent shutdown.",
                "restart_idle" => $"This PC will restart due to idle time policy in {seconds} seconds.\n\nClick Cancel to prevent restart.",
                "restart_remote" => $"This PC will restart due to a remote command in {seconds} seconds.\n\nClick Cancel to prevent restart.",
                _ => $"This PC will shut down in {seconds} seconds.\n\nClick Cancel to prevent shutdown."
            };
        }

        private sealed class WarningForm : AgentDialogBase
        {
            private const int WS_EX_TOPMOST = 0x00000008;
            private const int WS_EX_TOOLWINDOW = 0x00000080;

            private readonly Label _messageLabel;
            private readonly Label _countdownLabel;

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW;
                    return cp;
                }
            }

            public WarningForm(string title, string message)
            {
                Text = title;
                Width = 480;
                Height = 260;

                var iconLabel = CreateIconLabel("\u26A0", AccentRed);
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

                _countdownLabel = new Label
                {
                    Text = "",
                    Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                    ForeColor = AccentRed,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleRight,
                    Left = ClientSize.Width - 100,
                    Top = 15,
                    Width = 70,
                    Height = 40,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };

                _messageLabel = CreateStyledLabel(message, 11F);
                _messageLabel.Left = 24;
                _messageLabel.Top = 60;
                _messageLabel.Width = ClientSize.Width - 48;
                _messageLabel.Height = 120;
                _messageLabel.TextAlign = ContentAlignment.TopLeft;

                Controls.Add(iconLabel);
                Controls.Add(titleLabel);
                Controls.Add(_countdownLabel);
                Controls.Add(_messageLabel);
            }

            public void UpdateMessage(string message)
            {
                if (IsDisposed) return;
                _messageLabel.Text = message;
            }

            public void UpdateCountdown(int seconds)
            {
                if (IsDisposed) return;
                _countdownLabel.Text = $"{seconds}s";
            }
        }
    }
}
