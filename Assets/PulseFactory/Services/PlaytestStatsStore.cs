using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PulseFactory.Persistence
{
    [DataContract]
    public sealed class PlaytestLevelStats
    {
        [DataMember(IsRequired = true)] public string LevelId;
        [DataMember(IsRequired = true)] public int Starts;
        [DataMember(IsRequired = true)] public int Completions;
        [DataMember(IsRequired = true)] public int Commands;
        [DataMember(IsRequired = true)] public int FailedAttempts;
        [DataMember(IsRequired = true)] public int UndoUses;
        [DataMember(IsRequired = true)] public int Restarts;
        [DataMember(IsRequired = true)] public double TotalCompletionSeconds;
    }

    [DataContract]
    public sealed class PlaytestStatsData
    {
        [DataMember(IsRequired = true)] public int SchemaVersion = 1;
        [DataMember(IsRequired = true)] public List<PlaytestLevelStats> Levels = new List<PlaytestLevelStats>();
    }

    public sealed class PlaytestStatsStore
    {
        private readonly string path;
        private readonly Action<string> report;
        public PlaytestStatsData Data { get; private set; }

        public PlaytestStatsStore(string path, Action<string> report)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            this.report = report ?? throw new ArgumentNullException(nameof(report));
            Data = Load();
        }

        public void RecordStart(string levelId)
        {
            Get(levelId).Starts++;
            Persist();
        }

        public void RecordCommand(string levelId, bool failed)
        {
            var stats = Get(levelId);
            stats.Commands++;
            if (failed) stats.FailedAttempts++;
            Persist();
        }

        public void RecordUndo(string levelId) { Get(levelId).UndoUses++; Persist(); }
        public void RecordRestart(string levelId) { Get(levelId).Restarts++; Persist(); }

        public void RecordCompletion(string levelId, double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            var stats = Get(levelId);
            stats.Completions++;
            stats.TotalCompletionSeconds += elapsedSeconds;
            Persist();
        }

        public string Export(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Export directory is required.", nameof(directory));
            string path = Path.Combine(directory, "pulse-factory-playtest-v1.json");
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(PlaytestStatsData)).WriteObject(stream, Data);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
                return path;
            }
            catch (Exception ex) when (IsStorageError(ex))
            {
                report("Playtest export could not be written: " + ex.Message);
                return null;
            }
            finally { RemoveTemporary(temporary); }
        }

        private PlaytestStatsData Load()
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var data = (PlaytestStatsData)new DataContractJsonSerializer(typeof(PlaytestStatsData)).ReadObject(stream);
                    Validate(data);
                    return data;
                }
            }
            catch (FileNotFoundException) { return new PlaytestStatsData(); }
            catch (DirectoryNotFoundException) { return new PlaytestStatsData(); }
            catch (Exception ex) when (IsStorageError(ex))
            {
                report("Playtest statistics could not be loaded; fresh in-memory statistics will be used. Original retained: " + ex.Message);
                return new PlaytestStatsData();
            }
        }

        private PlaytestLevelStats Get(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId)) throw new ArgumentException("Level ID is required.", nameof(levelId));
            var stats = Data.Levels.SingleOrDefault(item => item.LevelId == levelId);
            if (stats != null) return stats;
            stats = new PlaytestLevelStats { LevelId = levelId };
            Data.Levels.Add(stats);
            return stats;
        }

        private void Persist()
        {
            Validate(Data);
            string temporary = path + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(path));
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(PlaytestStatsData)).WriteObject(stream, Data);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            catch (Exception ex) when (IsStorageError(ex))
            {
                report("Playtest statistics could not be saved: " + ex.Message);
            }
            finally { RemoveTemporary(temporary); }
        }

        private void RemoveTemporary(string temporary)
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { report("Playtest temporary file cleanup failed: " + ex.Message); }
        }

        private static bool IsStorageError(Exception ex) =>
            ex is IOException || ex is UnauthorizedAccessException || ex is SerializationException
            || ex is System.Xml.XmlException || ex is FormatException;

        private static void Validate(PlaytestStatsData data)
        {
            if (data == null || data.SchemaVersion != 1 || data.Levels == null
                || data.Levels.Any(level => level == null || string.IsNullOrWhiteSpace(level.LevelId)
                    || level.Starts < 0 || level.Completions < 0 || level.Commands < 0 || level.FailedAttempts < 0
                    || level.UndoUses < 0 || level.Restarts < 0 || double.IsNaN(level.TotalCompletionSeconds)
                    || double.IsInfinity(level.TotalCompletionSeconds) || level.TotalCompletionSeconds < 0)
                || data.Levels.Select(level => level.LevelId).Distinct(StringComparer.Ordinal).Count() != data.Levels.Count)
                throw new SerializationException("Invalid or incompatible playtest statistics.");
        }
    }
}
