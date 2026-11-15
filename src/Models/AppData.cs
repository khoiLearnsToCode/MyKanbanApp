using System.Collections.Generic;

namespace KanbanDashboard.Models
{
    public class AppData
    {
        public List<TaskItem> Tasks { get; set; } = new();

        public AppTheme Theme { get; set; } = AppTheme.CatppuccinFrappe();

        /// <summary>Remembers whether the calendar (instead of the board) was last shown.</summary>
        public bool ShowCalendar { get; set; }
    }
}
