using System.Drawing;
using System.Windows.Forms;

namespace IRIS.Agent.Logic
{
    internal class AgentDialogBase : Form
    {
        protected static readonly Color BackgroundLight = Color.FromArgb(243, 243, 243);
        protected static readonly Color TextPrimary = Color.FromArgb(27, 27, 27);
        protected static readonly Color TextSecondary = Color.FromArgb(96, 96, 96);
        protected static readonly Color AccentRed = Color.FromArgb(196, 43, 28);
        protected static readonly Color AccentRedHover = Color.FromArgb(220, 60, 45);
        protected static readonly Color ButtonSecondaryBg = Color.FromArgb(224, 224, 224);
        protected static readonly Color ButtonHoverSecondary = Color.FromArgb(200, 200, 200);

        protected AgentDialogBase()
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BackgroundLight;
            ForeColor = TextPrimary;
            Font = new Font("Segoe UI", 11F, FontStyle.Regular);
            // Required — without a non-zero baseline, AutoScaleMode.Dpi is a no-op
            // and the form stays at designer pixels on high-DPI monitors, clipping controls.
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var brush = new SolidBrush(AccentRed);
            e.Graphics.FillRectangle(brush, 0, 0, ClientSize.Width, 3);
        }

        internal static Button CreateStyledButton(string text, bool isPrimary)
        {
            var bgColor = isPrimary ? AccentRed : ButtonSecondaryBg;
            var fgColor = isPrimary ? Color.White : TextPrimary;
            var hoverColor = isPrimary ? AccentRedHover : ButtonHoverSecondary;

            var button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = bgColor,
                ForeColor = fgColor,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                Cursor = Cursors.Hand,
                Height = 36,
                MinimumSize = new Size(100, 36),
                Width = 100
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = hoverColor;
            button.FlatAppearance.MouseDownBackColor = hoverColor;

            return button;
        }

        internal static Label CreateStyledLabel(string text, float fontSize = 11F, bool isSecondary = false)
        {
            return new Label
            {
                Text = text,
                ForeColor = isSecondary ? TextSecondary : TextPrimary,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", fontSize, FontStyle.Regular),
                AutoSize = false
            };
        }

        internal static Label CreateIconLabel(string iconText, Color color, float size = 20F)
        {
            return new Label
            {
                Text = iconText,
                Font = new Font("Segoe UI", size, FontStyle.Bold),
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = 32,
                Height = 32,
                TextAlign = ContentAlignment.MiddleCenter
            };
        }
    }
}
