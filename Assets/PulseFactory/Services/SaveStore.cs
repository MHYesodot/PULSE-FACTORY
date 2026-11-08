using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace PulseFactory.Persistence
{
    [DataContract]
    public sealed class LevelProgress
    {
        [DataMember(IsRequired = true)] public string LevelId;
        [DataMember(IsRequired = true)] public int Stars;
    }

    [DataContract]
    public sealed class SaveData
    {
        [DataMember(IsRequired = true)] public int SchemaVersion = 1;
        [DataMember(IsRequired = true)] public int HighestUnlockedLevel = 1;
        [DataMember(IsRequired = true)] public List<LevelProgress> BestResults = new List<LevelProgress>();
        [DataMember(IsRequired = true)] public string Locale = "en";
        [DataMember(IsRequired = true)] public bool ReducedMotion;

        public void RecordVictory(string levelId, int levelNumber, int stars)
        {
            if (string.IsNullOrWhiteSpace(levelId) || levelNumber < 1 || levelNumber == int.MaxValue || stars < 1 || stars > 3)
                throw new ArgumentException("Invalid victory record.");
            var previous = BestResults.SingleOrDefault(p => p.LevelId == levelId);
            if (previous == null) BestResults.Add(new LevelProgress { LevelId = levelId, Stars = stars });
            else previous.Stars = Math.Max(previous.Stars, stars);
            HighestUnlockedLevel = Math.Max(HighestUnlockedLevel, levelNumber + 1);
        }
    }

    public sealed class SaveStore
    {
        private readonly string path;
        private readonly Action<string> report;
        public bool HadLoadError { get; private set; }
        public SaveStore(string path, Action<string> report)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            this.report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public SaveData Load()
        {
            HadLoadError = false;
            // Opening directly distinguishes a missing save from unreadable files.
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var data = (SaveData)new DataContractJsonSerializer(typeof(SaveData)).ReadObject(stream);
                    Validate(data);
                    return data;
                }
            }
            catch (FileNotFoundException) { return new SaveData(); }
            catch (DirectoryNotFoundException) { return new SaveData(); }
            catch (Exception ex) when (IsStorageError(ex))
            {
                HadLoadError = true;
                report("Save could not be loaded; using new in-memory progress. Original retained: " + ex.Message);
                return new SaveData();
            }
        }

        public bool Save(SaveData data)
        {
            Validate(data);
            string temporary = path + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(path));
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(SaveData)).WriteObject(stream, data);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception ex) when (IsStorageError(ex))
            {
                report("Save could not be written: " + ex.Message);
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                { report("Temporary save cleanup failed: " + ex.Message); }
            }
        }

        private static bool IsStorageError(Exception ex) =>
            ex is IOException || ex is UnauthorizedAccessException || ex is SerializationException
            || ex is System.Xml.XmlException || ex is FormatException;

        private static void Validate(SaveData data)
        {
            if (data == null || data.SchemaVersion != 1 || data.HighestUnlockedLevel < 1
                || (data.Locale != "en" && data.Locale != "he") || data.BestResults == null
                || data.BestResults.Any(p => p == null || string.IsNullOrWhiteSpace(p.LevelId) || p.Stars < 1 || p.Stars > 3)
                || data.BestResults.Select(p => p.LevelId).Distinct(StringComparer.Ordinal).Count() != data.BestResults.Count)
                throw new SerializationException("Invalid or incompatible save data.");
        }
    }
}
