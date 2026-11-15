using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using KanbanDashboard.Models;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    /// <summary>
    /// Month calendar of task deadlines, painted as a single control. Rows stretch to fill the
    /// available height, so tiles stay short on a wide window; days with more tasks than fit
    /// show a "+N more" link. It only repaints when its data, month or hover state changes.
    /// </summary>
    public class CalendarView : Control
    {
        private const int HeaderHeight  = 50;
        private const int WeekdayHeight = 26;
        private const int LineHeight    = 20;
        private const int DayNumberArea = 26;

        private static readonly Font TitleFont   = new("Segoe UI", 17f);
        private static readonly Font NavFont     = new("Segoe UI", 10f);
        private static readonly Font WeekdayFont = new("Segoe UI", 9f, FontStyle.Bold);
        private static readonly Font DayFont     = new("Segoe UI", 9.5f);
        private static readonly Font TaskFont    = new("Segoe UI", 9f);
        private static readonly Font MoreFont    = new("Segoe UI", 8.5f, FontStyle.Bold);
        private static readonly CultureInfo En   = CultureInfo.InvariantCulture;
        private static readonly string[] WeekdayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        private enum HitKind { Previous, Next, Title, Task, More, Day }

        private sealed class Hit
        {
            public HitKind         Kind;
            public Rectangle       Bounds;
            public DateTime        Date;
            public TaskItem        Task;
            public List<TaskItem>  Tasks;  // every task of the day, for "+N more"
        }

        private readonly List<Hit> _hits = new();
        private readonly ToolTip   _toolTip = new() { InitialDelay = 400, ReshowDelay = 100 };

        private List<TaskItem> _tasks = new();
        private AppTheme       _theme = AppTheme.CatppuccinFrappe();
        private DateTime       _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private Hit            _hover;

        public event EventHandler<TaskItem> TaskClicked;
        public event EventHandler<DateTime> DayDoubleClicked;

        public CalendarView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetData(IEnumerable<TaskItem> tasks, AppTheme theme)
        {
            _tasks = tasks.Where(t => t.DueAt.HasValue).ToList();
            _theme = theme;
            Invalidate();
        }

        public DateTime Month
        {
            get => _month;
            set
            {
                _month = new DateTime(value.Year, value.Month, 1);
                _hover = null;
                Invalidate();
            }
        }

        // ---- painting ------------------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var bg      = ColorHelper.ParseHexOrDefault(_theme.BackgroundHex, Color.FromArgb(48, 52, 70));
            var panel   = ColorHelper.ParseHexOrDefault(_theme.PanelHex,      Color.FromArgb(41, 44, 60));
            var text    = ColorHelper.ParseHexOrDefault(_theme.TextHex,       Color.Gainsboro);
            var accent  = ColorHelper.ParseHexOrDefault(_theme.ChillColumnHex, Color.SteelBlue);
            var soft    = ColorHelper.Blend(text, panel, 0.40f);
            var faint   = ColorHelper.Blend(panel, text, 0.12f);

            g.Clear(bg);
            _hits.Clear();

            var area = new Rectangle(Padding.Left, Padding.Top,
                                     Math.Max(1, Width - Padding.Horizontal), Math.Max(1, Height - Padding.Vertical));

            // Header: ◄ previous month | Month Year | next month ►
            var prevText = "◄  " + _month.AddMonths(-1).ToString("MMMM", En);
            var nextText = _month.AddMonths(1).ToString("MMMM", En) + "  ►";
            var title    = _month.ToString("MMMM yyyy", En);

            var prevSize  = TextRenderer.MeasureText(prevText, NavFont);
            var nextSize  = TextRenderer.MeasureText(nextText, NavFont);
            var titleSize = TextRenderer.MeasureText(title, TitleFont);

            var prevRect  = new Rectangle(area.Left + 4, area.Top + (HeaderHeight - prevSize.Height) / 2, prevSize.Width + 8, prevSize.Height);
            var nextRect  = new Rectangle(area.Right - nextSize.Width - 12, prevRect.Y, nextSize.Width + 8, nextSize.Height);
            var titleRect = new Rectangle(area.Left + (area.Width - titleSize.Width) / 2, area.Top + (HeaderHeight - titleSize.Height) / 2,
                                          titleSize.Width, titleSize.Height);

            AddHit(HitKind.Previous, prevRect);
            AddHit(HitKind.Next, nextRect);
            AddHit(HitKind.Title, titleRect);

            DrawText(g, prevText, NavFont, prevRect, IsHover(HitKind.Previous) ? text : soft, TextFormatFlags.Left);
            DrawText(g, nextText, NavFont, nextRect, IsHover(HitKind.Next) ? text : soft, TextFormatFlags.Right);
            DrawText(g, title, TitleFont, titleRect, IsHover(HitKind.Title) ? text : accent, TextFormatFlags.HorizontalCenter);

            // Grid
            var grid = new Rectangle(area.Left, area.Top + HeaderHeight, area.Width - 1, area.Height - HeaderHeight - 1);
            using (var panelBrush = new SolidBrush(panel))
                g.FillRectangle(panelBrush, grid);

            var firstCell = _month.AddDays(-(((int)_month.DayOfWeek + 6) % 7));  // Monday on or before the 1st
            int lead      = (_month - firstCell).Days;
            int weeks     = (lead + DateTime.DaysInMonth(_month.Year, _month.Month) + 6) / 7;

            float cellW = grid.Width / 7f;
            int   rowsTop = grid.Top + WeekdayHeight;
            float cellH = (grid.Bottom - rowsTop) / (float)weeks;

            for (int c = 0; c < 7; c++)
            {
                var r = new Rectangle(grid.Left + (int)(c * cellW) + 10, grid.Top, (int)cellW - 10, WeekdayHeight);
                DrawText(g, WeekdayNames[c], WeekdayFont, r, soft, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }

            using (var gridPen = new Pen(faint) { DashStyle = DashStyle.Dot })
            {
                g.DrawRectangle(gridPen, grid);
                g.DrawLine(gridPen, grid.Left, rowsTop, grid.Right, rowsTop);
                for (int c = 1; c < 7; c++)
                    g.DrawLine(gridPen, grid.Left + c * cellW, grid.Top, grid.Left + c * cellW, grid.Bottom);
                for (int w = 1; w < weeks; w++)
                    g.DrawLine(gridPen, grid.Left, rowsTop + w * cellH, grid.Right, rowsTop + w * cellH);
            }

            var byDay = _tasks.GroupBy(t => t.DueAt.Value.Date)
                              .ToDictionary(grp => grp.Key, grp => grp.OrderBy(t => t.DueAt).ToList());

            for (int i = 0; i < weeks * 7; i++)
            {
                var date = firstCell.AddDays(i);
                if (date.Month != _month.Month)
                    continue;  // like the reference: days of other months stay blank

                var cell = Rectangle.FromLTRB(
                    (int)(grid.Left + (i % 7) * cellW), (int)(rowsTop + (i / 7) * cellH),
                    (int)(grid.Left + (i % 7 + 1) * cellW), (int)(rowsTop + (i / 7 + 1) * cellH));
                byDay.TryGetValue(date, out var dayTasks);
                DrawDay(g, cell, date, dayTasks, panel, text, soft, accent);
            }
        }

        private void DrawDay(Graphics g, Rectangle cell, DateTime date, List<TaskItem> dayTasks,
                             Color panel, Color text, Color soft, Color accent)
        {
            AddHit(HitKind.Day, cell, date);

            // Day number (in a filled circle for today, accent-coloured when the day has tasks)
            var numberRect = new Rectangle(cell.X + 6, cell.Y + 4, 24, 22);
            string number = date.Day.ToString(En);
            if (date == DateTime.Today)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var circle = new SolidBrush(ColorHelper.Blend(panel, accent, 0.55f));
                g.FillEllipse(circle, numberRect.X, numberRect.Y, 23, 23);
                DrawText(g, number, DayFont, new Rectangle(numberRect.X, numberRect.Y, 23, 23),
                         ColorHelper.ContrastColor(ColorHelper.Blend(panel, accent, 0.55f)),
                         TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                DrawText(g, number, DayFont, numberRect, dayTasks != null ? accent : soft,
                         TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            if (dayTasks == null)
                return;

            // Every line below the day number shows a task; any overflow is summarised by a
            // "+N more" link on the day-number row, so even a one-line tile still shows a task.
            int capacity = Math.Max(0, (cell.Height - DayNumberArea - 4) / LineHeight);
            int shown    = Math.Min(dayTasks.Count, capacity);
            int y        = cell.Y + DayNumberArea + 2;

            for (int k = 0; k < shown; k++, y += LineHeight)
            {
                var task = dayTasks[k];
                var line = new Rectangle(cell.X + 4, y, cell.Width - 8, LineHeight);
                var hit  = AddHit(HitKind.Task, line, date, task);
                DrawTaskLine(g, line, task, ReferenceEquals(_hover?.Task, task) && _hover.Kind == HitKind.Task,
                             panel, text);
                hit.Tasks = dayTasks;
            }

            if (shown < dayTasks.Count)
            {
                string label = $"+{dayTasks.Count - shown} more";
                var size = TextRenderer.MeasureText(label, MoreFont);
                var more = new Rectangle(cell.Right - size.Width - 10, cell.Y + 4, size.Width + 4, 22);
                var hit  = AddHit(HitKind.More, more, date);
                hit.Tasks = dayTasks;
                bool hovered = _hover != null && _hover.Kind == HitKind.More && _hover.Date == date;
                DrawText(g, label, MoreFont, more, hovered ? text : soft,
                         TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        private void DrawTaskLine(Graphics g, Rectangle line, TaskItem task, bool hovered, Color panel, Color text)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (hovered)
            {
                using var path  = ColorHelper.RoundedRectPath(line, 5);
                using var brush = new SolidBrush(ColorHelper.Blend(panel, text, 0.10f));
                g.FillPath(brush, path);
            }

            var dot = new Rectangle(line.X + 4, line.Y + (line.Height - 9) / 2, 9, 9);
            var ring = DotColor(task);
            using (var pen = new Pen(ring, 1.6f))
                g.DrawEllipse(pen, dot);
            if (task.IsCompleted)
            {
                using var fill = new SolidBrush(ring);
                g.FillEllipse(fill, dot);
            }

            var label = Rectangle.FromLTRB(dot.Right + 5, line.Top, line.Right - 2, line.Bottom);
            var color = task.IsCompleted ? ColorHelper.Blend(text, panel, 0.45f) : text;
            DrawText(g, task.Title, TaskFont, label, color,
                     TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private Color DotColor(TaskItem task)
        {
            string hex =
                task.IsCompleted ? (task.WasCompletedLate ? _theme.DoneLateHex : _theme.DoneSuccessHex)
              : task.IsOverdue   ? _theme.DoneLateHex
              : task.Column switch
                {
                    BoardColumn.Urgent      => _theme.UrgentColumnHex,
                    BoardColumn.Prioritized => _theme.PrioritizedColumnHex,
                    BoardColumn.Chill       => _theme.ChillColumnHex,
                    _                       => _theme.DoneColumnHex
                };
            return ColorHelper.ParseHexOrDefault(hex, Color.Gray);
        }

        private static void DrawText(Graphics g, string s, Font font, Rectangle r, Color color, TextFormatFlags flags) =>
            TextRenderer.DrawText(g, s, font, r, color, flags | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        private Hit AddHit(HitKind kind, Rectangle bounds, DateTime date = default, TaskItem task = null)
        {
            var hit = new Hit { Kind = kind, Bounds = bounds, Date = date, Task = task };
            _hits.Add(hit);
            return hit;
        }

        private bool IsHover(HitKind kind) => _hover?.Kind == kind;

        // ---- input ---------------------------------------------------------------------------

        // Later hits are drawn on top (task lines over their day cell), so search from the end.
        private Hit HitAt(Point p)
        {
            for (int i = _hits.Count - 1; i >= 0; i--)
                if (_hits[i].Bounds.Contains(p))
                    return _hits[i];
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var hit = HitAt(e.Location);
            if (hit?.Kind == HitKind.Day) hit = null;  // plain day area has no hover effect
            if (SameTarget(hit, _hover)) return;

            _hover = hit;
            Cursor = hit != null ? Cursors.Hand : Cursors.Default;
            _toolTip.SetToolTip(this, hit?.Kind == HitKind.Task ? ToolTipFor(hit.Task) : null);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover == null) return;
            _hover = null;
            _toolTip.SetToolTip(this, null);
            Invalidate();
        }

        private static bool SameTarget(Hit a, Hit b) =>
            a == null || b == null ? a == b
                                   : a.Kind == b.Kind && a.Date == b.Date && ReferenceEquals(a.Task, b.Task);

        private static string ToolTipFor(TaskItem task)
        {
            string status = task.IsCompleted ? "Done" : task.IsOverdue ? "Overdue" : task.Column.ToString();
            return $"{task.Title}\nDue {task.DueAt.Value.ToString("ddd MMM d, h:mm tt", En)} · {status}";
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;

            var hit = HitAt(e.Location);
            switch (hit?.Kind)
            {
                case HitKind.Previous: Month = _month.AddMonths(-1); break;
                case HitKind.Next:     Month = _month.AddMonths(1);  break;
                case HitKind.Title:    Month = DateTime.Today;       break;
                case HitKind.Task:     TaskClicked?.Invoke(this, hit.Task); break;
                case HitKind.More:     ShowDayMenu(hit); break;
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            var hit = HitAt(e.Location);
            if (e.Button == MouseButtons.Left && hit?.Kind == HitKind.Day)
                DayDoubleClicked?.Invoke(this, hit.Date);
        }

        // Every task of a crowded day, in a theme-coloured menu created only while it is open.
        private void ShowDayMenu(Hit hit)
        {
            var panel = ColorHelper.ParseHexOrDefault(_theme.PanelHex, Color.FromArgb(41, 44, 60));
            var text  = ColorHelper.ParseHexOrDefault(_theme.TextHex,  Color.Gainsboro);

            var menu = new ContextMenuStrip
            {
                Renderer  = new ThemedMenuRenderer(panel, text),
                ShowImageMargin = false,
                Font      = TaskFont
            };
            foreach (var task in hit.Tasks)
            {
                var item = new ToolStripMenuItem(task.Title)
                {
                    ForeColor                = text,
                    ShortcutKeyDisplayString = task.DueAt.Value.ToString("h:mm tt", En)  // right-aligned
                };
                var t = task;
                item.Click += (_, _) => TaskClicked?.Invoke(this, t);
                menu.Items.Add(item);
            }
            menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(this, new Point(hit.Bounds.Left, hit.Bounds.Bottom));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _toolTip.Dispose();
            base.Dispose(disposing);
        }

        private sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
        {
            private readonly Color _back, _text, _hover, _border;

            public ThemedMenuRenderer(Color back, Color text)
            {
                _back   = back;
                _text   = text;
                _hover  = ColorHelper.Blend(back, text, 0.14f);
                _border = ColorHelper.Blend(back, text, 0.25f);
                RoundedEdges = false;
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using var brush = new SolidBrush(_back);
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using var pen = new Pen(_border);
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                using var brush = new SolidBrush(e.Item.Selected ? _hover : _back);
                e.Graphics.FillRectangle(brush, new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height));
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = _text;
                base.OnRenderItemText(e);
            }
        }
    }
}
