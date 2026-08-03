namespace KanbanDashboard.Models
{
    /// <summary>
    /// The four columns of the board, left to right.
    /// Numeric value doubles as an "urgency rank" - lower is more urgent.
    /// </summary>
    public enum BoardColumn
    {
        Urgent = 0,
        Prioritized = 1,
        Chill = 2,
        Done = 3
    }
}
