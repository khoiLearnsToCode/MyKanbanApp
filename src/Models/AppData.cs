using System.Collections.Generic;

namespace KanbanDashboard.Models
{
    public class AppData
    {
        public List<TaskItem> Tasks { get; set; } = new();

        public AppTheme Theme { get; set; } = AppTheme.CatppuccinFrappe();
    }
}
