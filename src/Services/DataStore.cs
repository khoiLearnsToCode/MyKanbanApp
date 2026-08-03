using System;
using System.IO;
using System.Text.Json;
using KanbanDashboard.Models;

namespace KanbanDashboard.Services
{
    /// <summary>
    /// Loads/saves the whole board (tasks + theme) as a single small JSON file under
    /// the current user's AppData folder, so the exe itself stays read-only/portable.
    /// </summary>
    public class DataStore
    {
        private readonly string _filePath;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public DataStore()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KanbanDashboard");

            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "board.json");
        }

        public AppData Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new AppData();

                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
                return data ?? new AppData();
            }
            catch
            {
                // Corrupt or unreadable file: don't crash the app, just start fresh.
                // (We intentionally don't overwrite the bad file here, in case the
                // user wants to recover it manually.)
                return new AppData();
            }
        }

        public void Save(AppData data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data, JsonOptions);
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // Best-effort persistence; a failed save shouldn't crash the UI.
            }
        }
    }
}
