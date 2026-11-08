using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PulseFactory.Core
{
    public static class LevelJson
    {
        public static LevelDefinition Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new LevelValidationException("Level JSON is empty.");
            LevelData data;
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    data = (LevelData)new DataContractJsonSerializer(typeof(LevelData)).ReadObject(stream);
            }
            catch (Exception ex) when (ex is SerializationException || ex is System.Xml.XmlException || ex is FormatException)
            {
                throw new LevelValidationException("Malformed level JSON: " + ex.Message);
            }
            if (data == null || data.nodes == null || data.edges == null || data.spawns == null || data.initialSwitches == null
                || data.nodes.Any(n => n == null) || data.edges.Any(e => e == null) || data.spawns.Any(s => s == null))
                throw new LevelValidationException("Level arrays, entries, and initialSwitches cannot be null.");
            return new LevelDefinition(data.id, data.schemaVersion,
                data.nodes.Select(n => new NodeDefinition(n.id, ParseEnum<NodeType>(n.type), n.x, n.y,
                    n.group == null ? (SwitchGroup?)null : ParseEnum<SwitchGroup>(n.group),
                    n.inverted, n.falseEdge, n.trueEdge, n.accepts, n.requiredCount)),
                data.edges.Select(e => new EdgeDefinition(e.id, e.from, e.to)),
                data.spawns.Select(s => new PackageSpawn(new PackageDefinition(s.id, s.kind), s.source, s.beat)),
                new SwitchState(data.initialSwitches.a, data.initialSwitches.b),
                data.maxBeats, data.threeStarBeats, data.twoStarBeats);
        }

        private static T ParseEnum<T>(string value) where T : struct
        {
            if (value == null || !Enum.GetNames(typeof(T)).Contains(value)
                || !Enum.TryParse(value, out T parsed))
                throw new LevelValidationException("Unsupported " + typeof(T).Name + ": " + value);
            return parsed;
        }

        [DataContract]
        private sealed class LevelData
        {
            [DataMember(IsRequired = true)] public string id;
            [DataMember(IsRequired = true)] public int schemaVersion;
            [DataMember(IsRequired = true)] public NodeData[] nodes;
            [DataMember(IsRequired = true)] public EdgeData[] edges;
            [DataMember(IsRequired = true)] public SpawnData[] spawns;
            [DataMember(IsRequired = true)] public SwitchData initialSwitches;
            [DataMember(IsRequired = true)] public int maxBeats;
            [DataMember(IsRequired = true)] public int threeStarBeats;
            [DataMember(IsRequired = true)] public int twoStarBeats;
        }
        [DataContract]
        private sealed class NodeData
        {
            [DataMember(IsRequired = true)] public string id;
            [DataMember(IsRequired = true)] public string type;
            [DataMember(IsRequired = true)] public float x;
            [DataMember(IsRequired = true)] public float y;
            [DataMember] public string group;
            [DataMember] public bool inverted;
            [DataMember] public string falseEdge;
            [DataMember] public string trueEdge;
            [DataMember] public string accepts;
            [DataMember] public int requiredCount;
        }
        [DataContract]
        private sealed class EdgeData
        {
            [DataMember(IsRequired = true)] public string id;
            [DataMember(IsRequired = true)] public string from;
            [DataMember(IsRequired = true)] public string to;
        }
        [DataContract]
        private sealed class SpawnData
        {
            [DataMember(IsRequired = true)] public string id;
            [DataMember(IsRequired = true)] public string kind;
            [DataMember(IsRequired = true)] public string source;
            [DataMember(IsRequired = true)] public int beat;
        }
        [DataContract]
        private sealed class SwitchData
        {
            [DataMember(IsRequired = true)] public bool a;
            [DataMember(IsRequired = true)] public bool b;
        }
    }
}
