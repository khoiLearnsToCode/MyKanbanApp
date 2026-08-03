using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace KanbanDashboard.Services
{
    /// <summary>
    /// Small helper to safely convert between hex strings (as typed by the user
    /// in the theme dialog) and System.Drawing.Color.
    /// </summary>
    public static class ColorHelper
    {
        /// <summary>
        /// Accepts "#RRGGBB", "RRGGBB", "#RGB" or "RGB". Returns false (and leaves
        /// <paramref name="color"/> as Color.Empty) if the text isn't a valid hex color.
        /// </summary>
        public static bool TryParseHex(string text, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var s = text.Trim();
            if (s.StartsWith("#"))
                s = s[1..];

            // Expand shorthand "RGB" -> "RRGGBB"
            if (s.Length == 3)
            {
                var chars = s.ToCharArray();
                s = $"{chars[0]}{chars[0]}{chars[1]}{chars[1]}{chars[2]}{chars[2]}";
            }

            if (s.Length != 6)
                return false;

            if (!byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r))
                return false;
            if (!byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g))
                return false;
            if (!byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return false;

            color = Color.FromArgb(255, r, g, b);
            return true;
        }

        /// <summary>Falls back to <paramref name="fallback"/> if the hex string is invalid.</summary>
        public static Color ParseHexOrDefault(string text, Color fallback) =>
            TryParseHex(text, out var c) ? c : fallback;

        public static string ToHex(Color color) =>
            $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        /// <summary>Blends a color toward white to create a "mild" pastel tint, e.g. for the Done column vibes.</summary>
        public static Color Soften(Color color, float amount = 0.55f)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            int r = (int)(color.R + (255 - color.R) * amount);
            int g = (int)(color.G + (255 - color.G) * amount);
            int b = (int)(color.B + (255 - color.B) * amount);
            return Color.FromArgb(255, r, g, b);
        }

        /// <summary>Darkens/deepens a color slightly - used for text sitting on a pastel pill background.</summary>
        public static Color Deepen(Color color, float amount = 0.35f)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            int r = (int)(color.R * (1 - amount));
            int g = (int)(color.G * (1 - amount));
            int b = (int)(color.B * (1 - amount));
            return Color.FromArgb(255, r, g, b);
        }

        /// <summary>Builds a rounded-rectangle path, shared by cards/pills/buttons that want soft corners.</summary>
        public static GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X,           bounds.Y,            d, d, 180, 90);
            path.AddArc(bounds.Right - d,   bounds.Y,            d, d, 270, 90);
            path.AddArc(bounds.Right - d,   bounds.Bottom - d,   d, d,   0, 90);
            path.AddArc(bounds.X,           bounds.Bottom - d,   d, d,  90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Returns near-black or near-white, whichever gives the higher contrast against
        /// <paramref name="bg"/>, using the WCAG relative-luminance formula.
        /// </summary>
        public static Color ContrastColor(Color bg)
        {
            double luminance = 0.2126 * (bg.R / 255.0)
                             + 0.7152 * (bg.G / 255.0)
                             + 0.0722 * (bg.B / 255.0);
            return luminance > 0.45
                ? Color.FromArgb(30, 30, 30)
                : Color.FromArgb(230, 230, 230);
        }

        /// <summary>Linear colour blend. t=0 returns <paramref name="from"/>; t=1 returns <paramref name="to"/>.</summary>
        public static Color Blend(Color from, Color to, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(from.R + (to.R - from.R) * t),
                (int)(from.G + (to.G - from.G) * t),
                (int)(from.B + (to.B - from.B) * t));
        }
    }
}
