using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    /// <summary>
    /// Fully theme-painted drop-down list (the native ComboBox always draws a white frame and
    /// arrow button). Each item shows a coloured dot. The popup is created on first open.
    /// </summary>
    public class ThemedDropDown : Control
    {
        private const int ItemHeight = 28;
        private const int Radius     = 6;
        private const int DotSize    = 8;

        private static readonly Font ItemFont = new("Segoe UI", 9.5f);

        private readonly string[] _items;
        private readonly Color[]  _dotColors;
        private int  _selectedIndex;
        private bool _hover;

        private ToolStripDropDown _popup;
        private ListPanel         _list;

        public event EventHandler SelectedIndexChanged;

        /// <summary>Background of the box and the list (BackColor is the surrounding surface).</summary>
        public Color FieldColor { get; set; } = SystemColors.Window;

        public ThemedDropDown(string[] items, Color[] dotColors)
        {
            _items     = items;
            _dotColors = dotColors;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
            Height  = 30;
            Cursor  = Cursors.Hand;
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                value = Math.Clamp(value, 0, _items.Length - 1);
                if (value == _selectedIndex) return;
                _selectedIndex = value;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private Color BorderColor =>
            ColorHelper.Blend(FieldColor, ForeColor, Focused || _hover || (_popup?.Visible ?? false) ? 0.45f : 0.22f);

        // ---- closed box ----------------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var box = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path  = ColorHelper.RoundedRectPath(box, Radius))
            using (var fill  = new SolidBrush(FieldColor))
            using (var pen   = new Pen(BorderColor))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            DrawItem(g, _selectedIndex, new Rectangle(0, 0, Width - 28, Height), ForeColor);

            // Chevron
            float cx = Width - 16, cy = Height / 2f + 1;
            bool open = _popup?.Visible ?? false;
            float dir = open ? -1 : 1;
            using var chevron = new Pen(ColorHelper.Blend(FieldColor, ForeColor, 0.7f), 1.6f)
            {
                StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
            };
            g.DrawLines(chevron, new[]
            {
                new PointF(cx - 4, cy - 2 * dir), new PointF(cx, cy + 2 * dir), new PointF(cx + 4, cy - 2 * dir)
            });
        }

        private void DrawItem(Graphics g, int index, Rectangle bounds, Color textColor)
        {
            var dot = new Rectangle(bounds.X + 10, bounds.Y + (bounds.Height - DotSize) / 2, DotSize, DotSize);
            using (var brush = new SolidBrush(_dotColors[index]))
                g.FillEllipse(brush, dot);

            var text = Rectangle.FromLTRB(dot.Right + 8, bounds.Top, bounds.Right - 4, bounds.Bottom);
            TextRenderer.DrawText(g, _items[index], ItemFont, text, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        // ---- input ---------------------------------------------------------------------------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            if (_popup?.Visible ?? false) _popup.Close();
            else OpenPopup();
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData is Keys.Up or Keys.Down || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Down when e.Alt:
                case Keys.F4:
                case Keys.Space:
                    OpenPopup(); break;
                case Keys.Up:   SelectedIndex--; break;
                case Keys.Down: SelectedIndex++; break;
                default: return;
            }
            e.Handled = true;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true;  Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e)   { base.OnGotFocus(e);  Invalidate(); }
        protected override void OnLostFocus(EventArgs e)  { base.OnLostFocus(e); Invalidate(); }

        // ---- popup ---------------------------------------------------------------------------

        private void OpenPopup()
        {
            if (_popup == null)
            {
                _list = new ListPanel(this)
                {
                    Size = new Size(Width, _items.Length * ItemHeight + 8)
                };
                _popup = new ToolStripDropDown
                {
                    Padding         = Padding.Empty,
                    Margin          = Padding.Empty,
                    AutoSize        = false,
                    DropShadowEnabled = true,
                    Size            = _list.Size
                };
                _popup.Items.Add(new ToolStripControlHost(_list)
                {
                    Padding = Padding.Empty, Margin = Padding.Empty, AutoSize = false, Size = _list.Size
                });
                _popup.Closed += (_, _) => Invalidate();
            }

            _list.HotIndex = _selectedIndex;
            _popup.Show(this, new Point(0, Height + 2));
            _list.Focus();
            Invalidate();
        }

        private void Pick(int index)
        {
            SelectedIndex = index;
            _popup.Close();
            Focus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _popup?.Dispose();  // also disposes the hosted list
            base.Dispose(disposing);
        }

        private sealed class ListPanel : Control
        {
            private readonly ThemedDropDown _owner;
            private int _hotIndex;

            public ListPanel(ThemedDropDown owner)
            {
                _owner = owner;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
                Cursor = Cursors.Hand;
            }

            public int HotIndex
            {
                get => _hotIndex;
                set { _hotIndex = Math.Clamp(value, 0, _owner._items.Length - 1); Invalidate(); }
            }

            private Rectangle ItemBounds(int i) => new(4, 4 + i * ItemHeight, Width - 8, ItemHeight);

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                var field = _owner.FieldColor;
                g.Clear(field);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var pen = new Pen(ColorHelper.Blend(field, _owner.ForeColor, 0.30f)))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

                for (int i = 0; i < _owner._items.Length; i++)
                {
                    var bounds = ItemBounds(i);
                    if (i == _hotIndex)
                    {
                        var hot = ColorHelper.Blend(field, _owner._dotColors[i], 0.28f);
                        using var path  = ColorHelper.RoundedRectPath(bounds, 5);
                        using var brush = new SolidBrush(hot);
                        g.FillPath(brush, path);
                    }
                    _owner.DrawItem(g, i, bounds, _owner.ForeColor);
                }
            }

            private int IndexAt(int y) => Math.Clamp((y - 4) / ItemHeight, 0, _owner._items.Length - 1);

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int i = IndexAt(e.Y);
                if (i != _hotIndex) HotIndex = i;
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (e.Button == MouseButtons.Left) _owner.Pick(IndexAt(e.Y));
            }

            protected override bool IsInputKey(Keys keyData) =>
                keyData is Keys.Up or Keys.Down or Keys.Enter or Keys.Escape || base.IsInputKey(keyData);

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                switch (e.KeyCode)
                {
                    case Keys.Up:     HotIndex--; break;
                    case Keys.Down:   HotIndex++; break;
                    case Keys.Enter:
                    case Keys.Space:  _owner.Pick(_hotIndex); break;
                    case Keys.Escape: _owner._popup.Close(); _owner.Focus(); break;
                    default: return;
                }
                e.Handled = true;
            }
        }
    }
}
