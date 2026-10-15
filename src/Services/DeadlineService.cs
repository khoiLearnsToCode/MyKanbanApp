using System;
using System.Collections.Generic;
using KanbanDashboard.Models;

namespace KanbanDashboard.Services
{
    public static class DeadlineService
    {
        public static readonly TimeSpan PrioritizedThreshold = TimeSpan.FromHours(72);
        public static readonly TimeSpan UrgentThreshold = TimeSpan.FromHours(24);

        public static bool ApplyRules(IEnumerable<TaskItem> tasks, DateTime now)
        {
            bool changed = false;

            foreach (var task in tasks)
            {
                if (task.IsCompleted || task.Column == BoardColumn.Done)
                    continue;
                if (!task.DueAt.HasValue)
                    continue;

                var remaining = task.DueAt.Value - now;

                BoardColumn target;
                if (remaining <= UrgentThreshold)
                    target = BoardColumn.Urgent;
                else if (remaining <= PrioritizedThreshold)
                    target = BoardColumn.Prioritized;
                else
                    continue;

                if ((int)target < (int)task.Column)
                {
                    task.Column = target;
                    changed = true;
                }
            }

            return changed;
        }
    }
}
