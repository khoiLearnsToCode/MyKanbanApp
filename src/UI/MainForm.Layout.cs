using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KanbanDashboard.Models;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    public partial class MainForm
    {
        private Panel      _topBar;
        private Label      _appTitleLabel;
        private Label      _clockLabel;
        private PillButton _addTaskButton;
        private Button     _themeButton;
        private Button     _clearDoneButton;

        private TableLayoutPanel _boardLayout;

        private ColumnPanel _urgentColumn;
        private ColumnPanel _prioritizedColumn;
        private ColumnPanel _chillColumn;
        private ColumnPanel _doneColumn;

        private ColumnPanel[] _allColumns;

        private void BuildLayout()
        {
            Text            = "Khoi's Kanban Dashboard";
            StartPosition   = FormStartPosition.CenterScreen;
            MinimumSize     = new Size(900, 500);
            Size            = new Size(1240, 760);
            Font            = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.None;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            _topBar = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(20, 0, 20, 0) };

            _appTitleLabel = new Label
            {
                Dock      = DockStyle.Left,
                AutoSize  = false,
                Width     = 380,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 14f, FontStyle.Bold),
                Text      = "Welcome back, Khoi!"
            };

            _clockLabel = new Label
            {
                Dock      = DockStyle.Left,
                AutoSize  = false,
                Width     = 160,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Consolas", 16f, FontStyle.Bold),
                Text      = DateTime.Now.ToString("HH:mm:ss")
            };

            var rightControls = new FlowLayoutPanel
            {
                Dock          = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                WrapContents  = false
            };

            _themeButton = new Button
            {
                Text      = "Theme",
                AutoSize  = false,
                Width     = 90,
                Height    = 34,
                FlatStyle = FlatStyle.Flat,
                Margin    = new Padding(0, 15, 12, 0),
                Cursor    = Cursors.Hand
            };
            _themeButton.FlatAppearance.BorderSize = 1;

            _clearDoneButton = new Button
            {
                Text      = "Clear Done",
                AutoSize  = false,
                Width     = 100,
                Height    = 34,
                FlatStyle = FlatStyle.Flat,
                Margin    = new Padding(0, 15, 12, 0),
                Cursor    = Cursors.Hand
            };
            _clearDoneButton.FlatAppearance.BorderSize = 1;

            _addTaskButton = new PillButton
            {
                Text   = "+ Add Task",
                Width  = 130,
                Height = 34,
                Margin = new Padding(0, 15, 0, 0),
                Font   = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            rightControls.Controls.Add(_themeButton);
            rightControls.Controls.Add(_clearDoneButton);
            rightControls.Controls.Add(_addTaskButton);

            _topBar.Controls.Add(rightControls);
            _topBar.Controls.Add(_clockLabel);
            _topBar.Controls.Add(_appTitleLabel);

            _boardLayout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 4,
                RowCount    = 1,
                Padding     = new Padding(8, 4, 8, 8)
            };
            for (int i = 0; i < 4; i++)
                _boardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            _boardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _urgentColumn      = new ColumnPanel(BoardColumn.Urgent,      "Urgent");
            _prioritizedColumn = new ColumnPanel(BoardColumn.Prioritized, "Prioritized");
            _chillColumn       = new ColumnPanel(BoardColumn.Chill,       "Chill");
            _doneColumn        = new ColumnPanel(BoardColumn.Done,        "Done / Over");

            _boardLayout.Controls.Add(_urgentColumn,      0, 0);
            _boardLayout.Controls.Add(_prioritizedColumn, 1, 0);
            _boardLayout.Controls.Add(_chillColumn,       2, 0);
            _boardLayout.Controls.Add(_doneColumn,        3, 0);

            _allColumns = new[] { _urgentColumn, _prioritizedColumn, _chillColumn, _doneColumn };

            Controls.Add(_boardLayout);
            Controls.Add(_topBar);
        }

        private ColumnPanel PanelFor(BoardColumn column) => column switch
        {
            BoardColumn.Urgent      => _urgentColumn,
            BoardColumn.Prioritized => _prioritizedColumn,
            BoardColumn.Chill       => _chillColumn,
            BoardColumn.Done        => _doneColumn,
            _ => throw new ArgumentOutOfRangeException(nameof(column))
        };

        private class PillButton : Button
        {
            public PillButton()
            {
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (Width <= 0 || Height <= 0) return;
                using var path = ColorHelper.RoundedRectPath(
                    new Rectangle(0, 0, Width, Height), Height / 2);
                var old = Region;
                Region = new Region(path);
                old?.Dispose();
            }
        }
    }
}
