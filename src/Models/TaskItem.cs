using System;

namespace KanbanDashboard.Models
{
    /// <summary>
    /// A single task/card on the board.
    /// </summary>
    public class TaskItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Optional deadline. If null, the task never gets auto-escalated by
        /// <see cref="Services.DeadlineService"/> and the user manages its column manually.
        /// </summary>
        public DateTime? DueAt { get; set; }

        public BoardColumn Column { get; set; } = BoardColumn.Chill;

        public bool IsCompleted { get; set; }

        public DateTime? CompletedAt { get; set; }

        /// <summary>True if the deadline has passed and the task has not been checked off yet.</summary>
        public bool IsOverdue => DueAt.HasValue && !IsCompleted && DateTime.Now > DueAt.Value;

        /// <summary>True if the task was marked done, but after its deadline had already passed.</summary>
        public bool WasCompletedLate =>
            IsCompleted && DueAt.HasValue && CompletedAt.HasValue && CompletedAt.Value > DueAt.Value;

        public TimeSpan? TimeRemaining => DueAt.HasValue ? DueAt.Value - DateTime.Now : null;
    }
}
