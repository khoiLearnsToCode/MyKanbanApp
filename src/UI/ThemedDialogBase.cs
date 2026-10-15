using System.Drawing;
using System.Windows.Forms;
using KanbanDashboard.Models;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    public abstract class ThemedDialogBase : Form
    {
        protected readonly Color _bg;
        protected readonly Color _panel;
        protected readonly Color _text;
        protected readonly Color _accent;
        protected readonly Color _inputBg;

        private Point _dragStart;
        private bool  _isDragging;

        protected ThemedDialogBase(AppTheme theme)
        {
            if (theme != null)
            {
                _bg      = ColorHelper.ParseHexOrDefault(theme.BackgroundHex,  Color.FromArgb(35, 38, 52));
                _panel   = ColorHelper.ParseHexOrDefault(theme.PanelHex,       Color.FromArgb(48, 52, 70));
                _text    = ColorHelper.ParseHexOrDefault(theme.TextHex,         Color.FromArgb(198, 208, 245));
                _accent  = ColorHelper.ParseHexOrDefault(theme.ChillColumnHex,  Color.FromArgb(140, 170, 238));
                _inputBg = ColorHelper.Deepen(_panel, 0.18f);
            }
            else
            {
                _bg      = SystemColors.Control;
                _panel   = SystemColors.ControlLight;
                _text    = Color.Black;
                _accent  = Color.SteelBlue;
                _inputBg = SystemColors.Window;
            }

            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.CenterParent;
            MinimizeBox     = false;
            MaximizeBox     = false;
            Font            = new Font("Segoe UI", 9.5f);
            BackColor       = _bg;
            ForeColor       = _text;
        }

        protected void MakeDraggable(Control c)
        {
            c.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                _isDragging = true;
                _dragStart  = e.Location;
            };
            c.MouseMove += (_, e) =>
            {
                if (!_isDragging) return;
                Location = new Point(Location.X + e.X - _dragStart.X,
                                     Location.Y + e.Y - _dragStart.Y);
            };
            c.MouseUp += (_, _) => _isDragging = false;
        }

        protected Button CreateStyledButton(
            string text, Point location, int width, bool accent = false)
        {
            var btn = new Button
            {
                Text      = text,
                Location  = location,
                Width     = width,
                Height    = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = accent ? _accent : _panel,
                ForeColor = accent ? _bg     : _text,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9f,
                                accent ? FontStyle.Bold : FontStyle.Regular)
            };
            btn.FlatAppearance.BorderSize        = 0;
            btn.FlatAppearance.MouseOverBackColor = accent
                ? ColorHelper.Soften(_accent, 0.2f)
                : ColorHelper.Soften(_panel,  0.1f);
            return btn;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(ColorHelper.Soften(_panel, 0.3f), 1.5f);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
