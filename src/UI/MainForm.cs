using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KanbanDashboard.Models;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    public partial class MainForm : Form
    {
        private readonly DataStore _store = new();
        private AppData _data;
        private bool    _saveErrorShown;

        private readonly System.Windows.Forms.Timer _deadlineTimer = new() { Interval = 60_000 };
        private readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 1_000 };

        public MainForm()
        {
            BuildLayout();
            WireEvents();

            _data = _store.Load();
            if (_store.LoadWarning != null)
                Shown += (_, _) => MessageBox.Show(this, _store.LoadWarning, "Khoi's Kanban",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

            DeadlineService.ApplyRules(_data.Tasks, DateTime.Now);

            ApplyTheme();
            RenderBoard();

            _deadlineTimer.Tick += (_, _) => OnDeadlineTick();
            _deadlineTimer.Start();

            _clockTimer.Tick += (_, _) => _clockLabel.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();
        }

        private void WireEvents()
        {
            _addTaskButton.Click   += (_, _) => AddTask();
            _themeButton.Click     += (_, _) => EditTheme();
            _clearDoneButton.Click += (_, _) => ClearDone();

            foreach (var col in _allColumns)
            {
                col.EditRequested     += (_, task)   => EditTask(task);
                col.DeleteRequested   += (_, task)   => DeleteTask(task);
                col.CompletedChanged  += (_, args)   => SetCompleted(args.Task, args.Completed);
                col.QuickAddRequested += (_, column) => AddTask(column);
            }

            FormClosing += OnFormClosingSave;

            MouseDown        += OnTopBarMouseDown;
            _topBar.MouseDown      += OnTopBarMouseDown;
            _appTitleLabel.MouseDown += OnTopBarMouseDown;
            _clockLabel.MouseDown    += OnTopBarMouseDown;

            KeyPreview = true;
            KeyDown    += OnGlobalKeyDown;
        }

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION       = 0x02;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private void OnTopBarMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private const int WM_NCHITTEST  = 0x0084;
        private const int HTLEFT        = 10;
        private const int HTRIGHT       = 11;
        private const int HTTOP         = 12;
        private const int HTTOPLEFT     = 13;
        private const int HTTOPRIGHT    = 14;
        private const int HTBOTTOM      = 15;
        private const int HTBOTTOMLEFT  = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int GripSize      = 8;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                int lp  = (int)m.LParam.ToInt64();
                var pos = PointToClient(new Point((short)(lp & 0xFFFF), (short)(lp >> 16)));

                bool left   = pos.X <= GripSize;
                bool right  = pos.X >= Width  - GripSize;
                bool top    = pos.Y <= GripSize;
                bool bottom = pos.Y >= Height - GripSize;

                if      (top    && left)  { m.Result = (IntPtr)HTTOPLEFT;     return; }
                else if (top    && right) { m.Result = (IntPtr)HTTOPRIGHT;    return; }
                else if (bottom && left)  { m.Result = (IntPtr)HTBOTTOMLEFT;  return; }
                else if (bottom && right) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                else if (left)            { m.Result = (IntPtr)HTLEFT;        return; }
                else if (right)           { m.Result = (IntPtr)HTRIGHT;       return; }
                else if (top)             { m.Result = (IntPtr)HTTOP;         return; }
                else if (bottom)          { m.Result = (IntPtr)HTBOTTOM;      return; }
            }

            base.WndProc(ref m);
        }
        private void OnGlobalKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.T && e.Control)
            {
                e.SuppressKeyPress = true;
                AddTask();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                Application.Exit();
            }
        }

        private void SaveBoard()
        {
            if (_store.Save(_data, out var error))
            {
                _saveErrorShown = false;
                return;
            }

            // Warn once per failure streak; the timer may retry saving every minute.
            if (_saveErrorShown)
                return;
            _saveErrorShown = true;
            MessageBox.Show(this, "Your board could not be saved:\n" + error +
                "\n\nChanges are kept in memory and saving will be retried on the next change.",
                "Khoi's Kanban", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void OnFormClosingSave(object sender, FormClosingEventArgs e)
        {
            if (_store.Save(_data, out var error))
                return;

            if (e.CloseReason == CloseReason.WindowsShutDown)
                return;

            var choice = MessageBox.Show(this, "Your board could not be saved:\n" + error +
                "\n\nClose anyway and lose unsaved changes?", "Khoi's Kanban",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (choice != DialogResult.Yes)
                e.Cancel = true;
        }

        private void OnDeadlineTick()
        {
            // Rebuilding recreates every card (~12 window handles each), so only do it when a
            // task actually moved column; otherwise just refresh the time-based text in place.
            if (DeadlineService.ApplyRules(_data.Tasks, DateTime.Now))
            {
                RenderBoard();
                SaveBoard();
                return;
            }

            foreach (var col in _allColumns)
                col.RefreshCards(_data.Theme);
        }

        private void AddTask(BoardColumn? presetColumn = null)
        {
            using var dialog = new TaskEditDialog("Add Task", initialColumn: presetColumn, theme: _data.Theme);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var task = new TaskItem
            {
                Title       = dialog.TaskTitle,
                Description = dialog.TaskDescription,
                DueAt       = dialog.Deadline,
                Column      = dialog.SelectedColumn,
                IsCompleted = dialog.SelectedColumn == BoardColumn.Done
            };
            if (task.IsCompleted)
                task.CompletedAt = DateTime.Now;

            _data.Tasks.Add(task);

            DeadlineService.ApplyRules(_data.Tasks, DateTime.Now);

            RenderBoard();
            SaveBoard();
        }

        private void EditTask(TaskItem task)
        {
            using var dialog = new TaskEditDialog("Edit Task", task, theme: _data.Theme);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            task.Title       = dialog.TaskTitle;
            task.Description = dialog.TaskDescription;
            task.DueAt       = dialog.Deadline;
            task.Column      = dialog.SelectedColumn;

            if (task.Column == BoardColumn.Done && !task.IsCompleted)
            {
                task.IsCompleted = true;
                task.CompletedAt = DateTime.Now;
            }
            else if (task.Column != BoardColumn.Done)
            {
                task.IsCompleted = false;
                task.CompletedAt = null;
            }

            DeadlineService.ApplyRules(_data.Tasks, DateTime.Now);
            RenderBoard();
            SaveBoard();
        }

        private void DeleteTask(TaskItem task)
        {
            var confirm = MessageBox.Show(this, $"Delete \"{task.Title}\"?", "Khoi's Kanban",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            _data.Tasks.Remove(task);
            RenderBoard();
            SaveBoard();
        }

        private void SetCompleted(TaskItem task, bool completed)
        {
            task.IsCompleted = completed;
            if (completed)
            {
                task.CompletedAt = DateTime.Now;
                task.Column      = BoardColumn.Done;
            }
            else
            {
                task.CompletedAt = null;
                task.Column = BoardColumn.Chill;
                DeadlineService.ApplyRules(_data.Tasks, DateTime.Now);
            }

            RenderBoard();
            SaveBoard();
        }

        private void ClearDone()
        {
            int count = _data.Tasks.Count(t => t.Column == BoardColumn.Done);
            if (count == 0)
            {
                MessageBox.Show(this, "The Done column is already empty.", "Khoi's Kanban",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(this,
                $"Remove all {count} task{(count == 1 ? "" : "s")} from the Done column? This cannot be undone.",
                "Khoi's Kanban", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            _data.Tasks.RemoveAll(t => t.Column == BoardColumn.Done);
            RenderBoard();
            SaveBoard();
        }

        private void EditTheme()
        {
            using var dialog = new ThemeDialog(_data.Theme);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            _data.Theme = dialog.ResultTheme;
            ApplyTheme();
            RenderBoard();
            SaveBoard();
        }

        private void ApplyTheme()
        {
            var theme      = _data.Theme;
            var background = ColorHelper.ParseHexOrDefault(theme.BackgroundHex, SystemColors.Control);
            var panel      = ColorHelper.ParseHexOrDefault(theme.PanelHex,      SystemColors.ControlLight);
            var text       = ColorHelper.ParseHexOrDefault(theme.TextHex,       Color.Black);
            var primary    = ColorHelper.ParseHexOrDefault(theme.ChillColumnHex, Color.SteelBlue);

            BackColor              = background;
            _boardLayout.BackColor = background;

            _topBar.BackColor        = background;
            _appTitleLabel.BackColor = background;
            _appTitleLabel.ForeColor = text;

            _clockLabel.BackColor = background;
            _clockLabel.ForeColor = ColorHelper.Soften(primary, 0.15f);

            _themeButton.BackColor = panel;
            _themeButton.ForeColor = text;
            _themeButton.FlatAppearance.BorderColor = ColorHelper.Soften(text, 0.6f);

            var doneAccent = ColorHelper.ParseHexOrDefault(theme.DoneColumnHex, Color.SeaGreen);
            _clearDoneButton.BackColor = panel;
            _clearDoneButton.ForeColor = doneAccent;
            _clearDoneButton.FlatAppearance.BorderColor = doneAccent;

            _addTaskButton.BackColor = primary;
            _addTaskButton.ForeColor = Color.White;

            _urgentColumn.SetAccentColor(ColorHelper.ParseHexOrDefault(theme.UrgentColumnHex,           Color.IndianRed));
            _prioritizedColumn.SetAccentColor(ColorHelper.ParseHexOrDefault(theme.PrioritizedColumnHex, Color.Orange));
            _chillColumn.SetAccentColor(ColorHelper.ParseHexOrDefault(theme.ChillColumnHex,             Color.SteelBlue));
            _doneColumn.SetAccentColor(ColorHelper.ParseHexOrDefault(theme.DoneColumnHex,               Color.SeaGreen));

            foreach (var col in _allColumns)
                col.SetBackgroundColors(panel, text);
        }

        private void RenderBoard()
        {
            foreach (var col in _allColumns)
                col.BeginRebuild();

            var nonDone = _data.Tasks
                .Where(t => t.Column != BoardColumn.Done)
                .OrderBy(t => t.DueAt ?? DateTime.MaxValue)
                .ThenBy(t => t.CreatedAt);

            var done = _data.Tasks
                .Where(t => t.Column == BoardColumn.Done)
                .OrderByDescending(t => t.CompletedAt ?? DateTime.MinValue)
                .ThenByDescending(t => t.CreatedAt);

            foreach (var task in nonDone.Concat(done))
            {
                var card = new TaskCardControl(task);
                card.ApplyTheme(_data.Theme);
                PanelFor(task.Column).AddCard(card);
            }

            foreach (var col in _allColumns)
                col.EndRebuild();

            foreach (var col in _allColumns)
                col.UpdateHeaderCount(_data.Tasks.Count(t => t.Column == col.Column));
        }

    }
}
