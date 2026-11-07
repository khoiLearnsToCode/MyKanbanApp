using System;
using System.Drawing;
using System.Windows.Forms;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    public partial class MainForm
    {
        private TaskCardControl _dragCard;
        private DragGhost       _dragGhost;
        private ColumnPanel     _dropTarget;
        private Point           _dragGrabOffset;

        private void OnCardDragStarted(TaskCardControl card)
        {
            _dragCard = card;

            var cursor = Cursor.Position;
            var origin = card.PointToScreen(Point.Empty);
            _dragGrabOffset = new Point(cursor.X - origin.X, cursor.Y - origin.Y);

            _dragGhost = new DragGhost(card) { Location = origin };
            _dragGhost.Show(this);
        }

        private void OnCardDragMoved()
        {
            var cursor = Cursor.Position;
            _dragGhost.Location = new Point(cursor.X - _dragGrabOffset.X, cursor.Y - _dragGrabOffset.Y);

            var task    = _dragCard.Task;
            ColumnPanel target = null;
            bool blocked = false;

            foreach (var col in _allColumns)
            {
                if (col.Column == task.Column || !col.RectangleToScreen(col.ClientRectangle).Contains(cursor))
                    continue;

                if (DeadlineService.CanPlace(task, col.Column, DateTime.Now))
                    target = col;
                else
                    blocked = true;
            }

            SetDropTarget(target);
            Cursor.Current = blocked ? Cursors.No : Cursors.Hand;
        }

        private void OnCardDragEnded(TaskCardControl card, bool dropped)
        {
            var target = _dropTarget;

            SetDropTarget(null);
            _dragGhost?.Dispose();
            _dragGhost = null;
            _dragCard  = null;
            Cursor.Current = Cursors.Default;

            // Posted because moving the task re-parents this card, which is still handling
            // its mouse-up.
            if (dropped && target != null)
                BeginInvoke(new Action(() => MoveTask(card.Task, target.Column)));
        }

        private void SetDropTarget(ColumnPanel target)
        {
            if (target == _dropTarget)
                return;

            _dropTarget?.SetDropHighlight(false);
            _dropTarget = target;
            _dropTarget?.SetDropHighlight(true);
        }

        /// <summary>
        /// Translucent copy of a card that follows the cursor. It exists only during a drag, never
        /// takes focus, and is click-through so the card being dragged keeps the mouse capture.
        /// </summary>
        private sealed class DragGhost : Form
        {
            private const int WS_EX_TOPMOST     = 0x00000008;
            private const int WS_EX_TRANSPARENT = 0x00000020;
            private const int WS_EX_TOOLWINDOW  = 0x00000080;
            private const int WS_EX_NOACTIVATE  = 0x08000000;

            private readonly Bitmap _image;
            private readonly Region _shape;

            public DragGhost(Control card)
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar   = false;
                StartPosition   = FormStartPosition.Manual;
                Opacity         = 0.85;
                Size            = card.Size;

                _image = new Bitmap(card.Width, card.Height);
                card.DrawToBitmap(_image, new Rectangle(Point.Empty, card.Size));

                using var path = ColorHelper.RoundedRectPath(
                    new Rectangle(Point.Empty, card.Size), TaskCardControl.CornerRadius);
                _shape = new Region(path);
                Region = _shape;
            }

            protected override bool ShowWithoutActivation => true;

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                    return cp;
                }
            }

            protected override void OnPaintBackground(PaintEventArgs e) =>
                e.Graphics.DrawImageUnscaled(_image, 0, 0);

            protected override void Dispose(bool disposing)
            {
                // Destroy the window first: it can still repaint until then, and painting needs
                // the image.
                base.Dispose(disposing);

                if (disposing)
                {
                    _image.Dispose();
                    _shape.Dispose();
                }
            }
        }
    }
}
