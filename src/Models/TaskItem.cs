using System;

namespace KanbanDashboard.Models
{
    public class TaskItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? DueAt { get; set; }

        public BoardColumn Column { get; set; } = BoardColumn.Chill;

        public bool IsCompleted { get; set; }

        public DateTime? CompletedAt { get; set; }

        public bool IsOverdue => DueAt.HasValue && !IsCompleted && DateTime.Now > DueAt.Value;

        public bool WasCompletedLate =>
            IsCompleted && DueAt.HasValue && CompletedAt.HasValue && CompletedAt.Value > DueAt.Value;

        public TimeSpan? TimeRemaining => DueAt.HasValue ? DueAt.Value - DateTime.Now : null;
    }
}
