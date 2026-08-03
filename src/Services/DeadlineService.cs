using System;
using System.Collections.Generic;
using KanbanDashboard.Models;

namespace KanbanDashboard.Services
{
    /// <summary>
    /// Contains the timing rules for auto-moving tasks toward Urgent as their
    /// deadline approaches:
    ///   - more than 72h left  -> left where the user put it (no change)
    ///   - 72h or less left    -> at least "Prioritized"
    ///   - 24h or less left    -> "Urgent"
    /// This is upgrade-only: a task is never auto-downgraded (e.g. moved back to
    /// Chill), and Done tasks and tasks with no deadline are never touched.
    /// Kept as pure logic (no Timer/UI here) so it's easy to reason about and reuse
    /// both at startup ("sync with computer") and on every periodic tick.
    /// </summary>
    public static class DeadlineService
    {
        public static readonly TimeSpan PrioritizedThreshold = TimeSpan.FromHours(72);
        public static readonly TimeSpan UrgentThreshold = TimeSpan.FromHours(24);

        /// <summary>
        /// Applies the escalation rules to every task in place. Returns true if
        /// anything changed (so the caller knows whether to re-render/save).
        /// </summary>
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
                    continue; // more than 72h left: don't touch the column

                // Only move "forward" (toward more urgent). Enum values are ranked
                // Urgent(0) < Prioritized(1) < Chill(2), so a smaller number wins.
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
