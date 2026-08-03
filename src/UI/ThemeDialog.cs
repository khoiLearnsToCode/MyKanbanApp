using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KanbanDashboard.Models;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    /// <summary>
    /// Lets the user type in hex color codes for the app background and each
    /// column/card element. Defaults to (and can be reset back to) Catppuccin Frappe.
    /// Fully theme-aware: colors are applied from the current AppTheme so it
    /// matches the main window instead of looking like a plain system dialog.
    /// </summary>
    public class ThemeDialog : Form
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

        // Colors derived from the active theme
        private readonly Color _bg;
        private readonly Color _panel;
        private readonly Color _text;
        private readonly Color _accent;
        private readonly Color _inputBg;

        public ThemeDialog(AppTheme currentTheme)
        {
            ResultTheme = currentTheme.Clone();

            // Resolve palette from the live theme
            _bg      = ColorHelper.ParseHexOrDefault(currentTheme.BackgroundHex, Color.FromArgb(35, 38, 52));
            _panel   = ColorHelper.ParseHexOrDefault(currentTheme.PanelHex,      Color.FromArgb(48, 52, 70));
            _text    = ColorHelper.ParseHexOrDefault(currentTheme.TextHex,        Color.FromArgb(198, 208, 245));
            _accent  = ColorHelper.ParseHexOrDefault(currentTheme.ChillColumnHex, Color.FromArgb(140, 170, 238));
            _inputBg = ColorHelper.Deepen(_panel, 0.18f);

            Text              = "Theme Settings";
            FormBorderStyle   = FormBorderStyle.None;        // frameless – we draw our own header
            StartPosition     = FormStartPosition.CenterParent;
            MinimizeBox       = false;
            MaximizeBox       = false;
            Font              = new Font("Segoe UI", 9.5f);
            BackColor         = _bg;
            ForeColor         = _text;

            const int rowH    = 36;
            const int leftW   = 200;
            const int boxW    = 110;
            const int swW     = 28;
            const int padX    = 20;
            const int headerH = 48;
            const int bodyPad = 16;   // top padding inside body
            const int btnRowH = 40;   // preset buttons row height
            const int okRowH  = 42;   // OK/Cancel row height
            const int bottomP = 20;   // bottom padding
            int totalH = headerH + bodyPad + _fields.Length * rowH + btnRowH + okRowH + bottomP;
            ClientSize = new Size(420, totalH);

            // ── Draggable header bar ────────────────────────────────────────────
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
            closeBtn.Click    += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            closeBtn.MouseEnter += (_, _) => closeBtn.ForeColor = Color.Tomato;
            closeBtn.MouseLeave += (_, _) => closeBtn.ForeColor = _text;
            header.Controls.Add(headerLabel);
            header.Controls.Add(closeBtn);

            // Make header draggable
            MakeDraggable(header);
            MakeDraggable(headerLabel);

            // ── Scroll body ────────────────────────────────────────────────────
            var body = new Panel
            {
                Location   = new Point(0, headerH),
                Width      = ClientSize.Width,
                Height     = ClientSize.Height - headerH,
                BackColor  = _bg,
                AutoScroll = false   // tall enough to show everything without scrolling
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
                    Location  = new Point(padX + leftW, y + 4),
                    Width     = boxW,
                    Text      = _fields[i].Get(ResultTheme),
                    BackColor = _inputBg,
                    ForeColor = _text,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font      = new Font("Courier New", 9.5f)
                };

                var swatch = new Panel
                {
                    Location    = new Point(padX + leftW + boxW + 8, y + 4),
                    Width       = swW,
                    Height      = 22,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor   = ColorHelper.ParseHexOrDefault(box.Text, Color.Gray)
                };

                int idx = i;
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

            // ── Preset buttons ─────────────────────────────────────────────────
            int btnY = yOff + _fields.Length * rowH + 10;

            var frappeBtn = CreateStyledButton("Frappe (dark)", new Point(padX, btnY), 130);
            frappeBtn.Click += (_, _) => ApplyThemeToFields(AppTheme.CatppuccinFrappe());

            var daybreakBtn = CreateStyledButton("Daybreak (light)", new Point(padX + 142, btnY), 130);
            daybreakBtn.Click += (_, _) => ApplyThemeToFields(AppTheme.SoftDaybreak());

            // ── OK / Cancel ────────────────────────────────────────────────────
            int okY = btnY + 42;

            var okButton = CreateStyledButton("Apply", new Point(ClientSize.Width - 200, okY), 84, accent: true);
            okButton.DialogResult = DialogResult.OK;
            okButton.Click += (_, e) =>
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
            Controls.Add(header);  // on top so it paints over body's top edge
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private Button CreateStyledButton(string text, Point loc, int width, bool accent = false)
        {
            var btn = new Button
            {
                Text      = text,
                Location  = loc,
                Width     = width,
                Height    = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = accent ? _accent : _panel,
                ForeColor = accent ? _bg : _text,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9f, accent ? FontStyle.Bold : FontStyle.Regular)
            };
            btn.FlatAppearance.BorderSize  = 0;
            btn.FlatAppearance.MouseOverBackColor  = accent
                ? ColorHelper.Soften(_accent, 0.2f)
                : ColorHelper.Soften(_panel, 0.1f);
            return btn;
        }

        private void ApplyThemeToFields(AppTheme theme)
        {
            for (int i = 0; i < _fields.Length; i++)
            {
                var hex = _fields[i].Get(theme);
                _hexBoxes[i].Text    = hex;
                _swatches[i].BackColor = ColorHelper.ParseHexOrDefault(hex, Color.Gray);
            }
        }

        // ── Frameless dragging ─────────────────────────────────────────────────
        private Point _dragStart;
        private bool  _dragging;

        private void MakeDraggable(Control c)
        {
            c.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _dragging = true; _dragStart = e.Location; } };
            c.MouseMove += (_, e) => { if (_dragging) Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y); };
            c.MouseUp   += (_, _) => _dragging = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Thin rounded border
            using var pen = new Pen(ColorHelper.Soften(_panel, 0.3f), 1.5f);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
