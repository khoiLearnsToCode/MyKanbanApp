using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using KanbanDashboard.Services;

namespace KanbanDashboard.UI
{
    /// <summary>
    /// iOS-style "date and time" wheel picker (day / hour / minute / AM-PM), painted as a
    /// single control. Rows are projected onto a cylinder so they shrink and fade toward the
    /// edges. It only repaints while being interacted with or settling, so it is idle otherwise.
    /// </summary>
    public class WheelDateTimePicker : Control
    {
        private const int    RowHeight   = 32;                  // row spacing at the centre
        private const double StepRadians = 22 * Math.PI / 180;  // angle between rows on the cylinder
        private const int    DateRange   = 365;                 // days shown either side of the anchor
        private const float  Easing      = 0.22f;               // fraction of distance covered per frame
        private const double FlickThreshold = 0.5;              // px/ms; slower releases get no momentum

        private static readonly double Radius = RowHeight / Math.Sin(StepRadians);
        private static readonly Font   WheelFont = new("Segoe UI", 15f);
        private static readonly StringFormat RightFormat  = Format(StringAlignment.Far);
        private static readonly StringFormat CenterFormat = Format(StringAlignment.Center);
        private static readonly StringFormat LeftFormat   = Format(StringAlignment.Near);

        private readonly Wheel _dateWheel, _hourWheel, _minuteWheel, _ampmWheel;
        private readonly Wheel[] _wheels;
        private readonly System.Windows.Forms.Timer _animation = new() { Interval = 15 };

        private DateTime _minDate;

        // Day labels are long; at higher display scaling the 15pt font no longer fits the day
        // column, so a smaller size is chosen to fit (recomputed when size or DPI changes).
        private Font _dateFont;

        private Wheel _active;              // wheel being dragged / receiving arrow keys
        private int   _dragStartY;
        private double _dragStartOffset;
        private bool  _dragMoved;
        private readonly List<(long Time, int Y)> _dragSamples = new();

        public WheelDateTimePicker()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
            Height  = 176;

            _dateWheel   = new Wheel(DateRange * 2 + 1, cyclic: false, DateLabel,               RightFormat);
            _hourWheel   = new Wheel(12,                cyclic: true,  i => (i + 1).ToString(), RightFormat);
            _minuteWheel = new Wheel(60,                cyclic: true,  i => i.ToString("00"),   CenterFormat);
            _ampmWheel   = new Wheel(2,                 cyclic: false, i => i == 0 ? "AM" : "PM", LeftFormat);
            _wheels = new[] { _dateWheel, _hourWheel, _minuteWheel, _ampmWheel };
            _active = _hourWheel;

            _animation.Tick += (_, _) => Animate();
            Value = DateTime.Now;
        }

        /// <summary>The selected date and time (seconds are always zero).</summary>
        public DateTime Value
        {
            get
            {
                int hour12 = _hourWheel.SelectedIndex + 1;
                int hour24 = hour12 % 12 + (_ampmWheel.SelectedIndex == 1 ? 12 : 0);
                return _minDate.AddDays(_dateWheel.SelectedIndex)
                               .AddHours(hour24)
                               .AddMinutes(_minuteWheel.SelectedIndex);
            }
            set
            {
                // Re-anchor the day wheel so the value always sits in the middle of its range.
                _minDate = value.Date.AddDays(-DateRange);
                _dateWheel.JumpTo(DateRange);
                _hourWheel.JumpTo((value.Hour + 11) % 12);
                _minuteWheel.JumpTo(value.Minute);
                _ampmWheel.JumpTo(value.Hour >= 12 ? 1 : 0);
                Invalidate();
            }
        }

        private string DateLabel(int index)
        {
            var date  = _minDate.AddDays(index);
            var today = DateTime.Today;
            if (date == today)
                return "Today";
            return date.ToString(date.Year == today.Year ? "ddd MMM d" : "MMM d, yyyy",
                                 CultureInfo.InvariantCulture);
        }

        // ---- layout -------------------------------------------------------------------------

        private RectangleF ColumnBounds(Wheel wheel)
        {
            // Proportions follow the iOS picker: wide day column, narrow time columns.
            float w = Width;
            if (wheel == _dateWheel)   return new RectangleF(w * 0.00f, 0, w * 0.40f, Height);
            if (wheel == _hourWheel)   return new RectangleF(w * 0.40f, 0, w * 0.17f, Height);
            if (wheel == _minuteWheel) return new RectangleF(w * 0.59f, 0, w * 0.17f, Height);
            return new RectangleF(w * 0.79f, 0, w * 0.21f, Height);
        }

        private Wheel WheelAt(int x)
        {
            foreach (var wheel in _wheels)
            {
                var b = ColumnBounds(wheel);
                if (x >= b.Left && x < b.Right + 8)
                    return wheel;
            }
            return _ampmWheel;
        }

        private Rectangle SelectionBand =>
            new(4, Height / 2 - RowHeight / 2 - 2, Width - 8, RowHeight + 4);

        // ---- painting -----------------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            var band = SelectionBand;
            using (var bandPath  = ColorHelper.RoundedRectPath(band, 9))
            using (var bandBrush = new SolidBrush(ColorHelper.Blend(BackColor, ForeColor, Enabled ? 0.12f : 0.06f)))
                g.FillPath(bandBrush, bandPath);

            // iOS look: rows outside the band are grey, and whatever part of a row lies inside
            // the band is drawn at full strength. Two passes with a clip achieve exactly that.
            var dim    = ColorHelper.Blend(ForeColor, BackColor, Enabled ? 0.55f : 0.75f);
            var bright = Enabled ? ForeColor : ColorHelper.Blend(ForeColor, BackColor, 0.5f);

            foreach (var wheel in _wheels)
                DrawWheel(g, wheel, dim, fadeWithDepth: true);

            g.SetClip(band);
            foreach (var wheel in _wheels)
                DrawWheel(g, wheel, bright, fadeWithDepth: false);
            g.ResetClip();

            if (Focused && ShowFocusCues)
            {
                var b = ColumnBounds(_active);
                using var pen = new Pen(ColorHelper.Blend(BackColor, ForeColor, 0.35f), 1.5f);
                g.DrawLine(pen, b.Left + 6, band.Bottom + 1, b.Right - 6, band.Bottom + 1);
            }
        }

        private Font DateFont(Graphics g, float columnWidth)
        {
            if (_dateFont != null)
                return _dateFont;

            // Widest labels the day wheel can show, for this year and for other years.
            var samples = new[] { "Wed Sep 28", "Wed Nov 28", "Sep 28, 2028", "Nov 28, 2028" };
            for (float size = WheelFont.Size; size > 8f; size -= 0.5f)
            {
                var candidate = size == WheelFont.Size ? WheelFont : new Font(WheelFont.FontFamily, size);
                bool fits = samples.All(t => g.MeasureString(t, candidate).Width <= columnWidth - 6);
                if (fits || size - 0.5f <= 8f)
                    return _dateFont = candidate;
                if (candidate != WheelFont)  // the shared font must never be disposed
                    candidate.Dispose();
            }
            return _dateFont = WheelFont;
        }

        private void ResetDateFont()
        {
            if (_dateFont != null && _dateFont != WheelFont)
                _dateFont.Dispose();
            _dateFont = null;
        }

        protected override void OnResize(EventArgs e)
        {
            ResetDateFont();
            base.OnResize(e);
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            ResetDateFont();
            base.OnDpiChangedAfterParent(e);
        }

        private void DrawWheel(Graphics g, Wheel wheel, Color color, bool fadeWithDepth)
        {
            var bounds  = ColumnBounds(wheel);
            var font    = wheel == _dateWheel ? DateFont(g, bounds.Width) : WheelFont;
            float midY  = Height / 2f;
            int   first = (int)Math.Floor(wheel.Offset) - 4;

            for (int i = first; i <= first + 9; i++)
            {
                if (!wheel.Cyclic && (i < 0 || i >= wheel.Count))
                    continue;

                double angle = (i - wheel.Offset) * StepRadians;
                if (Math.Abs(angle) >= Math.PI / 2 * 0.97)
                    continue;

                float depth = (float)Math.Cos(angle);  // 1 at the centre, ->0 at the edges
                float y     = midY + (float)(Radius * Math.Sin(angle));

                var c = fadeWithDepth ? ColorHelper.Blend(BackColor, color, depth * depth) : color;
                using var brush = new SolidBrush(c);

                var state = g.Save();
                g.TranslateTransform(bounds.X, y);
                g.ScaleTransform(1f, depth);
                g.DrawString(wheel.Label(wheel.Wrap(i)), font, brush,
                             new RectangleF(0, -RowHeight / 2f, bounds.Width, RowHeight), wheel.Format);
                g.Restore(state);
            }
        }

        // ---- input --------------------------------------------------------------------------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            Focus();

            _active          = WheelAt(e.X);
            _dragStartY      = e.Y;
            _dragStartOffset = _active.Offset;
            _dragMoved       = false;
            _dragSamples.Clear();
            _dragSamples.Add((Environment.TickCount64, e.Y));
            _active.Target   = _active.Offset;  // stop any animation on this wheel
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!Capture || e.Button != MouseButtons.Left) return;

            int dy = e.Y - _dragStartY;
            if (Math.Abs(dy) > 3) _dragMoved = true;
            if (!_dragMoved) return;

            _active.Offset = _active.Target = _active.ClampSoft(_dragStartOffset - (double)dy / RowHeight);

            long now = Environment.TickCount64;
            _dragSamples.Add((now, e.Y));
            _dragSamples.RemoveAll(s => now - s.Time > 100);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!Capture) return;
            Capture = false;

            if (!_dragMoved)
            {
                // Click: spin the tapped row into the band.
                double sin = Math.Clamp((e.Y - Height / 2.0) / Radius, -1, 1);
                double rows = Math.Asin(sin) / StepRadians;
                AnimateTo(_active, Math.Round(_active.Offset + rows));
                return;
            }

            // Release: a fast flick keeps its momentum; a slow drag lands where it was let go.
            double velocity = 0; // rows per millisecond
            long now = Environment.TickCount64;
            _dragSamples.RemoveAll(s => now - s.Time > 100);
            if (_dragSamples.Count >= 2)
            {
                var (t0, y0) = _dragSamples[0];
                var (t1, y1) = _dragSamples[^1];
                double pxPerMs = t1 > t0 ? (double)(y1 - y0) / (t1 - t0) : 0;
                if (Math.Abs(pxPerMs) > FlickThreshold)
                    velocity = -pxPerMs / RowHeight;
            }
            AnimateTo(_active, Math.Round(_active.Offset + velocity * 180));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!Enabled) return;
            _active = WheelAt(e.X);
            AnimateTo(_active, Math.Round(_active.Target) - Math.Sign(e.Delta) * Math.Max(1, Math.Abs(e.Delta) / 120));
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;

            int index = Array.IndexOf(_wheels, _active);
            switch (e.KeyCode)
            {
                case Keys.Up:    AnimateTo(_active, Math.Round(_active.Target) - 1); break;
                case Keys.Down:  AnimateTo(_active, Math.Round(_active.Target) + 1); break;
                case Keys.Left:  _active = _wheels[Math.Max(0, index - 1)];                 Invalidate(); break;
                case Keys.Right: _active = _wheels[Math.Min(_wheels.Length - 1, index + 1)]; Invalidate(); break;
                default: return;
            }
            e.Handled = true;
        }

        protected override void OnGotFocus(EventArgs e)     { base.OnGotFocus(e);     Invalidate(); }
        protected override void OnLostFocus(EventArgs e)    { base.OnLostFocus(e);    Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e){ base.OnEnabledChanged(e); Invalidate(); }

        // ---- animation ----------------------------------------------------------------------

        private void AnimateTo(Wheel wheel, double target)
        {
            wheel.Target = wheel.ClampHard(target);
            _animation.Start();
        }

        private void Animate()
        {
            bool moving = false;
            foreach (var wheel in _wheels)
            {
                if (Capture && wheel == _active) continue;

                double diff = wheel.Target - wheel.Offset;
                if (Math.Abs(diff) < 0.004)
                {
                    wheel.Offset = wheel.Target;
                    wheel.Normalize();
                }
                else
                {
                    wheel.Offset += diff * Easing;
                    moving = true;
                }
            }

            Invalidate();
            if (!moving)
                _animation.Stop();  // idle: no timer, no repaints
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animation.Dispose();
                ResetDateFont();
            }
            base.Dispose(disposing);
        }

        private static StringFormat Format(StringAlignment alignment) => new()
        {
            Alignment     = alignment,
            LineAlignment = StringAlignment.Center,
            FormatFlags   = StringFormatFlags.NoWrap
        };

        // ---- one wheel's state ----------------------------------------------------------------

        private sealed class Wheel
        {
            public readonly int Count;
            public readonly bool Cyclic;
            public readonly Func<int, string> Label;
            public readonly StringFormat Format;

            public double Offset;  // fractional row currently at the centre
            public double Target;  // whole row the wheel is settling on

            public Wheel(int count, bool cyclic, Func<int, string> label, StringFormat format)
            {
                Count  = count;
                Cyclic = cyclic;
                Label  = label;
                Format = format;
            }

            public int SelectedIndex => Wrap((int)Math.Round(Target));

            public int Wrap(int i) => Cyclic ? ((i % Count) + Count) % Count : Math.Clamp(i, 0, Count - 1);

            public void JumpTo(int index) => Offset = Target = index;

            public double ClampHard(double v) => Cyclic ? v : Math.Clamp(v, 0, Count - 1);

            // While dragging past either end, allow a little rubber-band overscroll.
            public double ClampSoft(double v) => Cyclic ? v : Math.Clamp(v, -0.4, Count - 1 + 0.4);

            // Keep cyclic wheels' numbers small once settled.
            public void Normalize()
            {
                if (!Cyclic) return;
                double shift = Math.Floor(Target / Count) * Count;
                Offset -= shift;
                Target -= shift;
            }
        }
    }
}
