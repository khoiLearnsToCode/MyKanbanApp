using System;
using System.Collections.Generic;
using KanbanDashboard.Models;

namespace KanbanDashboard.Services
{
    public static class DeadlineService
    {
        public static readonly TimeSpan PrioritizedThreshold = TimeSpan.FromHours(72);
        public static readonly TimeSpan UrgentThreshold = TimeSpan.FromHours(24);

        /// <summary>
        /// False if <see cref="ApplyRules"/> would immediately pull the task out of
        /// <paramref name="column"/> because its deadline is too close.
        /// </summary>
        public static bool CanPlace(TaskItem task, BoardColumn column, DateTime now)
        {
            if (column == BoardColumn.Done || !task.DueAt.HasValue)
                return true;

            var remaining = task.DueAt.Value - now;
            if (remaining <= UrgentThreshold)
                return column == BoardColumn.Urgent;
            if (remaining <= PrioritizedThreshold)
                return column != BoardColumn.Chill;
            return true;
        }

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
