using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PulseFactory.Core
{
    public sealed class LevelCatalogEntry
    {
        public int Number { get; }
        public string LevelId { get; }
        public string Resource { get; }
        public string TitleKey { get; }
        public string InstructionKey { get; }
        public IReadOnlyList<SimulationCommand> IntendedSolution { get; }

        public LevelCatalogEntry(int number, string levelId, string resource, string titleKey,
            string instructionKey, IEnumerable<SimulationCommand> intendedSolution)
        {
            Number = number; LevelId = levelId; Resource = resource;
            TitleKey = titleKey; InstructionKey = instructionKey;
            IntendedSolution = Array.AsReadOnly(intendedSolution.ToArray());
        }
    }

    public sealed class LevelCatalog
    {
        public IReadOnlyList<LevelCatalogEntry> Levels { get; }

        public LevelCatalog(IEnumerable<LevelCatalogEntry> levels)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            var copy = levels.ToArray();
            if (copy.Length == 0 || copy.Any(level => level == null))
                throw new LevelValidationException("A level catalog must contain levels.");
            if (copy.Any(level => level.Number < 1 || string.IsNullOrWhiteSpace(level.LevelId)
                || string.IsNullOrWhiteSpace(level.Resource) || string.IsNullOrWhiteSpace(level.TitleKey)
                || string.IsNullOrWhiteSpace(level.InstructionKey) || level.IntendedSolution.Count == 0))
                throw new LevelValidationException("Catalog entries must be complete.");
            if (copy.Select(level => level.Number).Distinct().Count() != copy.Length
                || copy.Select(level => level.LevelId).Distinct(StringComparer.Ordinal).Count() != copy.Length
                || copy.Select(level => level.Resource).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new LevelValidationException("Catalog level numbers, IDs, and resources must be unique.");
            for (int i = 0; i < copy.Length; i++)
                if (copy[i].Number != i + 1)
                    throw new LevelValidationException("Catalog levels must be contiguous and ordered from 1.");
            Levels = Array.AsReadOnly(copy);
        }

        public static LevelCatalog Parse(string json)
        {
            CatalogData data;
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "")))
                    data = (CatalogData)new DataContractJsonSerializer(typeof(CatalogData)).ReadObject(stream);
            }
            catch (Exception ex) when (ex is SerializationException || ex is System.Xml.XmlException || ex is FormatException)
            {
                throw new LevelValidationException("Malformed level catalog: " + ex.Message);
            }
            if (data == null || data.schemaVersion != 1 || data.levels == null || data.levels.Any(level => level == null))
                throw new LevelValidationException("Invalid level catalog schema or entries.");
            return new LevelCatalog(data.levels.Select(level =>
                new LevelCatalogEntry(level.number, level.levelId, level.resource, level.titleKey,
                    level.instructionKey, (level.solution ?? Array.Empty<string>()).Select(ParseCommand))));
        }

        private static SimulationCommand ParseCommand(string value)
        {
            if (value == nameof(SimulationCommand.Move)) return SimulationCommand.Move;
            if (value == nameof(SimulationCommand.PulseA)) return SimulationCommand.PulseA;
            if (value == nameof(SimulationCommand.PulseB)) return SimulationCommand.PulseB;
            throw new LevelValidationException("Unknown command in catalog solution: " + value);
        }

        [DataContract]
        private sealed class CatalogData
        {
            [DataMember(IsRequired = true)] public int schemaVersion;
            [DataMember(IsRequired = true)] public CatalogEntryData[] levels;
        }
        [DataContract]
        private sealed class CatalogEntryData
        {
            [DataMember(IsRequired = true)] public int number;
            [DataMember(IsRequired = true)] public string levelId;
            [DataMember(IsRequired = true)] public string resource;
            [DataMember(IsRequired = true)] public string titleKey;
            [DataMember(IsRequired = true)] public string instructionKey;
            [DataMember(IsRequired = true)] public string[] solution;
        }
    }
}
