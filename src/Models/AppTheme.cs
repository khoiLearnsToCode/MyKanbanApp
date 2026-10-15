namespace KanbanDashboard.Models
{
    public class AppTheme
    {
        public string BackgroundHex { get; set; } = "#303446";
        public string PanelHex { get; set; } = "#292c3c";
        public string CardBackgroundHex { get; set; } = "#414559";
        public string TextHex { get; set; } = "#c6d0f5";

        public string UrgentColumnHex { get; set; } = "#e78284";
        public string PrioritizedColumnHex { get; set; } = "#ef9f76";
        public string ChillColumnHex { get; set; } = "#8caaee";
        public string DoneColumnHex { get; set; } = "#a6d189";

        public string DoneSuccessHex { get; set; } = "#a6d189";
        public string DoneLateHex { get; set; } = "#e78284";

        public static AppTheme CatppuccinFrappe() => new AppTheme();

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
