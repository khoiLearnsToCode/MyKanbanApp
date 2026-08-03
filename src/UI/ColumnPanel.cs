using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KanbanDashboard.Models;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    /// <summary>
    /// One column of the board: a header with a colored status dot + title + count,
    /// a quick-add button, a thin accent underline, then a scrollable stack of
    /// <see cref="TaskCardControl"/>s with a custom themed scrollbar.
    /// </summary>
    public class ColumnPanel : Panel
    {
        public BoardColumn Column { get; }

        public event EventHandler<TaskItem>                       EditRequested;
        public event EventHandler<TaskItem>                       DeleteRequested;
        public event EventHandler<(TaskItem Task, bool Completed)> CompletedChanged;
        public event EventHandler<BoardColumn>                    QuickAddRequested;

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

        public void ClearCards() => _cardsPanel.ClearCards();

        public void AddCard(TaskCardControl card)
        {
            card.EditRequested    += (_, _)         => EditRequested?.Invoke(this, card.Task);
            card.DeleteRequested  += (_, _)         => DeleteRequested?.Invoke(this, card.Task);
            card.CompletedChanged += (_, completed) => CompletedChanged?.Invoke(this, (card.Task, completed));
            _cardsPanel.AddCard(card);
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

        // ── Themed scroll panel ──────────────────────────────────────────────────
        /// <summary>
        /// Replaces the native OS scrollbar with a thin, rounded, Catppuccin-coloured
        /// track+thumb. Scrolling is done by offsetting a FlowLayoutPanel's Top position.
        /// </summary>
        private class ThemedScrollPanel : Panel
        {
            private readonly FlowLayoutPanel _content;
            private readonly Panel           _track;
            private readonly Panel           _thumb;

            private int  _scrollPx       = 0;
            private bool _dragging        = false;
            private int  _dragAnchorY;
            private int  _dragAnchorScroll;

            private const int BarW      = 5;   // thumb / track width
            private const int BarRight  = 4;   // gap from right edge of panel
            private const int MinThumb  = 22;  // minimum thumb height

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

                // ── Thumb drag ────────────────────────────────────────────
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

                // ── Track click (page up/down) ────────────────────────────
                _track.MouseDown += (_, e) =>
                {
                    if (e.Y < _thumb.Top) ApplyScroll(_scrollPx - Height);
                    else                  ApplyScroll(_scrollPx + Height);
                };

                _track.Controls.Add(_thumb);
                Controls.Add(_content);
                Controls.Add(_track);

                Resize += (_, _) => Relayout();
                _content.ControlAdded   += (_, _) => RefreshScroll();
                _content.ControlRemoved += (_, _) => RefreshScroll();
            }

            // ── Public API ────────────────────────────────────────────────

            public void SetScrollColors(Color panelBg, Color text)
            {
                // Track: slightly lighter than the panel background
                // Thumb: 40% of the way from panel toward text — visible but muted
                _track.BackColor = ColorHelper.Soften(panelBg, 0.10f);
                _thumb.BackColor = ColorHelper.Blend(panelBg, text, 0.40f);
                RefreshThumbRegion();
            }

            public void ClearCards()
            {
                _content.Controls.Clear();
                _scrollPx = 0;
                RefreshScroll();
            }

            public void AddCard(TaskCardControl card)
            {
                card.Width = AvailableCardWidth();
                // Forward wheel events from every control on the card to this panel
                HookMouseWheel(card);
                _content.Controls.Add(card);
            }

            // ── Scrolling ─────────────────────────────────────────────────

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                ApplyScroll(_scrollPx - e.Delta / 120 * 50);
            }

            private void OnForwardedWheel(object sender, MouseEventArgs e)
                => ApplyScroll(_scrollPx - e.Delta / 120 * 50);

            private void HookMouseWheel(Control c)
            {
                c.MouseWheel += OnForwardedWheel;
                foreach (Control child in c.Controls)
                    HookMouseWheel(child);
            }

            private void ApplyScroll(int target)
            {
                int maxScroll = Math.Max(0, TotalContentHeight() - Height);
                _scrollPx     = Math.Clamp(target, 0, maxScroll);
                _content.Top  = -_scrollPx;
                UpdateThumb();
            }

            // ── Layout ────────────────────────────────────────────────────

            private void Relayout()
            {
                // Content occupies the full width minus room for the scroll track
                _content.Left  = 0;
                _content.Width = Math.Max(Width - BarW - BarRight - 2, 100);
                _content.Top   = -_scrollPx;

                // Track: thin strip on the right edge, inset top/bottom slightly
                _track.Left   = Width - BarW - BarRight;
                _track.Top    = 6;
                _track.Width  = BarW;
                _track.Height = Math.Max(Height - 12, 0);

                // Resize all existing cards to the new width
                int w = AvailableCardWidth();
                foreach (Control c in _content.Controls)
                    c.Width = w;

                RefreshScroll();
            }

            private void RefreshScroll()
            {
                int total = TotalContentHeight();

                // Ensure content panel is tall enough to contain all cards
                _content.Height = Math.Max(total, Height);

                bool needsBar = total > Height;
                _track.Visible = needsBar;

                // Clamp scroll in case content shrank
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

            // ── Helpers ───────────────────────────────────────────────────

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

        // ── DotHeaderPanel ───────────────────────────────────────────────────────
        /// <summary>Header row: colored status dot, title+count text, and a small "+" quick-add button.</summary>
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
