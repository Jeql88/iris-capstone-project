using System.Threading.Tasks;
using System.Windows.Forms;
using Serilog;

namespace IRIS.Agent.Logic
{
    internal static class ShutdownWarningDialog
    {
        public static Task<bool> ShowCancelOnlyWarningAsync(string title, string baseMessage, int timeoutMs)
        {
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
                    // Proceed with shutdown — there is no user to warn.
                    Log.Warning("Cannot show shutdown warning dialog (no desktop session): {Message}", ex.Message);
                    tcs.TrySetResult(false);
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

            using var form = new Form
            {
                Text = title,
                Width = 460,
                Height = 230,
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = true,
                TopMost = true
            };

            var messageLabel = new Label
            {
                Text = FormatMessage(messageTemplate, secondsRemaining),
                Left = 16,
                Top = 16,
                Width = 412,
                Height = 120,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                Width = 90,
                Height = 30,
                Left = form.ClientSize.Width - 106,
                Top = form.ClientSize.Height - 46,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };

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
                    messageLabel.Text = FormatMessage(messageTemplate, secondsRemaining);
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

            form.Controls.Add(messageLabel);
            form.Controls.Add(cancelButton);

            countdownTimer.Start();
            closeTimer.Start();
            Application.Run(form);
        }

        private static string ExtractMessageTemplate(string message)
        {
            // Determine the action and reason from the original message
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
    }
}
