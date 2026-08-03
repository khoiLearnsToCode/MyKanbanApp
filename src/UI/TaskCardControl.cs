using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KanbanDashboard.Models;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    /// <summary>
    /// Visual representation of a single <see cref="TaskItem"/>: a rounded card with
    /// a colored status pill, title, optional description, a time-to-deadline
    /// progress bar, and a footer with the due-status text + a round "done" toggle.
    /// Built entirely in code (no designer file), using Dock-based layout so it
    /// stretches cleanly to whatever width the column gives it.
    /// </summary>
    public class TaskCardControl : Panel
    {
        public TaskItem Task { get; private set; }

        public event EventHandler EditRequested;
        public event EventHandler DeleteRequested;
        public event EventHandler<bool> CompletedChanged;

        private const int CornerRadius = 12;

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

        public TaskCardControl(TaskItem task)
        {
            Task = task;
            bool hasDescription = !string.IsNullOrWhiteSpace(task.Description);

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            Margin = new Padding(0, 0, 0, 12);
            Padding = new Padding(14, 12, 14, 12);
            Cursor = Cursors.Hand;

            // ---------- header: status pill (left) + kebab menu button (right) ----------
            _headerRow = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Color.Transparent };

            _statusPill = new Pill { Dock = DockStyle.Left, Width = 92, Height = 22 };

            _kebabMenu = new ContextMenuStrip();
            _kebabMenu.Items.Add("Edit", null, (_, _) => EditRequested?.Invoke(this, EventArgs.Empty));
            _kebabMenu.Items.Add("Delete", null, (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty));

            _kebabButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 26,
                Text = "\u22EF", // horizontal ellipsis, reads as a "..." kebab menu
                FlatStyle = FlatStyle.Flat,
                TabStop = false,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            _kebabButton.FlatAppearance.BorderSize = 0;
            _kebabButton.Click += (_, _) => _kebabMenu.Show(_kebabButton, new Point(0, _kebabButton.Height));

            _headerRow.Controls.Add(_statusPill);
            _headerRow.Controls.Add(_kebabButton);

            // ---------- title ----------
            _titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                BackColor = Color.Transparent
            };

            // ---------- description (collapses to 0 height if there's none) ----------
            _descriptionLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = hasDescription ? 20 : 0,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font("Segoe UI", 8.5f),
                BackColor = Color.Transparent,
                Visible = hasDescription
            };

            // ---------- time-to-deadline progress bar ----------
            _progressRow = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Color.Transparent };
            _progressPercentLabel = new Label
            {
                Dock = DockStyle.Right,
                Width = 42,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 8f),
                BackColor = Color.Transparent
            };
            _progressTrack = new ProgressTrack { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 6, 8) };
            _progressRow.Controls.Add(_progressTrack);
            _progressRow.Controls.Add(_progressPercentLabel);

            // ---------- footer: due-status text (left) + round done toggle (right) ----------
            _footerRow = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Color.Transparent };
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 8.5f),
                BackColor = Color.Transparent
            };
            _toggleButton = new ToggleDoneButton { Dock = DockStyle.Right, Width = 26, Height = 26 };
            _toggleButton.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    bool newState = !Task.IsCompleted;
                    CompletedChanged?.Invoke(this, newState);
                }
            };
            _footerRow.Controls.Add(_statusLabel);
            _footerRow.Controls.Add(_toggleButton);

            // Add order is the reverse of visual top-to-bottom order: Panel's docking
            // engine processes the LAST added control first, so it claims the topmost
            // slice. Adding footer -> progress -> description -> title -> header means
            // header ends up on top, footer on bottom - the order you'd read them in.
            Controls.Add(_footerRow);
            Controls.Add(_progressRow);
            Controls.Add(_descriptionLabel);
            Controls.Add(_titleLabel);
            Controls.Add(_headerRow);

            DoubleClick += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);
            _titleLabel.DoubleClick += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);

            Height = 24 /*padding*/ + 26 /*header*/ + 24 /*title*/ + (hasDescription ? 20 : 0) + 26 /*progress*/ + 26 /*footer*/;

            Resize += (_, _) => UpdateRoundedRegion();

            RefreshFromTask();
        }

        /// <summary>Re-reads the underlying TaskItem and updates all visible text/colors/progress.</summary>
        public void RefreshFromTask()
        {
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
                return Task.WasCompletedLate ? $"Completed late \u00b7 was due {Task.DueAt:MMM d}" : "Completed on time";

            var remaining = Task.TimeRemaining!.Value;
            if (remaining <= TimeSpan.Zero)
                return $"Overdue \u00b7 was due {Task.DueAt:MMM d, h:mm tt}";

            if (remaining.TotalDays >= 1)
                return $"Due {Task.DueAt:MMM d} \u00b7 {(int)remaining.TotalDays}d {remaining.Hours}h left";

            return $"Due {Task.DueAt:h:mm tt} \u00b7 {remaining.Hours}h {remaining.Minutes}m left";
        }

        private void UpdateProgress()
        {
            if (!Task.DueAt.HasValue)
            {
                _progressTrack.Percent = 0;
                _progressPercentLabel.Text = "\u2014"; // em dash: nothing to show
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
            _progressTrack.Invalidate();
        }

        /// <summary>Applies the theme's colors, plus the mild green/red "Done" vibe and the status pill's accent.</summary>
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
                background   = ColorHelper.Soften(vibe, 0.75f); // mild tint, not a solid block
                accent       = vibe;
                pillText     = Task.WasCompletedLate ? "Late" : "Done";

                // After softening, the background may be much lighter than the theme's
                // text color (which is usually light in dark themes).  Pick whichever of
                // black / white gives better contrast so text is always readable.
                textColor = ColorHelper.ContrastColor(background);
            }
            else if (Task.IsOverdue)
            {
                var red  = ColorHelper.ParseHexOrDefault(theme.DoneLateHex, Color.IndianRed);
                background = ColorHelper.Soften(red, 0.82f);
                accent     = red;
                pillText   = "Overdue";

                // Same contrast fix for overdue tints in dark themes.
                textColor = ColorHelper.ContrastColor(background);
            }

            var mutedText = ColorHelper.Soften(textColor, 0.25f);

            BackColor = background;
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

        /// <summary>Small rounded "pill" badge used for the column/status label at the top of a card.</summary>
        private class Pill : Panel
        {
            private Color _fill = Color.Gray;
            private Color _textColor = Color.White;

            public Pill()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                Font = new Font("Segoe UI", 7.75f, FontStyle.Bold);
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

        /// <summary>Thin rounded progress track that fills left-to-right based on <see cref="Percent"/>.</summary>
        private class ProgressTrack : Panel
        {
            public float Percent { get; set; }
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

        /// <summary>Small circular toggle that fills with the accent colour when done.</summary>
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
                // Panel has no OS-level text/button rendering — nothing can bleed through.
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                       | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
                Font   = new Font("Segoe UI", 9f, FontStyle.Bold);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                // Fill corners with the card's background so nothing bleeds through.
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
