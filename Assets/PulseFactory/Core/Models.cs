using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PulseFactory.Core
{
    public enum NodeType { Source, Conveyor, Diverter, Destination }
    public enum SwitchGroup { A, B }
    public enum SimulationCommand { Move, PulseA, PulseB }
    public enum LevelStatus { Playing, Victory, Failed }
    public enum FailureReason { None, Collision, WrongDestination, DestinationFull, BeatLimit }
    public enum EventType { SwitchToggled, Spawned, Moved, Delivered, Victory, Failed }

    public readonly struct SwitchState
    {
        public bool A { get; }
        public bool B { get; }
        public SwitchState(bool a, bool b) { A = a; B = b; }
        public bool Get(SwitchGroup group)
        {
            if (group == SwitchGroup.A) return A;
            if (group == SwitchGroup.B) return B;
            throw new ArgumentOutOfRangeException(nameof(group));
        }
        public SwitchState Toggle(SwitchGroup group)
        {
            if (group == SwitchGroup.A) return new SwitchState(!A, B);
            if (group == SwitchGroup.B) return new SwitchState(A, !B);
            throw new ArgumentOutOfRangeException(nameof(group));
        }
    }

    public sealed class NodeDefinition
    {
        public string Id { get; }
        public NodeType Type { get; }
        public float X { get; }
        public float Y { get; }
        public SwitchGroup? Group { get; }
        public bool Inverted { get; }
        public string FalseEdge { get; }
        public string TrueEdge { get; }
        public string Accepts { get; }
        public int RequiredCount { get; }

        public NodeDefinition(string id, NodeType type, float x, float y,
            SwitchGroup? group = null, bool inverted = false, string falseEdge = null,
            string trueEdge = null, string accepts = null, int requiredCount = 0)
        {
            Id = id; Type = type; X = x; Y = y; Group = group; Inverted = inverted;
            FalseEdge = falseEdge; TrueEdge = trueEdge; Accepts = accepts; RequiredCount = requiredCount;
        }
    }

    public sealed class EdgeDefinition
    {
        public string Id { get; }
        public string From { get; }
        public string To { get; }
        public EdgeDefinition(string id, string from, string to) { Id = id; From = from; To = to; }
    }

    public sealed class PackageDefinition
    {
        public string Id { get; }
        public string Kind { get; }
        public PackageDefinition(string id, string kind) { Id = id; Kind = kind; }
    }

    public sealed class PackageSpawn
    {
        public PackageDefinition Package { get; }
        public string Source { get; }
        public int Beat { get; }
        public PackageSpawn(PackageDefinition package, string source, int beat)
        { Package = package; Source = source; Beat = beat; }
    }

    public sealed class LevelDefinition
    {
        public string Id { get; }
        public int SchemaVersion { get; }
        public IReadOnlyList<NodeDefinition> Nodes { get; }
        public IReadOnlyList<EdgeDefinition> Edges { get; }
        public IReadOnlyList<PackageSpawn> Spawns { get; }
        public SwitchState InitialSwitches { get; }
        public int MaxBeats { get; }
        public int ThreeStarBeats { get; }
        public int TwoStarBeats { get; }

        public LevelDefinition(string id, int schemaVersion, IEnumerable<NodeDefinition> nodes,
            IEnumerable<EdgeDefinition> edges, IEnumerable<PackageSpawn> spawns,
            SwitchState initialSwitches, int maxBeats, int threeStarBeats, int twoStarBeats)
        {
            Id = id; SchemaVersion = schemaVersion;
            Nodes = Array.AsReadOnly((nodes ?? throw new ArgumentNullException(nameof(nodes))).ToArray());
            Edges = Array.AsReadOnly((edges ?? throw new ArgumentNullException(nameof(edges))).ToArray());
            Spawns = Array.AsReadOnly((spawns ?? throw new ArgumentNullException(nameof(spawns))).ToArray());
            InitialSwitches = initialSwitches; MaxBeats = maxBeats;
            ThreeStarBeats = threeStarBeats; TwoStarBeats = twoStarBeats;
            LevelValidator.Validate(this);
        }
    }

    public sealed class ActivePackage
    {
        public PackageDefinition Package { get; }
        public string NodeId { get; }
        public ActivePackage(PackageDefinition package, string nodeId) { Package = package; NodeId = nodeId; }
    }

    public sealed class Delivery
    {
        public string PackageId { get; }
        public string DestinationId { get; }
        public Delivery(string packageId, string destinationId) { PackageId = packageId; DestinationId = destinationId; }
    }

    public sealed class LevelResult
    {
        public LevelStatus Status { get; }
        public FailureReason Failure { get; }
        public int Stars { get; }
        public LevelResult(LevelStatus status, FailureReason failure = FailureReason.None, int stars = 0)
        { Status = status; Failure = failure; Stars = stars; }
    }

    public sealed class SimulationState
    {
        internal LevelDefinition Level { get; }
        public int Beat { get; }
        public SwitchState Switches { get; }
        public ReadOnlyCollection<ActivePackage> Packages { get; }
        public ReadOnlyCollection<Delivery> Deliveries { get; }
        public LevelResult Result { get; }
        internal SimulationState(LevelDefinition level, int beat, SwitchState switches,
            IEnumerable<ActivePackage> packages, IEnumerable<Delivery> deliveries, LevelResult result)
        {
            Level = level; Beat = beat; Switches = switches;
            Packages = Array.AsReadOnly(packages.ToArray());
            Deliveries = Array.AsReadOnly(deliveries.ToArray());
            Result = result;
        }
    }

    public sealed class SimulationEvent
    {
        public EventType Type { get; }
        public string PackageId { get; }
        public string From { get; }
        public string To { get; }
        public SwitchGroup? Group { get; }
        public FailureReason Failure { get; }
        public SimulationEvent(EventType type, string packageId = null, string from = null,
            string to = null, SwitchGroup? group = null, FailureReason failure = FailureReason.None)
        { Type = type; PackageId = packageId; From = from; To = to; Group = group; Failure = failure; }
    }

    public sealed class SimulationResult
    {
        public SimulationState State { get; }
        public IReadOnlyList<SimulationEvent> Events { get; }
        internal SimulationResult(SimulationState state, IEnumerable<SimulationEvent> events)
        { State = state; Events = Array.AsReadOnly(events.ToArray()); }
    }
}
