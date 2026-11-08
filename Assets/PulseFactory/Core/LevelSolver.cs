using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PulseFactory.Core
{
    public sealed class LevelSolverResult
    {
        public IReadOnlyList<SimulationCommand> Commands { get; }
        public int ExpandedStates { get; }
        public bool LimitReached { get; }
        public bool Solved => Commands != null && !LimitReached;
        internal LevelSolverResult(IEnumerable<SimulationCommand> commands, int expandedStates, bool limitReached)
        {
            Commands = commands == null ? null : Array.AsReadOnly(commands.ToArray());
            ExpandedStates = expandedStates;
            LimitReached = limitReached;
        }
    }

    public static class LevelSolver
    {
        public const int DefaultStateLimit = 100000;

        private sealed class SearchNode
        {
            public SimulationState State { get; }
            public SimulationCommand[] Commands { get; }
            public SearchNode(SimulationState state, SimulationCommand[] commands)
            { State = state; Commands = commands; }
        }

        public static LevelSolverResult Solve(LevelDefinition level, int maxExpandedStates = DefaultStateLimit)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (maxExpandedStates < 1) throw new ArgumentOutOfRangeException(nameof(maxExpandedStates));
            var engine = new SimulationEngine(level);
            var initial = engine.InitialState();
            var queue = new Queue<SearchNode>();
            var visited = new HashSet<string>(StringComparer.Ordinal) { CanonicalKey(initial) };
            queue.Enqueue(new SearchNode(initial, Array.Empty<SimulationCommand>()));
            int expanded = 0;
            var commands = new[] { SimulationCommand.Move, SimulationCommand.PulseA, SimulationCommand.PulseB };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.State.Result.Status == LevelStatus.Victory)
                    return new LevelSolverResult(current.Commands, expanded, false);
                if (current.State.Result.Status != LevelStatus.Playing) continue;
                if (expanded >= maxExpandedStates)
                    return new LevelSolverResult(null, expanded, true);
                expanded++;

                foreach (var command in commands)
                {
                    if (!engine.IsAvailable(current.State, command)) continue;
                    var state = engine.Step(current.State, command).State;
                    if (state.Result.Status == LevelStatus.Failed) continue;
                    if (!visited.Add(CanonicalKey(state))) continue;
                    var path = new SimulationCommand[current.Commands.Length + 1];
                    Array.Copy(current.Commands, path, current.Commands.Length);
                    path[path.Length - 1] = command;
                    queue.Enqueue(new SearchNode(state, path));
                }
            }
            return new LevelSolverResult(null, expanded, false);
        }

        public static string CanonicalKey(SimulationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var key = new StringBuilder();
            key.Append(state.Beat).Append('|')
                .Append(state.Switches.A ? '1' : '0').Append(state.Switches.B ? '1' : '0').Append('|')
                .Append((int)state.Result.Status).Append(':').Append((int)state.Result.Failure).Append('|');
            foreach (var package in state.Packages.OrderBy(package => package.Package.Id, StringComparer.Ordinal))
                key.Append(package.Package.Id.Length).Append(':').Append(package.Package.Id)
                    .Append('@').Append(package.NodeId.Length).Append(':').Append(package.NodeId).Append(';');
            key.Append('|');
            foreach (var delivery in state.Deliveries.OrderBy(delivery => delivery.PackageId, StringComparer.Ordinal))
                key.Append(delivery.PackageId.Length).Append(':').Append(delivery.PackageId)
                    .Append('@').Append(delivery.DestinationId.Length).Append(':').Append(delivery.DestinationId).Append(';');
            return key.ToString();
        }
    }
}
