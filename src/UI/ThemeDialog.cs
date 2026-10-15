using System;
using System.Drawing;
using System.Windows.Forms;
using KanbanDashboard.Models;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    public class ThemeDialog : ThemedDialogBase
    {
        public AppTheme ResultTheme { get; private set; }

        private readonly (string Label, Func<AppTheme, string> Get, Action<AppTheme, string> Set)[] _fields =
        {
            ("App background",              t => t.BackgroundHex,        (t, v) => t.BackgroundHex = v),
            ("Panel / header",              t => t.PanelHex,             (t, v) => t.PanelHex = v),
            ("Card background",             t => t.CardBackgroundHex,    (t, v) => t.CardBackgroundHex = v),
            ("Text color",                  t => t.TextHex,              (t, v) => t.TextHex = v),
            ("Urgent column",               t => t.UrgentColumnHex,      (t, v) => t.UrgentColumnHex = v),
            ("Prioritized column",          t => t.PrioritizedColumnHex, (t, v) => t.PrioritizedColumnHex = v),
            ("Chill column",                t => t.ChillColumnHex,       (t, v) => t.ChillColumnHex = v),
            ("Done column",                 t => t.DoneColumnHex,        (t, v) => t.DoneColumnHex = v),
            ("Done – on time (green vibe)", t => t.DoneSuccessHex,       (t, v) => t.DoneSuccessHex = v),
            ("Done – past deadline (red)",  t => t.DoneLateHex,          (t, v) => t.DoneLateHex = v),
        };

        private readonly TextBox[] _hexBoxes;
        private readonly Panel[]   _swatches;

        public ThemeDialog(AppTheme currentTheme)
            : base(currentTheme)
        {
            ResultTheme = currentTheme.Clone();
            Text        = "Theme Settings";

            const int rowH    = 36;
            const int leftW   = 200;
            const int boxW    = 110;
            const int swW     = 28;
            const int padX    = 20;
            const int headerH = 48;
            const int bodyPad = 16;
            const int btnRowH = 40;
            const int okRowH  = 42;
            const int bottomP = 20;
            int totalH = headerH + bodyPad + _fields.Length * rowH + btnRowH + okRowH + bottomP;
            ClientSize = new Size(420, totalH);

            var header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = headerH,
                BackColor = _panel
            };
            var headerLabel = new Label
            {
                Text      = "Theme Settings",
                ForeColor = _text,
                BackColor = _panel,
                Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(16, 0, 0, 0)
            };
            var closeBtn = new Label
            {
                Text      = "X",
                ForeColor = _text,
                BackColor = _panel,
                Font      = new Font("Segoe UI", 11f),
                Width     = 40,
                Dock      = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor    = Cursors.Hand
            };
            closeBtn.Click      += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            closeBtn.MouseEnter += (_, _) => closeBtn.ForeColor = Color.Tomato;
            closeBtn.MouseLeave += (_, _) => closeBtn.ForeColor = _text;
            header.Controls.Add(headerLabel);
            header.Controls.Add(closeBtn);
            MakeDraggable(header);
            MakeDraggable(headerLabel);

            var body = new Panel
            {
                Location   = new Point(0, headerH),
                Width      = ClientSize.Width,
                Height     = ClientSize.Height - headerH,
                BackColor  = _bg,
                AutoScroll = false
            };

            _hexBoxes = new TextBox[_fields.Length];
            _swatches = new Panel[_fields.Length];

            int yOff = bodyPad;
            for (int i = 0; i < _fields.Length; i++)
            {
                int y = yOff + i * rowH;

                var lbl = new Label
                {
                    Text      = _fields[i].Label,
                    Location  = new Point(padX, y + 8),
                    Width     = leftW,
                    AutoSize  = false,
                    ForeColor = _text,
                    BackColor = _bg
                };

                var box = new TextBox
                {
                    Location    = new Point(padX + leftW, y + 4),
                    Width       = boxW,
                    Text        = _fields[i].Get(ResultTheme),
                    BackColor   = _inputBg,
                    ForeColor   = _text,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font        = new Font("Courier New", 9.5f)
                };

                var swatch = new Panel
                {
                    Location    = new Point(padX + leftW + boxW + 8, y + 4),
                    Width       = swW,
                    Height      = 22,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor   = ColorHelper.ParseHexOrDefault(box.Text, Color.Gray)
                };

                box.TextChanged += (_, _) =>
                {
                    if (ColorHelper.TryParseHex(box.Text, out var c))
                    {
                        swatch.BackColor = c;
                        box.ForeColor    = _text;
                    }
                    else
                    {
                        box.ForeColor = Color.Tomato;
                    }
                };

                _hexBoxes[i] = box;
                _swatches[i] = swatch;

                body.Controls.Add(lbl);
                body.Controls.Add(box);
                body.Controls.Add(swatch);
            }

            int btnY = yOff + _fields.Length * rowH + 10;

            var frappeBtn = CreateStyledButton("Frappe (dark)", new Point(padX, btnY), 130);
            frappeBtn.Click += (_, _) => ApplyThemeToFields(AppTheme.CatppuccinFrappe());

            var daybreakBtn = CreateStyledButton("Daybreak (light)", new Point(padX + 142, btnY), 130);
            daybreakBtn.Click += (_, _) => ApplyThemeToFields(AppTheme.SoftDaybreak());

            int okY = btnY + 42;

            var okButton = CreateStyledButton("Apply", new Point(ClientSize.Width - 200, okY), 84, accent: true);
            okButton.DialogResult = DialogResult.OK;
            okButton.Click += (_, _) =>
            {
                for (int i = 0; i < _fields.Length; i++)
                {
                    if (!ColorHelper.TryParseHex(_hexBoxes[i].Text, out Color _))
                    {
                        MessageBox.Show(this,
                            $"\"{_fields[i].Label}\" is not a valid hex color (e.g. #A6D189).",
                            "Khoi's Kanban", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        DialogResult = DialogResult.None;
                        return;
                    }
                    _fields[i].Set(ResultTheme, _hexBoxes[i].Text.Trim());
                }
            };

            var cancelButton = CreateStyledButton("Cancel", new Point(ClientSize.Width - 106, okY), 84);
            cancelButton.DialogResult = DialogResult.Cancel;

            AcceptButton = okButton;
            CancelButton = cancelButton;

            body.Controls.Add(frappeBtn);
            body.Controls.Add(daybreakBtn);
            body.Controls.Add(okButton);
            body.Controls.Add(cancelButton);

            Controls.Add(body);
            Controls.Add(header);
        }

        private void ApplyThemeToFields(AppTheme theme)
        {
            for (int i = 0; i < _fields.Length; i++)
            {
                var hex = _fields[i].Get(theme);
                _hexBoxes[i].Text      = hex;
                _swatches[i].BackColor = ColorHelper.ParseHexOrDefault(hex, Color.Gray);
            }
        }
    }
}
