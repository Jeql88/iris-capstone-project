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
                ClientSize = new Size(464, 221);

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
            }
        }
    }
}
