using System;
using System.IO;
using System.Text.Json;
using KanbanDashboard.Models;

namespace KanbanDashboard.Services
{
    public class DataStore
    {
        private readonly string _filePath;
        private readonly string _tempPath;
        private readonly string _backupPath;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// Set by <see cref="Load"/> when the board file could not be read and was set aside
        /// or recovered from backup; null when loading went normally.
        /// </summary>
        public string LoadWarning { get; private set; }

        public DataStore()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KanbanDashboard");

            Directory.CreateDirectory(folder);
            _filePath   = Path.Combine(folder, "board.json");
            _tempPath   = _filePath + ".tmp";
            _backupPath = _filePath + ".bak";
        }

        public AppData Load()
        {
            LoadWarning = null;

            if (!File.Exists(_filePath))
            {
                // Only the backup exists if a previous save was interrupted mid-swap.
                if (TryRead(_backupPath, out var fromBackup))
                {
                    LoadWarning = "The board file was missing, so it was restored from the last backup.";
                    return fromBackup;
                }
                return new AppData();
            }

            if (TryRead(_filePath, out var data))
                return data;

            // Never let a later save overwrite the unreadable file: move it out of the way first.
            var corruptPath = SetAsideCorruptFile();

            if (TryRead(_backupPath, out var recovered))
            {
                LoadWarning = "The board file could not be read, so the last backup was restored." +
                              CorruptFileNote(corruptPath);
                return recovered;
            }

            LoadWarning = "The board file could not be read and no usable backup was found, " +
                          "so an empty board was started." + CorruptFileNote(corruptPath);
            return new AppData();
        }

        /// <summary>
        /// Writes the board to a temp file and swaps it in, keeping the previous version as a
        /// backup, so a crash or power loss mid-save can never leave a half-written board.json.
        /// </summary>
        public bool Save(AppData data, out string error)
        {
            try
            {
                var json = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);

                using (var stream = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(json);
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(_filePath))
                    File.Replace(_tempPath, _filePath, _backupPath, ignoreMetadataErrors: true);
                else
                    File.Move(_tempPath, _filePath);

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryRead(string path, out AppData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path))
                    return false;

                data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(path), JsonOptions);
                if (data == null)
                    return false;

                data.Tasks ??= new();
                data.Theme ??= AppTheme.CatppuccinFrappe();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string SetAsideCorruptFile()
        {
            try
            {
                var target = Path.Combine(
                    Path.GetDirectoryName(_filePath),
                    $"board.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.Move(_filePath, target);
                return target;
            }
            catch
            {
                return null;
            }
        }

        private static string CorruptFileNote(string corruptPath) =>
            corruptPath == null ? "" : $"\n\nThe unreadable file was kept at:\n{corruptPath}";
    }
}
