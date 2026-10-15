using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KanbanDashboard.Models;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    public class ColumnPanel : Panel
    {
        public BoardColumn Column { get; }

        public event EventHandler<BoardColumn> QuickAddRequested;

        private readonly DotHeaderPanel  _headerPanel;
        private readonly Panel           _accentUnderline;
        private readonly ThemedScrollPanel _cardsPanel;
        private string _baseTitle;

        public ColumnPanel(BoardColumn column, string title)
        {
            Column     = column;
            _baseTitle = title;
            Dock       = DockStyle.Fill;
            Margin     = new Padding(8);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            _headerPanel = new DotHeaderPanel { Dock = DockStyle.Top, Height = 38, Text = title };
            _headerPanel.QuickAddClicked += (_, _) => QuickAddRequested?.Invoke(this, Column);

            _accentUnderline = new Panel { Dock = DockStyle.Top, Height = 3 };

            _cardsPanel = new ThemedScrollPanel { Dock = DockStyle.Fill };

            Controls.Add(_cardsPanel);
            Controls.Add(_accentUnderline);
            Controls.Add(_headerPanel);
        }

        public void SetAccentColor(Color color)
        {
            _headerPanel.DotColor      = color;
            _accentUnderline.BackColor = color;
        }

        public void SetBackgroundColors(Color panelBackground, Color headerText)
        {
            BackColor                    = panelBackground;
            _cardsPanel.ContentBackColor = panelBackground;
            _headerPanel.BackColor       = panelBackground;
            _headerPanel.TextColor       = headerText;
            _cardsPanel.SetScrollColors(panelBackground, headerText);
        }

        // Batch several SetCards calls (across columns) so layout and scroll are recomputed once.
        public void BeginUpdate() => _cardsPanel.BeginUpdate();
        public void EndUpdate()   => _cardsPanel.EndUpdate();

        /// <summary>
        /// Makes this column show exactly <paramref name="cards"/>, in order. Existing cards are
        /// kept (or moved here from another column) rather than recreated.
        /// </summary>
        public void SetCards(IReadOnlyList<TaskCardControl> cards) => _cardsPanel.SetCards(cards);

        /// <summary>Updates countdowns, progress and overdue styling without recreating cards.</summary>
        public void RefreshCards(AppTheme theme)
        {
            foreach (var card in _cardsPanel.Cards)
                card.RefreshTimeDependent(theme);
        }

        public void UpdateHeaderCount(int count)
        {
            _headerPanel.Text = $"{_baseTitle}  ({count})";
            _headerPanel.Invalidate();
        }

        public void SetTitle(string title)
        {
            _baseTitle        = title;
            _headerPanel.Text = title;
        }

        private class ThemedScrollPanel : Panel
        {
            private readonly FlowLayoutPanel _content;
            private readonly Panel           _track;
            private readonly Panel           _thumb;

            private int  _scrollPx       = 0;
            private bool _dragging        = false;
            private int  _dragAnchorY;
            private int  _dragAnchorScroll;

            // While updating, per-card scroll recalculation is skipped (it is O(n) per
            // add/remove) and the scroll position is restored afterwards.
            private bool _updating;
            private int  _scrollBeforeUpdate;

            private const int BarW     = 5;
            private const int BarRight = 4;
            private const int MinThumb = 22;

            public Color ContentBackColor
            {
                get => _content.BackColor;
                set => _content.BackColor = value;
            }

            public ThemedScrollPanel()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

                _content = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents  = false,
                    AutoScroll    = false,
                    Padding       = new Padding(10, 12, 10, 12),
                };

                _track = new Panel { Visible = false };
                _thumb = new Panel { Cursor  = Cursors.Hand };

                _thumb.MouseDown += (_, e) =>
                {
                    if (e.Button != MouseButtons.Left) return;
                    _dragging        = true;
                    _dragAnchorY      = _thumb.PointToScreen(e.Location).Y;
                    _dragAnchorScroll = _scrollPx;
                };
                _thumb.MouseMove += (_, e) =>
                {
                    if (!_dragging) return;
                    int dy        = _thumb.PointToScreen(e.Location).Y - _dragAnchorY;
                    int trackH    = _track.Height - _thumb.Height;
                    int maxScroll = Math.Max(0, TotalContentHeight() - Height);
                    if (trackH > 0)
                        ApplyScroll(_dragAnchorScroll + (int)((double)dy / trackH * maxScroll));
                };
                _thumb.MouseUp += (_, _) => _dragging = false;

                _track.MouseDown += (_, e) =>
                {
                    if (e.Y < _thumb.Top) ApplyScroll(_scrollPx - Height);
                    else                  ApplyScroll(_scrollPx + Height);
                };

                _track.Controls.Add(_thumb);
                Controls.Add(_content);
                Controls.Add(_track);

                Resize += (_, _) => Relayout();
                // Cards can move between columns, so wheel forwarding follows the parent.
                _content.ControlAdded += (_, e) =>
                {
                    HookMouseWheel(e.Control, hook: true);
                    if (!_updating) RefreshScroll();
                };
                _content.ControlRemoved += (_, e) =>
                {
                    HookMouseWheel(e.Control, hook: false);
                    if (!_updating) RefreshScroll();
                };
            }

            public void SetScrollColors(Color panelBg, Color text)
            {
                _track.BackColor = ColorHelper.Soften(panelBg, 0.10f);
                _thumb.BackColor = ColorHelper.Blend(panelBg, text, 0.40f);
                RefreshThumbRegion();
            }

            public IEnumerable<TaskCardControl> Cards => _content.Controls.OfType<TaskCardControl>();

            public void BeginUpdate()
            {
                _updating           = true;
                _scrollBeforeUpdate = _scrollPx;
                _content.SuspendLayout();
            }

            public void EndUpdate()
            {
                _content.ResumeLayout(true);
                _updating = false;
                RefreshScroll();
                ApplyScroll(_scrollBeforeUpdate);
            }

            public void SetCards(IReadOnlyList<TaskCardControl> cards)
            {
                var wanted = new HashSet<Control>(cards);
                for (int i = _content.Controls.Count - 1; i >= 0; i--)
                {
                    if (!wanted.Contains(_content.Controls[i]))
                        _content.Controls.RemoveAt(i);  // not disposed: it may move to another column
                }

                int width = AvailableCardWidth();
                for (int i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];
                    if (card.Parent != _content)
                    {
                        card.Width = width;
                        _content.Controls.Add(card);  // also detaches it from its old column
                    }
                    if (_content.Controls.GetChildIndex(card) != i)
                        _content.Controls.SetChildIndex(card, i);
                }
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                ApplyScroll(_scrollPx - e.Delta / 120 * 50);
            }

            private void OnForwardedWheel(object sender, MouseEventArgs e)
                => ApplyScroll(_scrollPx - e.Delta / 120 * 50);

            private void HookMouseWheel(Control c, bool hook)
            {
                if (hook) c.MouseWheel += OnForwardedWheel;
                else      c.MouseWheel -= OnForwardedWheel;
                foreach (Control child in c.Controls)
                    HookMouseWheel(child, hook);
            }

            private void ApplyScroll(int target)
            {
                int maxScroll = Math.Max(0, TotalContentHeight() - Height);
                _scrollPx     = Math.Clamp(target, 0, maxScroll);
                _content.Top  = -_scrollPx;
                UpdateThumb();
            }

            private void Relayout()
            {
                _content.Left  = 0;
                _content.Width = Math.Max(Width - BarW - BarRight - 2, 100);
                _content.Top   = -_scrollPx;

                _track.Left   = Width - BarW - BarRight;
                _track.Top    = 6;
                _track.Width  = BarW;
                _track.Height = Math.Max(Height - 12, 0);

                int w = AvailableCardWidth();
                foreach (Control c in _content.Controls)
                    c.Width = w;

                RefreshScroll();
            }

            private void RefreshScroll()
            {
                int total = TotalContentHeight();

                _content.Height = Math.Max(total, Height);

                bool needsBar = total > Height;
                _track.Visible = needsBar;

                int max = Math.Max(0, total - Height);
                if (_scrollPx > max) ApplyScroll(max);

                UpdateThumb();
            }

            private void UpdateThumb()
            {
                int total  = TotalContentHeight();
                int trackH = _track.Height;
                if (total <= Height || trackH <= 0)
                    return;

                int thumbH   = Math.Max(MinThumb, (int)((double)Height / total * trackH));
                int range    = trackH - thumbH;
                int maxScroll = Math.Max(1, total - Height);
                int thumbTop = (int)((double)Math.Clamp(_scrollPx, 0, maxScroll) / maxScroll * range);

                _thumb.Left   = 0;
                _thumb.Width  = BarW;
                _thumb.Height = thumbH;
                _thumb.Top    = thumbTop;

                RefreshThumbRegion();
            }

            private void RefreshThumbRegion()
            {
                if (_thumb.Width <= 0 || _thumb.Height <= 0) return;
                using var path = ColorHelper.RoundedRectPath(
                    new Rectangle(0, 0, _thumb.Width, _thumb.Height),
                    _thumb.Width / 2);
                var old = _thumb.Region;
                _thumb.Region = new Region(path);
                old?.Dispose();
            }

            private int TotalContentHeight()
            {
                int h = _content.Padding.Vertical;
                foreach (Control c in _content.Controls)
                    h += c.Height + c.Margin.Vertical;
                return h;
            }

            private int AvailableCardWidth() =>
                Math.Max(_content.Width - _content.Padding.Horizontal - 2, 140);
        }

        private class DotHeaderPanel : Panel
        {
            public Color  DotColor  { get; set; } = Color.Gray;
            public Color  TextColor { get; set; } = Color.Black;
            public string Text      { get; set; } = "";

            public event EventHandler QuickAddClicked;

            private readonly Button _addButton;
            private readonly Font   _font = new("Segoe UI", 10.5f, FontStyle.Bold);

            public DotHeaderPanel()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

                _addButton = new Button
                {
                    Dock      = DockStyle.Right,
                    Width     = 30,
                    Text      = "+",
                    FlatStyle = FlatStyle.Flat,
                    TabStop   = false,
                    Cursor    = Cursors.Hand,
                    Font      = new Font("Segoe UI", 12f, FontStyle.Bold)
                };
                _addButton.FlatAppearance.BorderSize = 0;
                _addButton.Click += (_, _) => QuickAddClicked?.Invoke(this, EventArgs.Empty);
                Controls.Add(_addButton);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                const int dotSize = 10;
                var dotRect = new Rectangle(16, (Height - dotSize) / 2, dotSize, dotSize);
                using (var brush = new SolidBrush(DotColor))
                    e.Graphics.FillEllipse(brush, dotRect);

                var textRect = new Rectangle(dotRect.Right + 10, 0,
                    Width - dotRect.Right - _addButton.Width - 20, Height);
                TextRenderer.DrawText(e.Graphics, Text, _font, textRect, TextColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                Invalidate();
            }
        }
    }
}
