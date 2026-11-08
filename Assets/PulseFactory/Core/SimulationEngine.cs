using System;
using System.Collections.Generic;
using System.Linq;

namespace PulseFactory.Core
{
    public sealed class SimulationEngine
    {
        public LevelDefinition Level { get; }
        private readonly Dictionary<string, NodeDefinition> nodes;
        private readonly Dictionary<string, EdgeDefinition> edges;
        private readonly Dictionary<string, EdgeDefinition[]> outgoing;

        public SimulationEngine(LevelDefinition level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            nodes = level.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
            edges = level.Edges.ToDictionary(e => e.Id, StringComparer.Ordinal);
            outgoing = level.Nodes.ToDictionary(n => n.Id,
                n => level.Edges.Where(e => e.From == n.Id).ToArray(), StringComparer.Ordinal);
        }

        public SimulationState InitialState() => new SimulationState(Level, 0, Level.InitialSwitches,
            Array.Empty<ActivePackage>(), Array.Empty<Delivery>(), new LevelResult(LevelStatus.Playing));

        public bool IsAvailable(SimulationState state, SimulationCommand command)
        {
            if (state == null || !ReferenceEquals(state.Level, Level) || state.Result.Status != LevelStatus.Playing)
                return false;
            if (command == SimulationCommand.Move) return true;
            if (command != SimulationCommand.PulseA && command != SimulationCommand.PulseB) return false;
            var group = command == SimulationCommand.PulseA ? SwitchGroup.A : SwitchGroup.B;
            return Level.Nodes.Any(n => n.Type == NodeType.Diverter && n.Group == group);
        }

        public string Route(NodeDefinition node, SwitchState switches)
        {
            if (!nodes.TryGetValue(node.Id, out var owned) || !ReferenceEquals(owned, node))
                throw new ArgumentException("Node does not belong to this level.", nameof(node));
            if (node.Type == NodeType.Destination)
                throw new InvalidOperationException("Destinations have no outgoing route.");
            if (node.Type != NodeType.Diverter) return outgoing[node.Id][0].To;
            bool branch = switches.Get(node.Group.Value) ^ node.Inverted;
            return edges[branch ? node.TrueEdge : node.FalseEdge].To;
        }

        public SimulationResult Step(SimulationState current, SimulationCommand command)
        {
            if (!IsAvailable(current, command))
                throw new InvalidOperationException("Command is unavailable for this state.");
            int beat = current.Beat + 1;
            var switches = current.Switches;
            var events = new List<SimulationEvent>();
            if (command != SimulationCommand.Move)
            {
                var group = command == SimulationCommand.PulseA ? SwitchGroup.A : SwitchGroup.B;
                switches = switches.Toggle(group);
                events.Add(new SimulationEvent(EventType.SwitchToggled, group: group));
            }

            var active = current.Packages.ToList();
            foreach (var spawn in Level.Spawns.Where(s => s.Beat == beat).OrderBy(s => s.Package.Id, StringComparer.Ordinal))
            {
                active.Add(new ActivePackage(spawn.Package, spawn.Source));
                events.Add(new SimulationEvent(EventType.Spawned, spawn.Package.Id, to: spawn.Source));
            }
            active = active.OrderBy(p => p.Package.Id, StringComparer.Ordinal).ToList();
            var moves = active.Select(p => new ActivePackage(p.Package, Route(nodes[p.NodeId], switches))).ToArray();

            FailureReason failure = FailureReason.None;
            if (moves.Where(p => nodes[p.NodeId].Type != NodeType.Destination)
                .GroupBy(p => p.NodeId).Any(g => g.Count() > 1))
                failure = FailureReason.Collision;
            else if (moves.Any(p => nodes[p.NodeId].Type == NodeType.Destination && nodes[p.NodeId].Accepts != p.Package.Kind))
                failure = FailureReason.WrongDestination;
            else if (moves.Where(p => nodes[p.NodeId].Type == NodeType.Destination).GroupBy(p => p.NodeId)
                .Any(g => g.Count() + current.Deliveries.Count(d => d.DestinationId == g.Key) > nodes[g.Key].RequiredCount))
                failure = FailureReason.DestinationFull;

            // A rejected movement batch applies no moves or deliveries, but the command consumed its beat.
            if (failure != FailureReason.None)
            {
                events.Add(new SimulationEvent(EventType.Failed, failure: failure));
                return new SimulationResult(new SimulationState(Level, beat, switches, active, current.Deliveries,
                    new LevelResult(LevelStatus.Failed, failure)), events);
            }

            var remaining = new List<ActivePackage>();
            var deliveries = current.Deliveries.ToList();
            for (int i = 0; i < moves.Length; i++)
                events.Add(new SimulationEvent(EventType.Moved, moves[i].Package.Id, active[i].NodeId, moves[i].NodeId));
            foreach (var move in moves)
            {
                if (nodes[move.NodeId].Type == NodeType.Destination)
                {
                    deliveries.Add(new Delivery(move.Package.Id, move.NodeId));
                    events.Add(new SimulationEvent(EventType.Delivered, move.Package.Id, to: move.NodeId));
                }
                else remaining.Add(move);
            }

            var result = new LevelResult(LevelStatus.Playing);
            if (deliveries.Count == Level.Spawns.Count)
            {
                int stars = beat <= Level.ThreeStarBeats ? 3 : beat <= Level.TwoStarBeats ? 2 : 1;
                result = new LevelResult(LevelStatus.Victory, stars: stars);
                events.Add(new SimulationEvent(EventType.Victory));
            }
            else if (beat >= Level.MaxBeats)
            {
                result = new LevelResult(LevelStatus.Failed, FailureReason.BeatLimit);
                events.Add(new SimulationEvent(EventType.Failed, failure: FailureReason.BeatLimit));
            }
            return new SimulationResult(new SimulationState(Level, beat, switches, remaining, deliveries, result), events);
        }
    }

    public sealed class GameSession
    {
        public SimulationEngine Engine { get; }
        public SimulationState State { get; private set; }
        private readonly Stack<SimulationState> history = new Stack<SimulationState>();
        public bool CanUndo => history.Count > 0;
        public GameSession(LevelDefinition level) { Engine = new SimulationEngine(level); State = Engine.InitialState(); }
        public SimulationResult Execute(SimulationCommand command)
        {
            var result = Engine.Step(State, command);
            history.Push(State);
            State = result.State;
            return result;
        }
        public void Undo()
        {
            if (!CanUndo) throw new InvalidOperationException("No command to undo.");
            State = history.Pop();
        }
        public void Restart() { history.Clear(); State = Engine.InitialState(); }
    }
}
