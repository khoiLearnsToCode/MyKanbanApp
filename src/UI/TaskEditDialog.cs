using System;
using System.Drawing;
using System.Windows.Forms;
using KanbanDashboard.Models;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    public class TaskEditDialog : ThemedDialogBase
    {
        private readonly TextBox        _titleBox;
        private readonly TextBox        _descriptionBox;
        private readonly CheckBox       _hasDeadlineBox;
        private readonly WheelDateTimePicker _deadlinePicker;
        private readonly ThemedDropDown _columnBox;
        private readonly Button         _okButton;
        private readonly Button         _cancelButton;

        public string      TaskTitle       => _titleBox.Text.Trim();
        public string      TaskDescription => _descriptionBox.Text.Trim();
        public DateTime?   Deadline        => _hasDeadlineBox.Checked ? _deadlinePicker.Value : (DateTime?)null;
        public BoardColumn SelectedColumn  => (BoardColumn)_columnBox.SelectedIndex;

        public TaskEditDialog(
            string dialogTitle,
            TaskItem existing = null,
            BoardColumn? initialColumn = null,
            AppTheme theme = null,
            DateTime? initialDeadline = null)
            : base(theme)
        {
            Text       = dialogTitle;
            ClientSize = new Size(400, 530);

            const int headerH = 48;
            const int padX    = 20;
            const int ctrlW   = 360;

            var header = new Panel { Dock = DockStyle.Top, Height = headerH, BackColor = _panel };
            var headerLabel = new Label
            {
                Text      = existing == null ? "Add Task" : "Edit Task",
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

            var body = new Panel
            {
                Location   = new Point(0, headerH),
                Width      = ClientSize.Width,
                Height     = ClientSize.Height - headerH,
                BackColor  = _bg,
                AutoScroll = false
            };

            int y = 16;

            body.Controls.Add(MakeLabel("Title", padX, y));
            y += 20;
            _titleBox = MakeTextBox(padX, y, ctrlW);
            body.Controls.Add(_titleBox);
            y += 32;

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

            _hasDeadlineBox = new CheckBox
            {
                Text      = "Has a deadline",
                Location  = new Point(padX, y),
                AutoSize  = true,
                Checked   = true,
                ForeColor = _text,
                BackColor = _bg
            };
            _hasDeadlineBox.CheckedChanged += (_, _) =>
                _deadlinePicker.Enabled = _hasDeadlineBox.Checked;
            body.Controls.Add(_hasDeadlineBox);
            y += 26;

            _deadlinePicker = new WheelDateTimePicker
            {
                Location  = new Point(padX, y),
                Width     = ctrlW,
                Value     = DateTime.Now.AddDays(1),
                BackColor = _bg,
                ForeColor = _text
            };
            body.Controls.Add(_deadlinePicker);
            y += _deadlinePicker.Height + 10;

            body.Controls.Add(MakeLabel("Starting column", padX, y));
            y += 20;
            var columnAccents = new[]
            {
                ColorHelper.ParseHexOrDefault(theme?.UrgentColumnHex,      Color.IndianRed),
                ColorHelper.ParseHexOrDefault(theme?.PrioritizedColumnHex, Color.Orange),
                ColorHelper.ParseHexOrDefault(theme?.ChillColumnHex,       Color.SteelBlue),
                ColorHelper.ParseHexOrDefault(theme?.DoneColumnHex,        Color.SeaGreen)
            };
            _columnBox = new ThemedDropDown(new[] { "Urgent", "Prioritized", "Chill", "Done" }, columnAccents)
            {
                Location   = new Point(padX, y),
                Width      = ctrlW,
                BackColor  = _bg,
                FieldColor = _inputBg,
                ForeColor  = _text
            };
            _columnBox.SelectedIndex = 2;
            body.Controls.Add(_columnBox);
            y += 36;

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

            if (existing != null)
            {
                _titleBox.Text           = existing.Title;
                _descriptionBox.Text     = existing.Description;
                _hasDeadlineBox.Checked  = existing.DueAt.HasValue;
                _deadlinePicker.Enabled  = existing.DueAt.HasValue;
                if (existing.DueAt.HasValue)
                    _deadlinePicker.Value = existing.DueAt.Value;
                _columnBox.SelectedIndex = (int)existing.Column;
            }
            else if (initialColumn.HasValue)
            {
                _columnBox.SelectedIndex = (int)initialColumn.Value;
            }

            if (existing == null && initialDeadline.HasValue)
                _deadlinePicker.Value = initialDeadline.Value;

            Controls.Add(body);
            Controls.Add(header);
        }

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
    }
}
