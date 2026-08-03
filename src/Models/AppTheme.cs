namespace KanbanDashboard.Models
{
    /// <summary>
    /// All user-customizable colors, stored as hex strings ("#RRGGBB") so they
    /// serialize cleanly to JSON and round-trip through the theme editor's textboxes.
    /// Defaults to Catppuccin Frappe.
    /// </summary>
    public class AppTheme
    {
        public string BackgroundHex { get; set; } = "#303446";   // Base
        public string PanelHex { get; set; } = "#292c3c";        // Mantle
        public string CardBackgroundHex { get; set; } = "#414559"; // Surface0
        public string TextHex { get; set; } = "#c6d0f5";         // Text

        public string UrgentColumnHex { get; set; } = "#e78284";       // Red
        public string PrioritizedColumnHex { get; set; } = "#ef9f76";  // Peach
        public string ChillColumnHex { get; set; } = "#8caaee";        // Blue
        public string DoneColumnHex { get; set; } = "#a6d189";         // Green

        public string DoneSuccessHex { get; set; } = "#a6d189"; // mild green vibe (on-time)
        public string DoneLateHex { get; set; } = "#e78284";    // mild red vibe (past deadline)

        public static AppTheme CatppuccinFrappe() => new AppTheme();

        /// <summary>A light, airy pastel theme (mint/white) in the spirit of the reference mockup.</summary>
        public static AppTheme SoftDaybreak() => new AppTheme
        {
            BackgroundHex = "#EAF3EF",
            PanelHex = "#FFFFFF",
            CardBackgroundHex = "#FFFFFF",
            TextHex = "#2C3E3A",
            UrgentColumnHex = "#F08A8A",
            PrioritizedColumnHex = "#F5B267",
            ChillColumnHex = "#7FB2E5",
            DoneColumnHex = "#7FC79E",
            DoneSuccessHex = "#7FC79E",
            DoneLateHex = "#F08A8A"
        };

        public AppTheme Clone() => new AppTheme
        {
            BackgroundHex = BackgroundHex,
            PanelHex = PanelHex,
            CardBackgroundHex = CardBackgroundHex,
            TextHex = TextHex,
            UrgentColumnHex = UrgentColumnHex,
            PrioritizedColumnHex = PrioritizedColumnHex,
            ChillColumnHex = ChillColumnHex,
            DoneColumnHex = DoneColumnHex,
            DoneSuccessHex = DoneSuccessHex,
            DoneLateHex = DoneLateHex
        };
    }
}
