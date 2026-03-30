using System.Threading.Tasks;
using System.Windows.Forms;
using Serilog;

namespace IRIS.Agent.Logic
{
    internal static class ShutdownWarningDialog
    {
        public static Task<bool> ShowCancelOnlyWarningAsync(string title, string message, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var uiThread = new Thread(() =>
            {
                try
                {
                    ShowDialogOnThread(tcs, title, message, timeoutMs);
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

        private static void ShowDialogOnThread(TaskCompletionSource<bool> tcs, string title, string message, int timeoutMs)
        {
            var wasCancelled = false;

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
                Text = message,
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

            using var closeTimer = new System.Windows.Forms.Timer
            {
                Interval = Math.Max(1000, timeoutMs)
            };

            cancelButton.Click += (_, _) =>
            {
                wasCancelled = true;
                form.Close();
            };

            closeTimer.Tick += (_, _) =>
            {
                closeTimer.Stop();
                form.Close();
            };

            form.FormClosed += (_, _) =>
            {
                closeTimer.Stop();
                tcs.TrySetResult(wasCancelled);
                Application.ExitThread();
            };

            form.Controls.Add(messageLabel);
            form.Controls.Add(cancelButton);

            closeTimer.Start();
            Application.Run(form);
        }
    }
}
