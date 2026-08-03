using KanbanDashboard.UI;

namespace KanbanDashboard
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Global crash guard: even an unhandled UI exception shouldn't lose the user's
            // board, and we don't want a crash dialog storm burning CPU.
            Application.ThreadException += (_, e) =>
                MessageBox.Show("An unexpected error occurred:\n" + e.Exception.Message,
                    "My Kanban Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            Application.Run(new MainForm());
        }
    }
}
