using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using KanbanDashboard.Models;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    public class TaskCardControl : Panel
    {
        public TaskItem Task { get; private set; }

        public event EventHandler EditRequested;
        public event EventHandler DeleteRequested;
        public event EventHandler<bool> CompletedChanged;

        public event EventHandler DragStarted;
        public event EventHandler DragMoved;
        /// <summary>The argument is true for a drop, false if the drag was cancelled.</summary>
        public event EventHandler<bool> DragEnded;

        public const int CornerRadius = 12;

        private readonly Panel _headerRow;
        private readonly Pill _statusPill;
        private readonly Button _kebabButton;
        private readonly ContextMenuStrip _kebabMenu;

        private readonly Label _titleLabel;
        private readonly Label _descriptionLabel;

        private readonly Panel _progressRow;
        private readonly ProgressTrack _progressTrack;
        private readonly Label _progressPercentLabel;

        private readonly Panel _footerRow;
        private readonly Label _statusLabel;
        private readonly ToggleDoneButton _toggleButton;

        private Color _accent = Color.Gray;
        private bool  _styledAsOverdue;

        private bool  _pressed;
        private bool  _dragging;
        private Point _pressPoint;

        // Shared by every card: a re-render recreates all cards, so per-card fonts would
        // allocate (and leak) several GDI font handles each time.
        private static readonly Font KebabFont       = new("Segoe UI", 10f, FontStyle.Bold);
        private static readonly Font TitleFont       = new("Segoe UI", 10.5f, FontStyle.Bold);
        private static readonly Font DescriptionFont = new("Segoe UI", 8.5f);
        private static readonly Font PercentFont     = new("Segoe UI", 8f);
        private static readonly Font StatusFont      = new("Segoe UI", 8.5f);
        private static readonly Font PillFont        = new("Segoe UI", 7.75f, FontStyle.Bold);
        private static readonly Font ToggleFont      = new("Segoe UI", 9f, FontStyle.Bold);

        public TaskCardControl(TaskItem task)
        {
            Task = task;
            bool hasDescription = !string.IsNullOrWhiteSpace(task.Description);

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            Margin = new Padding(0, 0, 0, 12);
            Padding = new Padding(14, 12, 14, 12);
            Cursor = Cursors.Hand;

            // Child rows/labels leave BackColor unset so they inherit the card's solid colour.
            // (Transparent would make WinForms repaint the card behind every label.)
            _headerRow = new Panel { Dock = DockStyle.Top, Height = 26 };

            _statusPill = new Pill { Dock = DockStyle.Left, Width = 92, Height = 22 };

            _kebabMenu = new ContextMenuStrip();
            _kebabMenu.Items.Add("Edit", null, (_, _) => RaiseAfterInput(EditRequested));
            _kebabMenu.Items.Add("Delete", null, (_, _) => RaiseAfterInput(DeleteRequested));

            _kebabButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 26,
                Text = "\u22EF",
                FlatStyle = FlatStyle.Flat,
                TabStop = false,
                Cursor = Cursors.Hand,
                Font = KebabFont
            };
            _kebabButton.FlatAppearance.BorderSize = 0;
            _kebabButton.Click += (_, _) => _kebabMenu.Show(_kebabButton, new Point(0, _kebabButton.Height));

            _headerRow.Controls.Add(_statusPill);
            _headerRow.Controls.Add(_kebabButton);

            _titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.BottomLeft,
                Font = TitleFont,
            };

            _descriptionLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = hasDescription ? 20 : 0,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.TopLeft,
                Font = DescriptionFont,
                Visible = hasDescription
            };

            _progressRow = new Panel { Dock = DockStyle.Top, Height = 26 };
            _progressPercentLabel = new Label
            {
                Dock = DockStyle.Right,
                Width = 42,
                TextAlign = ContentAlignment.MiddleRight,
                Font = PercentFont,
            };
            _progressTrack = new ProgressTrack { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 6, 8) };
            _progressRow.Controls.Add(_progressTrack);
            _progressRow.Controls.Add(_progressPercentLabel);

            _footerRow = new Panel { Dock = DockStyle.Top, Height = 26 };
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Font = StatusFont,
            };
            _toggleButton = new ToggleDoneButton { Dock = DockStyle.Right, Width = 26, Height = 26 };
            _toggleButton.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    bool newState = !Task.IsCompleted;
                    RaiseAfterInput(() => CompletedChanged?.Invoke(this, newState));
                }
            };
            _footerRow.Controls.Add(_statusLabel);
            _footerRow.Controls.Add(_toggleButton);

            Controls.Add(_footerRow);
            Controls.Add(_progressRow);
            Controls.Add(_descriptionLabel);
            Controls.Add(_titleLabel);
            Controls.Add(_headerRow);

            DoubleClick += (_, _) => RaiseAfterInput(EditRequested);
            _titleLabel.DoubleClick += (_, _) => RaiseAfterInput(EditRequested);

            Resize += (_, _) => UpdateRoundedRegion();

            HookDrag(this);

            RefreshFromTask();
        }

        // The whole card is a drag handle, except the buttons that have their own click action.
        private void HookDrag(Control c)
        {
            if (c == _kebabButton || c == _toggleButton)
                return;

            c.MouseDown           += OnDragMouseDown;
            c.MouseMove           += OnDragMouseMove;
            c.MouseUp             += OnDragMouseUp;
            c.MouseCaptureChanged += (_, _) => EndDrag(dropped: false);

            foreach (Control child in c.Controls)
                HookDrag(child);
        }

        private void OnDragMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _pressed    = true;
            _pressPoint = Cursor.Position;
        }

        private void OnDragMouseMove(object sender, MouseEventArgs e)
        {
            if (!_pressed) return;

            if (!_dragging)
            {
                var size     = SystemInformation.DragSize;
                var deadzone = new Rectangle(
                    _pressPoint.X - size.Width / 2, _pressPoint.Y - size.Height / 2, size.Width, size.Height);
                if (deadzone.Contains(Cursor.Position))
                    return;

                _dragging = true;
                DragStarted?.Invoke(this, EventArgs.Empty);
            }

            DragMoved?.Invoke(this, EventArgs.Empty);
        }

        private void OnDragMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            EndDrag(dropped: true);
            _pressed = false;
        }

        public void CancelDrag() => EndDrag(dropped: false);

        private void EndDrag(bool dropped)
        {
            if (!_dragging) return;
            _dragging = false;
            _pressed  = false;
            DragEnded?.Invoke(this, dropped);
        }

        private void RaiseAfterInput(EventHandler handler) =>
            RaiseAfterInput(() => handler?.Invoke(this, EventArgs.Empty));

        // Handlers re-render the board, which removes and disposes this card. Posting the
        // event lets the current click / menu interaction finish before that happens.
        private void RaiseAfterInput(Action raise)
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(raise);
            else
                raise();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _kebabMenu.Dispose();

                var region = Region;
                Region = null;
                region?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Cheap per-minute update: refreshes the countdown and progress, and re-applies the
        /// theme only when the task has just become overdue (the one time-driven style change).
        /// </summary>
        public void RefreshTimeDependent(AppTheme theme)
        {
            _statusLabel.Text = BuildStatusText();
            UpdateProgress();

            if (Task.IsOverdue != _styledAsOverdue)
                ApplyTheme(theme);
        }

        public void RefreshFromTask()
        {
            // The description row only exists when there is a description, so an edit can
            // change the card's height.
            bool hasDescription = !string.IsNullOrWhiteSpace(Task.Description);
            _descriptionLabel.Visible = hasDescription;
            _descriptionLabel.Height  = hasDescription ? 20 : 0;
            Height = 24 + 26 + 24 + (hasDescription ? 20 : 0) + 26 + 26;

            _titleLabel.Text = string.IsNullOrWhiteSpace(Task.Title) ? "(untitled)" : Task.Title;
            _descriptionLabel.Text = Task.Description;

            _statusLabel.Text = BuildStatusText();
            _toggleButton.IsDone = Task.IsCompleted;

            UpdateProgress();
        }

        private string BuildStatusText()
        {
            if (!Task.DueAt.HasValue)
                return "No deadline";

            if (Task.IsCompleted)
                return Task.WasCompletedLate ? $"Completed late \u00b7 was due {Due("MMM d")}" : "Completed on time";

            var remaining = Task.TimeRemaining!.Value;
            if (remaining <= TimeSpan.Zero)
                return $"Overdue \u00b7 was due {Due("MMM d, h:mm tt")}";

            if (remaining.TotalDays >= 1)
                return $"Due {Due("MMM d")} \u00b7 {(int)remaining.TotalDays}d {remaining.Hours}h left";

            return $"Due {Due("h:mm tt")} \u00b7 {remaining.Hours}h {remaining.Minutes}m left";
        }

        // Always English ("Dec 13", "1:55 PM"), whatever the Windows display language is.
        private string Due(string format) => Task.DueAt.Value.ToString(format, CultureInfo.InvariantCulture);

        private void UpdateProgress()
        {
            if (!Task.DueAt.HasValue)
            {
                _progressTrack.Percent = 0;
                _progressPercentLabel.Text = "\u2014";
                return;
            }

            if (Task.IsCompleted)
            {
                _progressTrack.Percent = 1f;
                _progressPercentLabel.Text = "100%";
                return;
            }

            var total = (Task.DueAt.Value - Task.CreatedAt).TotalSeconds;
            var elapsed = (DateTime.Now - Task.CreatedAt).TotalSeconds;
            var pct = total > 0 ? (float)Math.Clamp(elapsed / total, 0, 1) : 1f;

            _progressTrack.Percent = pct;
            _progressPercentLabel.Text = $"{(int)(pct * 100)}%";
        }

        public void ApplyTheme(AppTheme theme)
        {
            var baseColor = ColorHelper.ParseHexOrDefault(theme.CardBackgroundHex, Color.Gainsboro);
            var textColor = ColorHelper.ParseHexOrDefault(theme.TextHex, Color.Black);
            var accent    = ColorHelper.ParseHexOrDefault(ColumnAccentHex(theme), Color.Gray);

            string pillText  = Task.Column.ToString();
            Color background = baseColor;

            if (Task.Column == BoardColumn.Done)
            {
                var vibeHex  = Task.WasCompletedLate ? theme.DoneLateHex : theme.DoneSuccessHex;
                var vibe     = ColorHelper.ParseHexOrDefault(vibeHex, baseColor);
                background   = ColorHelper.Soften(vibe, 0.75f);
                accent       = vibe;
                pillText     = Task.WasCompletedLate ? "Late" : "Done";

                textColor = ColorHelper.ContrastColor(background);
            }
            else if (Task.IsOverdue)
            {
                var red  = ColorHelper.ParseHexOrDefault(theme.DoneLateHex, Color.IndianRed);
                background = ColorHelper.Soften(red, 0.82f);
                accent     = red;
                pillText   = "Overdue";

                textColor = ColorHelper.ContrastColor(background);
            }

            var mutedText = ColorHelper.Soften(textColor, 0.25f);

            BackColor        = background;
            _styledAsOverdue = Task.IsOverdue;
            _accent   = accent;

            _statusPill.SetColors(accent);
            _statusPill.Text = pillText;

            _titleLabel.ForeColor          = textColor;
            _descriptionLabel.ForeColor    = mutedText;
            _statusLabel.ForeColor         = mutedText;
            _progressPercentLabel.ForeColor = mutedText;

            _progressTrack.TrackColor = ColorHelper.Soften(textColor, 0.85f);
            _progressTrack.FillColor  = accent;

            _kebabButton.ForeColor = mutedText;
            _kebabButton.FlatAppearance.MouseOverBackColor = ColorHelper.Soften(textColor, 0.9f);

            _toggleButton.AccentColor = accent;
            _toggleButton.ForeColor   = Color.White;

            Invalidate();
        }

        private string ColumnAccentHex(AppTheme theme) => Task.Column switch
        {
            BoardColumn.Urgent      => theme.UrgentColumnHex,
            BoardColumn.Prioritized => theme.PrioritizedColumnHex,
            BoardColumn.Chill       => theme.ChillColumnHex,
            BoardColumn.Done        => theme.DoneColumnHex,
            _                       => theme.CardBackgroundHex
        };

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateRoundedRegion();
        }

        private void UpdateRoundedRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using var path = ColorHelper.RoundedRectPath(
                new Rectangle(0, 0, Width, Height), CornerRadius);
            var old = Region;
            Region = new Region(path);
            old?.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = ColorHelper.RoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
            using var pen = new Pen(Color.FromArgb(90, _accent), 1.4f);
            e.Graphics.DrawPath(pen, path);
        }

        private class Pill : Panel
        {
            private Color _fill = Color.Gray;
            private Color _textColor = Color.White;

            public Pill()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                Font = PillFont;
            }

            public void SetColors(Color accent)
            {
                _fill = ColorHelper.Soften(accent, 0.78f);
                _textColor = ColorHelper.Deepen(accent, 0.15f);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = ColorHelper.RoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2);
                using var brush = new SolidBrush(_fill);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, _textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private class ProgressTrack : Panel
        {
            private float _percent;

            public float Percent
            {
                get => _percent;
                set
                {
                    if (_percent == value) return;
                    _percent = value;
                    Invalidate();
                }
            }
            public Color TrackColor { get; set; } = Color.LightGray;
            public Color FillColor { get; set; } = Color.Gray;

            public ProgressTrack()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                Height = 8;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 0 || Height <= 0)
                    return;

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var trackRect = new Rectangle(0, Math.Max((Height - 6) / 2, 0), Width - 1, 6);
                using (var trackPath = ColorHelper.RoundedRectPath(trackRect, 3))
                using (var trackBrush = new SolidBrush(TrackColor))
                    e.Graphics.FillPath(trackBrush, trackPath);

                var fillWidth = (int)(trackRect.Width * Math.Clamp(Percent, 0f, 1f));
                if (fillWidth > 2)
                {
                    var fillRect = new Rectangle(trackRect.X, trackRect.Y, fillWidth, trackRect.Height);
                    using var fillPath = ColorHelper.RoundedRectPath(fillRect, 3);
                    using var fillBrush = new SolidBrush(FillColor);
                    e.Graphics.FillPath(fillBrush, fillPath);
                }
            }
        }

        private class ToggleDoneButton : Panel
        {
            public Color AccentColor { get; set; } = Color.Gray;
            private bool _isDone;

            public bool IsDone
            {
                get => _isDone;
                set { _isDone = value; Invalidate(); }
            }

            public ToggleDoneButton()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
                Font   = ToggleFont;
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                Control c = Parent;
                while (c != null && (c.BackColor == Color.Transparent || c.BackColor.A == 0))
                    c = c.Parent;
                var bg = c?.BackColor ?? BackColor;
                using var brush = new SolidBrush(bg);
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var fill = _isDone ? AccentColor : Color.Transparent;
                using (var brush = new SolidBrush(fill))
                    e.Graphics.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
                using (var pen = new Pen(AccentColor, 1.4f))
                    e.Graphics.DrawEllipse(pen, 1, 1, Width - 3, Height - 3);

                if (_isDone)
                    TextRenderer.DrawText(e.Graphics, "\u2713", Font, ClientRectangle, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
