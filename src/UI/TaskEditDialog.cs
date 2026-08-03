using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KanbanDashboard.Models;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    /// <summary>
    /// Modal dialog for creating a new task or editing an existing one.
    /// Fully theme-aware so it matches the main window's dark aesthetic.
    /// </summary>
    public class TaskEditDialog : Form
    {
        private readonly TextBox        _titleBox;
        private readonly TextBox        _descriptionBox;
        private readonly CheckBox       _hasDeadlineBox;
        private readonly DateTimePicker _deadlinePicker;
        private readonly ComboBox       _columnBox;
        private readonly Button         _okButton;
        private readonly Button         _cancelButton;

        public string      TaskTitle       => _titleBox.Text.Trim();
        public string      TaskDescription => _descriptionBox.Text.Trim();
        public DateTime?   Deadline        => _hasDeadlineBox.Checked ? _deadlinePicker.Value : (DateTime?)null;
        public BoardColumn SelectedColumn  => (BoardColumn)_columnBox.SelectedIndex;

        // Theme colors – resolved once in the constructor
        private readonly Color _bg;
        private readonly Color _panel;
        private readonly Color _text;
        private readonly Color _accent;
        private readonly Color _inputBg;

        /// <param name="dialogTitle">Window caption ("Add Task" or "Edit Task").</param>
        /// <param name="existing">Non-null when editing an existing task.</param>
        /// <param name="initialColumn">Pre-selected column for a quick-add from a column header.</param>
        /// <param name="theme">The current AppTheme so the dialog can match it. Null = system defaults.</param>
        public TaskEditDialog(
            string dialogTitle,
            TaskItem existing = null,
            BoardColumn? initialColumn = null,
            AppTheme theme = null)
        {
            // ── Palette ───────────────────────────────────────────────────────
            _bg     = theme != null ? ColorHelper.ParseHexOrDefault(theme.BackgroundHex, Color.FromArgb(35, 38, 52))  : SystemColors.Control;
            _panel  = theme != null ? ColorHelper.ParseHexOrDefault(theme.PanelHex,      Color.FromArgb(48, 52, 70))  : SystemColors.ControlLight;
            _text   = theme != null ? ColorHelper.ParseHexOrDefault(theme.TextHex,        Color.FromArgb(198, 208, 245)) : Color.Black;
            _accent = theme != null ? ColorHelper.ParseHexOrDefault(theme.ChillColumnHex, Color.FromArgb(140, 170, 238)) : Color.SteelBlue;
            _inputBg = theme != null ? ColorHelper.Deepen(_panel, 0.18f) : SystemColors.Window;

            // ── Window chrome ─────────────────────────────────────────────────
            Text            = dialogTitle;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.CenterParent;
            MinimizeBox     = false;
            MaximizeBox     = false;
            ClientSize      = new Size(400, 380);
            Font            = new Font("Segoe UI", 9.5f);
            BackColor       = _bg;
            ForeColor       = _text;

            const int headerH = 48;
            const int padX    = 20;
            const int ctrlW   = 360;

            // ── Header bar ────────────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = headerH, BackColor = _panel };
            var headerLabel = new Label
            {
                Text      = (existing == null ? "Add Task" : "Edit Task"),
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

            // ── Body ──────────────────────────────────────────────────────────
            var body = new Panel
            {
                Location   = new Point(0, headerH),
                Width      = ClientSize.Width,
                Height     = ClientSize.Height - headerH,
                BackColor  = _bg,
                AutoScroll = false
            };

            int y = 16;

            // Title
            body.Controls.Add(MakeLabel("Title", padX, y));
            y += 20;
            _titleBox = MakeTextBox(padX, y, ctrlW);
            body.Controls.Add(_titleBox);
            y += 32;

            // Description
            body.Controls.Add(MakeLabel("Description", padX, y));
            y += 20;
            _descriptionBox = new TextBox
            {
                Location    = new Point(padX, y),
                Width       = ctrlW,
                Height      = 78,
                Multiline   = true,
                ScrollBars  = ScrollBars.Vertical,
                BackColor   = _inputBg,
                ForeColor   = _text,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_descriptionBox);
            y += 86;

            // Deadline checkbox
            _hasDeadlineBox = new CheckBox
            {
                Text      = "Has a deadline",
                Location  = new Point(padX, y),
                AutoSize  = true,
                Checked   = true,
                ForeColor = _text,
                BackColor = _bg
            };
            _hasDeadlineBox.CheckedChanged += (_, _) => _deadlinePicker.Enabled = _hasDeadlineBox.Checked;
            body.Controls.Add(_hasDeadlineBox);
            y += 26;

            // DateTimePicker
            _deadlinePicker = new DateTimePicker
            {
                Location     = new Point(padX, y),
                Width        = ctrlW,
                Format       = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy   h:mm tt",
                Value        = DateTime.Now.AddDays(1),
                CalendarForeColor  = _text,
                CalendarMonthBackground = _panel,
                CalendarTitleBackColor  = _panel,
                CalendarTitleForeColor  = _text,
                CalendarTrailingForeColor = ColorHelper.Soften(_text, 0.5f)
            };
            body.Controls.Add(_deadlinePicker);
            y += 36;

            // Column
            body.Controls.Add(MakeLabel("Starting column", padX, y));
            y += 20;
            _columnBox = new ComboBox
            {
                Location      = new Point(padX, y),
                Width         = ctrlW,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = _inputBg,
                ForeColor     = _text,
                FlatStyle     = FlatStyle.Flat
            };
            _columnBox.Items.AddRange(new object[] { "Urgent", "Prioritized", "Chill", "Done" });
            _columnBox.SelectedIndex = 2;
            body.Controls.Add(_columnBox);
            y += 36;

            // Buttons
            _okButton = CreateStyledButton("Save", new Point(ClientSize.Width - 210, y), 90, accent: true);
            _okButton.DialogResult = DialogResult.OK;
            _okButton.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_titleBox.Text))
                {
                    MessageBox.Show(this, "Please enter a title.", "Khoi's Kanban",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                }
            };

            _cancelButton = CreateStyledButton("Cancel", new Point(ClientSize.Width - 110, y), 90);
            _cancelButton.DialogResult = DialogResult.Cancel;

            body.Controls.Add(_okButton);
            body.Controls.Add(_cancelButton);

            AcceptButton = _okButton;
            CancelButton = _cancelButton;

            // ── Pre-fill for edit ─────────────────────────────────────────────
            if (existing != null)
            {
                _titleBox.Text          = existing.Title;
                _descriptionBox.Text    = existing.Description;
                _hasDeadlineBox.Checked = existing.DueAt.HasValue;
                _deadlinePicker.Enabled = existing.DueAt.HasValue;
                if (existing.DueAt.HasValue)
                    _deadlinePicker.Value = existing.DueAt.Value;
                _columnBox.SelectedIndex = (int)existing.Column;
            }
            else if (initialColumn.HasValue)
            {
                _columnBox.SelectedIndex = (int)initialColumn.Value;
            }

            Controls.Add(body);
            Controls.Add(header);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private Label MakeLabel(string text, int x, int y) => new Label
        {
            Text      = text,
            Location  = new Point(x, y),
            AutoSize  = true,
            ForeColor = ColorHelper.Soften(_text, 0.25f),
            BackColor = _bg,
            Font      = new Font("Segoe UI", 8.5f)
        };

        private TextBox MakeTextBox(int x, int y, int width) => new TextBox
        {
            Location    = new Point(x, y),
            Width       = width,
            BackColor   = _inputBg,
            ForeColor   = _text,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = new Font("Segoe UI", 9.5f)
        };

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
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = accent
                ? ColorHelper.Soften(_accent, 0.2f)
                : ColorHelper.Soften(_panel, 0.1f);
            return btn;
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
            using var pen = new Pen(ColorHelper.Soften(_panel, 0.3f), 1.5f);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
