using System.Collections.Generic;

namespace KanbanDashboard.Models
{
    /// <summary>
    /// The full persisted state of the app: every task plus the active theme.
    /// This whole object is serialized to a single JSON file.
    /// </summary>
    public class AppData
    {
        public List<TaskItem> Tasks { get; set; } = new();

        public AppTheme Theme { get; set; } = AppTheme.CatppuccinFrappe();
    }
}
