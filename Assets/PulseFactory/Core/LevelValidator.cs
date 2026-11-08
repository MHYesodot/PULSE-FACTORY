using System;
using System.Collections.Generic;
using System.Linq;

namespace PulseFactory.Core
{
    public sealed class LevelValidationException : Exception
    {
        public LevelValidationException(string message) : base(message) { }
    }

    public static class LevelValidator
    {
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new LevelValidationException(message);
        }

        public static void Validate(LevelDefinition level)
        {
            Require(!string.IsNullOrWhiteSpace(level.Id), "Level ID is required.");
            Require(level.SchemaVersion == 1, "Unsupported level schema version.");
            Require(level.MaxBeats > 0, "Maximum beats must be positive.");
            Require(level.ThreeStarBeats > 0 && level.ThreeStarBeats <= level.TwoStarBeats
                && level.TwoStarBeats <= level.MaxBeats, "Star thresholds must be ordered within the beat limit.");
            Require(level.Nodes.Count > 0, "Nodes are required.");
            Require(level.Nodes.All(n => n != null && !string.IsNullOrWhiteSpace(n.Id)), "Node IDs are required.");
            Require(level.Nodes.Select(n => n.Id).Distinct(StringComparer.Ordinal).Count() == level.Nodes.Count,
                "Duplicate node IDs.");
            var nodes = level.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
            Require(level.Edges.All(e => e != null && !string.IsNullOrWhiteSpace(e.Id)), "Edge IDs are required.");
            Require(level.Edges.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() == level.Edges.Count,
                "Duplicate edge IDs.");
            foreach (var edge in level.Edges)
                Require(edge.From != null && edge.To != null && nodes.ContainsKey(edge.From) && nodes.ContainsKey(edge.To),
                    "Invalid edge reference: " + edge.Id);

            foreach (var node in level.Nodes)
            {
                Require(Enum.IsDefined(typeof(NodeType), node.Type), "Unsupported node type: " + node.Id);
                Require(!float.IsNaN(node.X) && !float.IsInfinity(node.X)
                    && !float.IsNaN(node.Y) && !float.IsInfinity(node.Y), "Invalid board position: " + node.Id);
                var outgoing = level.Edges.Where(e => e.From == node.Id).ToArray();
                if (node.Type == NodeType.Destination)
                    Require(outgoing.Length == 0 && !string.IsNullOrWhiteSpace(node.Accepts) && node.RequiredCount > 0,
                        "Destination must be a sink with a kind and positive requirement: " + node.Id);
                else
                    Require(node.Accepts == null && node.RequiredCount == 0,
                        "Only destinations may have requirements: " + node.Id);

                if (node.Type == NodeType.Diverter)
                {
                    Require(node.Group.HasValue && Enum.IsDefined(typeof(SwitchGroup), node.Group.Value),
                        "Unsupported or missing switch group: " + node.Id);
                    Require(outgoing.Length == 2 && node.FalseEdge != node.TrueEdge
                        && outgoing.Any(e => e.Id == node.FalseEdge) && outgoing.Any(e => e.Id == node.TrueEdge),
                        "Diverter must map two distinct outgoing edges: " + node.Id);
                }
                else
                {
                    Require(node.Group == null && !node.Inverted && node.FalseEdge == null && node.TrueEdge == null,
                        "Only diverters may define switch routing: " + node.Id);
                    if (node.Type != NodeType.Destination)
                        Require(outgoing.Length == 1, "Source/conveyor must have one outgoing edge: " + node.Id);
                }
                if (node.Type == NodeType.Source)
                    Require(!level.Edges.Any(e => e.To == node.Id), "Sources cannot have incoming edges: " + node.Id);
            }

            Require(level.Nodes.Any(n => n.Type == NodeType.Destination), "At least one destination is required.");
            Require(level.Spawns.Count > 0, "At least one package is required.");
            Require(level.Spawns.All(s => s != null && s.Package != null
                && !string.IsNullOrWhiteSpace(s.Package.Id) && !string.IsNullOrWhiteSpace(s.Package.Kind)),
                "Package IDs and kinds are required.");
            Require(level.Spawns.Select(s => s.Package.Id).Distinct(StringComparer.Ordinal).Count() == level.Spawns.Count,
                "Duplicate package IDs.");
            foreach (var spawn in level.Spawns)
            {
                Require(spawn.Beat > 0 && spawn.Beat <= level.MaxBeats, "Spawn beat outside level limit.");
                Require(spawn.Source != null && nodes.TryGetValue(spawn.Source, out var source)
                    && source.Type == NodeType.Source, "Spawn must reference a source.");
            }
            Require(!level.Spawns.GroupBy(s => new { s.Source, s.Beat }).Any(g => g.Count() > 1),
                "Only one package can spawn at a source per beat.");
            var destinations = level.Nodes.Where(n => n.Type == NodeType.Destination).ToArray();
            var kinds = level.Spawns.Select(s => s.Package.Kind).Union(destinations.Select(n => n.Accepts));
            foreach (var kind in kinds)
                Require(level.Spawns.Count(s => s.Package.Kind == kind)
                    == destinations.Where(n => n.Accepts == kind).Sum(n => (long)n.RequiredCount),
                    "Spawn count must match destination requirements for kind: " + kind);

            // Kahn's algorithm avoids recursion depth limits on authored graphs.
            var incoming = nodes.Keys.ToDictionary(id => id, id => 0, StringComparer.Ordinal);
            var adjacency = nodes.Keys.ToDictionary(id => id, id => new List<string>(), StringComparer.Ordinal);
            foreach (var edge in level.Edges) { incoming[edge.To]++; adjacency[edge.From].Add(edge.To); }
            var ready = new Queue<string>(incoming.Where(pair => pair.Value == 0).Select(pair => pair.Key));
            int visited = 0;
            while (ready.Count > 0)
            {
                var id = ready.Dequeue();
                visited++;
                foreach (var target in adjacency[id])
                    if (--incoming[target] == 0) ready.Enqueue(target);
            }
            Require(visited == nodes.Count, "Level graph contains a cycle.");
        }
    }
}
