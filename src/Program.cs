using System.Diagnostics;
using System.Runtime.InteropServices;
using KanbanDashboard.UI;

namespace KanbanDashboard
{
    internal static class Program
    {
        // "Local\" scopes the mutex to the current Windows user session.
        private const string SingleInstanceMutexName = @"Local\KanbanDashboard.SingleInstance";

        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [STAThread]
        private static void Main()
        {
            using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                ActivateExistingInstance();
                return;
            }

            ApplicationConfiguration.Initialize();

            Application.ThreadException += (_, e) =>
                MessageBox.Show("An unexpected error occurred:\n" + e.Exception.Message,
                    "My Kanban Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            Application.Run(new MainForm());
        }

        private static void ActivateExistingInstance()
        {
            using var current = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                using (process)
                {
                    if (process.Id == current.Id)
                        continue;

                    var handle = process.MainWindowHandle;
                    if (handle == IntPtr.Zero)
                        continue;

                    if (IsIconic(handle))
                        ShowWindow(handle, SW_RESTORE);
                    SetForegroundWindow(handle);
                    return;
                }
            }
        }
    }
}
