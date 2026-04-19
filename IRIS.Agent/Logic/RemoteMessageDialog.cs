using System.Drawing;
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
            using var form = new MessageForm(title, message);

            form.FormClosed += (_, _) =>
            {
                tcs.TrySetResult(true);
                Application.ExitThread();
            };

            Application.Run(form);
        }

        private sealed class MessageForm : AgentDialogBase
        {
            public MessageForm(string title, string message)
            {
                Text = title;
                Width = 480;
                Height = 260;

                var okButton = CreateStyledButton("OK", isPrimary: true);

                var iconLabel = CreateIconLabel("\u2139", AccentRed);
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

                // Leave a gap above the OK button so the label never overlaps it.
                var messageTop = 60;
                var messageBottomPadding = okButton.Height + 32;
                var messageLabel = CreateStyledLabel(message, 11F);
                messageLabel.Left = 24;
                messageLabel.Top = messageTop;
                messageLabel.Width = ClientSize.Width - 48;
                messageLabel.Height = Math.Max(40, ClientSize.Height - messageTop - messageBottomPadding);
                messageLabel.TextAlign = ContentAlignment.TopLeft;

                okButton.Left = ClientSize.Width - okButton.Width - 16;
                okButton.Top = ClientSize.Height - okButton.Height - 16;
                okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                okButton.DialogResult = DialogResult.OK;
                okButton.Click += (_, _) => Close();

                AcceptButton = okButton;
                Controls.Add(iconLabel);
                Controls.Add(titleLabel);
                Controls.Add(messageLabel);
                Controls.Add(okButton);
            }
        }
    }
}
