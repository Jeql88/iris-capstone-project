using System.Windows.Forms;
using Serilog;

namespace IRIS.Agent.Logic
{
    internal static class RemoteMessageDialog
    {
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
            using var form = new Form
            {
                Text = title,
                Width = 500,
                Height = 260,
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
                Width = 452,
                Height = 170,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var okButton = new Button
            {
                Text = "OK",
                Width = 90,
                Height = 30,
                Left = form.ClientSize.Width - 106,
                Top = form.ClientSize.Height - 46,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.OK
            };

            okButton.Click += (_, _) => form.Close();

            form.FormClosed += (_, _) =>
            {
                tcs.TrySetResult(true);
                Application.ExitThread();
            };

            form.AcceptButton = okButton;
            form.Controls.Add(messageLabel);
            form.Controls.Add(okButton);
            Application.Run(form);
        }
    }
}
