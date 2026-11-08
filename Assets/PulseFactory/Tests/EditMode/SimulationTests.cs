using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PulseFactory.Core;

namespace PulseFactory.Tests
{
    internal static class TestResources
    {
        public static string Read(string path)
        {
#if UNITY_5_3_OR_NEWER
            string root = Path.Combine(UnityEngine.Application.dataPath, "Resources");
#else
            string root = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources");
#endif
            return File.ReadAllText(Path.Combine(root, path));
        }
        public static LevelDefinition Tutorial() => LevelJson.Parse(Read("Levels/tutorial.json"));
    }

    public sealed class SimulationTests
    {
        private static string Snapshot(SimulationState state) =>
            string.Join("|", state.Beat, state.Switches.A, state.Switches.B,
                state.Result.Status, state.Result.Failure, state.Result.Stars,
                string.Join(",", state.Packages.Select(p => p.Package.Id + ":" + p.Package.Kind + ":" + p.NodeId)),
                string.Join(",", state.Deliveries.Select(d => d.PackageId + ":" + d.DestinationId)));

        private static string Events(SimulationResult result) => string.Join("|", result.Events.Select(e =>
            string.Join(":", e.Type, e.PackageId, e.From, e.To, e.Group, e.Failure)));

        internal static LevelDefinition Chain(int maxBeats = 4, bool twoPackages = false)
        {
            return new LevelDefinition("chain", 1, new[]
            {
                new NodeDefinition("S", NodeType.Source, 0, 0),
                new NodeDefinition("C", NodeType.Conveyor, 0, 1),
                new NodeDefinition("D", NodeType.Destination, 0, 2, accepts: "red", requiredCount: twoPackages ? 2 : 1)
            }, new[] { new EdgeDefinition("SC", "S", "C"), new EdgeDefinition("CD", "C", "D") },
                twoPackages
                    ? new[] { new PackageSpawn(new PackageDefinition("p1", "red"), "S", 1),
                        new PackageSpawn(new PackageDefinition("p2", "red"), "S", 2) }
                    : new[] { new PackageSpawn(new PackageDefinition("p1", "red"), "S", 1) },
                new SwitchState(false, false), maxBeats, 1, maxBeats);
        }

        [Test]
        public void TutorialSolutionDeliversBothPackagesOnBeatFour()
        {
            var session = new GameSession(TestResources.Tutorial());
            foreach (var command in new[] { SimulationCommand.Move, SimulationCommand.Move, SimulationCommand.Move, SimulationCommand.PulseA })
                session.Execute(command);
            Assert.That(session.State.Beat, Is.EqualTo(4));
            Assert.That(session.State.Result.Status, Is.EqualTo(LevelStatus.Victory));
            Assert.That(session.State.Result.Stars, Is.EqualTo(3));
            Assert.That(session.State.Packages, Is.Empty);
            Assert.That(session.State.Deliveries.Select(d => d.PackageId + ":" + d.DestinationId),
                Is.EqualTo(new[] { "red-1:R", "blue-1:B" }));
        }

        [Test]
        public void TutorialIncorrectRoutingFailsWithoutApplyingMovementBatch()
        {
            var session = new GameSession(TestResources.Tutorial());
            session.Execute(SimulationCommand.Move);
            session.Execute(SimulationCommand.Move);
            session.Execute(SimulationCommand.Move);
            var result = session.Execute(SimulationCommand.Move);
            Assert.That(result.State.Result.Failure, Is.EqualTo(FailureReason.WrongDestination));
            Assert.That(result.State.Deliveries.Count, Is.EqualTo(1));
            Assert.That(result.State.Packages.Single().NodeId, Is.EqualTo("J"));
            Assert.That(result.Events.Any(e => e.Type == EventType.Moved), Is.False);
        }

        [Test]
        public void SpawnParticipatesInItsScheduledBeatAndDoesNotMutateInput()
        {
            var session = new GameSession(TestResources.Tutorial());
            var initial = session.State;
            string before = Snapshot(initial);
            var result = session.Execute(SimulationCommand.Move);
            Assert.That(result.State.Packages.Single().Package.Id, Is.EqualTo("red-1"));
            Assert.That(result.State.Packages.Single().NodeId, Is.EqualTo("J"));
            Assert.That(result.Events.Select(e => e.Type), Is.EqualTo(new[] { EventType.Spawned, EventType.Moved }));
            Assert.That(Snapshot(initial), Is.EqualTo(before));
            session.Execute(SimulationCommand.Move);
            Assert.That(session.State.Packages, Is.Empty);
            Assert.That(session.State.Result.Status, Is.EqualTo(LevelStatus.Playing));
            session.Execute(SimulationCommand.Move);
            Assert.That(session.State.Packages.Single().Package.Id, Is.EqualTo("blue-1"));
        }

        [Test]
        public void MovePreservesSwitchesAndPulseTogglesBeforeRouting()
        {
            var session = new GameSession(TestResources.Tutorial());
            session.Execute(SimulationCommand.Move);
            Assert.That(session.State.Switches.A, Is.False);
            var result = session.Execute(SimulationCommand.PulseA);
            Assert.That(result.State.Switches.A, Is.True);
            Assert.That(result.State.Result.Failure, Is.EqualTo(FailureReason.WrongDestination));
            Assert.That(result.Events.First().Type, Is.EqualTo(EventType.SwitchToggled));
            Assert.That(result.State.Beat, Is.EqualTo(2));
        }

        [TestCase("A", false)]
        [TestCase("B", false)]
        [TestCase("A", true)]
        [TestCase("B", true)]
        public void GroupsAndNodeInversionSelectExpectedEdge(string group, bool inverted)
        {
            string json = TestResources.Read("Levels/tutorial.json").Replace("\"group\": \"A\"", "\"group\": \"" + group + "\"");
            if (inverted) json = json.Replace("\"falseEdge\":", "\"inverted\": true, \"falseEdge\":");
            var level = LevelJson.Parse(json);
            var engine = new SimulationEngine(level);
            var node = level.Nodes.Single(n => n.Id == "J");
            Assert.That(engine.Route(node, new SwitchState(false, false)), Is.EqualTo(inverted ? "B" : "R"));
            var session = new GameSession(level);
            var command = group == "A" ? SimulationCommand.PulseA : SimulationCommand.PulseB;
            session.Execute(command);
            Assert.That(session.State.Switches.Get(group == "A" ? SwitchGroup.A : SwitchGroup.B), Is.True);
            Assert.That(engine.Route(node, session.State.Switches), Is.EqualTo(inverted ? "R" : "B"));
            Assert.That(session.State.Switches.Get(group == "A" ? SwitchGroup.B : SwitchGroup.A), Is.False);
        }

        [Test]
        public void AllDivertersInGroupChangeTogetherWhileOtherGroupIsUnchanged()
        {
            var original = TestResources.Tutorial();
            var second = new NodeDefinition("J2", NodeType.Diverter, 1, 1, SwitchGroup.A, true, "J2R", "J2B");
            var third = new NodeDefinition("J3", NodeType.Diverter, 2, 1, SwitchGroup.B, false, "J3R", "J3B");
            var level = new LevelDefinition("groups", 1, original.Nodes.Concat(new[] { second, third }),
                original.Edges.Concat(new[] { new EdgeDefinition("J2R", "J2", "R"), new EdgeDefinition("J2B", "J2", "B"),
                    new EdgeDefinition("J3R", "J3", "R"), new EdgeDefinition("J3B", "J3", "B") }),
                original.Spawns, original.InitialSwitches, 6, 4, 5);
            var session = new GameSession(level);
            session.Execute(SimulationCommand.PulseA);
            Assert.That(session.Engine.Route(level.Nodes.Single(n => n.Id == "J"), session.State.Switches), Is.EqualTo("B"));
            Assert.That(session.Engine.Route(second, session.State.Switches), Is.EqualTo("R"));
            Assert.That(session.Engine.Route(third, session.State.Switches), Is.EqualTo("R"));
        }

        [Test]
        public void OnePulseRoutesPackagesAtTwoDivertersSimultaneously()
        {
            var original = TestResources.Tutorial();
            var nodes = original.Nodes.Concat(new[]
            {
                new NodeDefinition("S2", NodeType.Source, 1, 0),
                new NodeDefinition("J2", NodeType.Diverter, 1, 1, SwitchGroup.A, true, "J2R", "J2B")
            });
            var edges = original.Edges.Concat(new[]
            {
                new EdgeDefinition("S2J2", "S2", "J2"),
                new EdgeDefinition("J2R", "J2", "R"),
                new EdgeDefinition("J2B", "J2", "B")
            });
            var spawns = new[]
            {
                new PackageSpawn(new PackageDefinition("red", "red"), "S", 1),
                new PackageSpawn(new PackageDefinition("blue", "blue"), "S2", 1)
            };
            var session = new GameSession(new LevelDefinition("simultaneous", 1, nodes, edges, spawns,
                new SwitchState(true, false), 3, 2, 3));
            session.Execute(SimulationCommand.Move);
            var result = session.Execute(SimulationCommand.PulseA);
            Assert.That(result.State.Result.Status, Is.EqualTo(LevelStatus.Victory));
            Assert.That(result.State.Deliveries.Select(d => d.PackageId + ":" + d.DestinationId),
                Is.EqualTo(new[] { "blue:B", "red:R" }));
            Assert.That(result.Events.Count(e => e.Type == EventType.SwitchToggled), Is.EqualTo(1));
            Assert.That(result.Events.Count(e => e.Type == EventType.Moved), Is.EqualTo(2));
        }

        [Test]
        public void SimultaneousMovementAllowsEnteringVacatedNode()
        {
            var session = new GameSession(Chain(twoPackages: true));
            session.Execute(SimulationCommand.Move);
            var result = session.Execute(SimulationCommand.Move);
            Assert.That(result.State.Result.Status, Is.EqualTo(LevelStatus.Playing));
            Assert.That(result.State.Packages.Single().Package.Id, Is.EqualTo("p2"));
            Assert.That(result.State.Packages.Single().NodeId, Is.EqualTo("C"));
            Assert.That(result.State.Deliveries.Single().PackageId, Is.EqualTo("p1"));
            Assert.That(result.Events.Count(e => e.Type == EventType.Moved), Is.EqualTo(2));
            Assert.That(result.Events.Select(e => e.Type),
                Is.EqualTo(new[] { EventType.Spawned, EventType.Moved, EventType.Moved, EventType.Delivered }));
            session.Execute(SimulationCommand.Move);
            Assert.That(session.State.Result.Status, Is.EqualTo(LevelStatus.Victory));
        }

        private static LevelDefinition Merge(bool destination)
        {
            return new LevelDefinition("merge", 1, destination ? new[]
            {
                new NodeDefinition("S1", NodeType.Source, 0, 0),
                new NodeDefinition("S2", NodeType.Source, 1, 0),
                new NodeDefinition("M", NodeType.Destination, 0, 1, accepts: "red", requiredCount: 2)
            } : new[]
            {
                new NodeDefinition("S1", NodeType.Source, 0, 0),
                new NodeDefinition("S2", NodeType.Source, 1, 0),
                new NodeDefinition("M", NodeType.Conveyor, 0, 1),
                new NodeDefinition("D", NodeType.Destination, 0, 2, accepts: "red", requiredCount: 2)
            }, destination ? new[] { new EdgeDefinition("1", "S1", "M"), new EdgeDefinition("2", "S2", "M") }
                : new[] { new EdgeDefinition("1", "S1", "M"), new EdgeDefinition("2", "S2", "M"), new EdgeDefinition("3", "M", "D") },
                new[] { new PackageSpawn(new PackageDefinition("p1", "red"), "S1", 1),
                    new PackageSpawn(new PackageDefinition("p2", "red"), "S2", 1) },
                new SwitchState(false, false), 3, 1, 2);
        }

        [Test]
        public void CollisionRejectsAllMovesDeterministically()
        {
            var engine = new SimulationEngine(Merge(false));
            var initial = engine.InitialState();
            var first = engine.Step(initial, SimulationCommand.Move);
            var second = engine.Step(initial, SimulationCommand.Move);
            Assert.That(first.State.Result.Failure, Is.EqualTo(FailureReason.Collision));
            Assert.That(first.State.Packages.Select(p => p.NodeId), Is.EqualTo(new[] { "S1", "S2" }));
            Assert.That(first.State.Deliveries, Is.Empty);
            Assert.That(first.Events.Any(e => e.Type == EventType.Moved), Is.False);
            Assert.That(Snapshot(first.State), Is.EqualTo(Snapshot(second.State)));
            Assert.That(Events(first), Is.EqualTo(Events(second)));
        }

        [Test]
        public void MatchingPackagesCanEnterSameDestinationSimultaneously()
        {
            var session = new GameSession(Merge(true));
            session.Execute(SimulationCommand.Move);
            Assert.That(session.State.Result.Status, Is.EqualTo(LevelStatus.Victory));
            Assert.That(session.State.Deliveries.Count, Is.EqualTo(2));
        }

        [Test]
        public void DestinationCapacityCannotBeExceeded()
        {
            var original = Merge(true);
            var nodes = original.Nodes.Where(n => n.Id != "M").Concat(new[]
            {
                new NodeDefinition("M", NodeType.Destination, 0, 1, accepts: "red", requiredCount: 1),
                new NodeDefinition("Unused", NodeType.Destination, 1, 1, accepts: "red", requiredCount: 1)
            });
            var session = new GameSession(new LevelDefinition("capacity", 1, nodes, original.Edges, original.Spawns,
                original.InitialSwitches, 3, 1, 2));
            session.Execute(SimulationCommand.Move);
            Assert.That(session.State.Result.Failure, Is.EqualTo(FailureReason.DestinationFull));
        }

        [Test]
        public void MaximumBeatFailsButVictoryOnFinalBeatWins()
        {
            var tooShort = new GameSession(Chain(maxBeats: 1));
            tooShort.Execute(SimulationCommand.Move);
            Assert.That(tooShort.State.Result.Failure, Is.EqualTo(FailureReason.BeatLimit));
            Assert.That(tooShort.State.Packages.Single().NodeId, Is.EqualTo("C"));
            var exact = new GameSession(Chain(maxBeats: 2));
            exact.Execute(SimulationCommand.Move);
            exact.Execute(SimulationCommand.Move);
            Assert.That(exact.State.Result.Status, Is.EqualTo(LevelStatus.Victory));
            Assert.That(exact.State.Result.Stars, Is.EqualTo(2));
        }

        [Test]
        public void UndoRestoresExactObjectIncludingToggleSpawnAndFailure()
        {
            var session = new GameSession(TestResources.Tutorial());
            var initial = session.State;
            session.Execute(SimulationCommand.Move);
            var previous = session.State;
            session.Execute(SimulationCommand.PulseA);
            session.Undo();
            Assert.That(session.State, Is.SameAs(previous));
            session.Undo();
            Assert.That(session.State, Is.SameAs(initial));
            Assert.That(session.CanUndo, Is.False);
            Assert.Throws<InvalidOperationException>(() => session.Undo());
        }

        [Test]
        public void RestartRestoresInitialStateAndClearsHistory()
        {
            var session = new GameSession(TestResources.Tutorial());
            string original = Snapshot(session.State);
            session.Execute(SimulationCommand.PulseA);
            session.Execute(SimulationCommand.Move);
            session.Restart();
            Assert.That(Snapshot(session.State), Is.EqualTo(original));
            Assert.That(session.CanUndo, Is.False);
        }

        [Test]
        public void UnavailableCommandsDoNotConsumeBeatOrHistory()
        {
            var session = new GameSession(TestResources.Tutorial());
            Assert.That(session.Engine.IsAvailable(session.State, SimulationCommand.PulseB), Is.False);
            Assert.Throws<InvalidOperationException>(() => session.Execute(SimulationCommand.PulseB));
            Assert.Throws<InvalidOperationException>(() => session.Execute((SimulationCommand)99));
            Assert.That(session.State.Beat, Is.Zero);
            Assert.That(session.CanUndo, Is.False);
            var foreign = new SimulationEngine(TestResources.Tutorial()).InitialState();
            Assert.Throws<InvalidOperationException>(() => session.Engine.Step(foreign, SimulationCommand.Move));
        }

        [Test]
        public void TerminalStateRejectsFurtherCommandsAndCanBeUndone()
        {
            var session = new GameSession(Chain(2));
            session.Execute(SimulationCommand.Move);
            var before = session.State;
            session.Execute(SimulationCommand.Move);
            Assert.Throws<InvalidOperationException>(() => session.Execute(SimulationCommand.Move));
            Assert.That(session.State.Beat, Is.EqualTo(2));
            session.Undo();
            Assert.That(session.State, Is.SameAs(before));
        }

        [Test]
        public void ReplaysMatchEveryStateAndEventAcrossIndependentLoads()
        {
            var one = new GameSession(TestResources.Tutorial());
            var two = new GameSession(TestResources.Tutorial());
            foreach (var command in new[] { SimulationCommand.Move, SimulationCommand.Move, SimulationCommand.Move, SimulationCommand.PulseA })
            {
                var a = one.Execute(command);
                var b = two.Execute(command);
                Assert.That(Snapshot(a.State), Is.EqualTo(Snapshot(b.State)));
                Assert.That(Events(a), Is.EqualTo(Events(b)));
            }
        }

        [Test]
        public void EightCuratedLevelsHaveValidatedShortestIntendedSolutions()
        {
            var catalog = LevelCatalog.Parse(TestResources.Read("Levels/catalog.json"));
            Assert.That(catalog.Levels.Count, Is.EqualTo(8));
            for (int i = 0; i < catalog.Levels.Count; i++)
            {
                var entry = catalog.Levels[i];
                Assert.That(entry.Number, Is.EqualTo(i + 1));
                var level = LevelJson.Parse(TestResources.Read(entry.Resource + ".json"));
                Assert.That(level.Id, Is.EqualTo(entry.LevelId));
                Assert.That(level.MaxBeats, Is.GreaterThanOrEqualTo(entry.IntendedSolution.Count));

                var search = LevelSolver.Solve(level);
                Assert.That(search.LimitReached, Is.False, entry.LevelId + " hit the search bound.");
                Assert.That(search.Solved, Is.True, entry.LevelId + " has no winning command sequence.");
                Assert.That(search.Commands.Count, Is.EqualTo(entry.IntendedSolution.Count),
                    entry.LevelId + " intended path is not a shortest solution.");

                var session = new GameSession(level);
                foreach (var command in entry.IntendedSolution)
                {
                    Assert.That(session.Engine.IsAvailable(session.State, command), Is.True,
                        entry.LevelId + " contains an unavailable command.");
                    session.Execute(command);
                    Assert.That(session.State.Result.Status, Is.Not.EqualTo(LevelStatus.Failed),
                        entry.LevelId + " failed while replaying its documented solution.");
                }
                Assert.That(session.State.Result.Status, Is.EqualTo(LevelStatus.Victory), entry.LevelId);
            }
        }

        [Test]
        public void SharedControlSolutionPulsesBothDivertersBeforeTheirSimultaneousDelivery()
        {
            var catalog = LevelCatalog.Parse(TestResources.Read("Levels/catalog.json"));
            var entry = catalog.Levels.Single(level => level.LevelId == "shared-control");
            var level = LevelJson.Parse(TestResources.Read(entry.Resource + ".json"));
            var session = new GameSession(level);
            session.Execute(SimulationCommand.PulseA);
            Assert.That(session.State.Switches.A, Is.True);
            Assert.That(session.Engine.Route(level.Nodes.Single(node => node.Id == "J1"), session.State.Switches), Is.EqualTo("R"));
            Assert.That(session.Engine.Route(level.Nodes.Single(node => node.Id == "J2"), session.State.Switches), Is.EqualTo("B"));
            var result = session.Execute(entry.IntendedSolution[1]);
            Assert.That(result.State.Result.Status, Is.EqualTo(LevelStatus.Victory));
            Assert.That(result.Events.Count(item => item.Type == EventType.Delivered), Is.EqualTo(2));
        }

        [Test]
        public void BoundedSolverReportsLimitAndCanonicalKeysPreserveDistinctStates()
        {
            var level = TestResources.Tutorial();
            var result = LevelSolver.Solve(level, 1);
            Assert.That(result.Solved, Is.False);
            Assert.That(result.LimitReached, Is.True);
            Assert.That(result.ExpandedStates, Is.EqualTo(1));

            var engine = new SimulationEngine(level);
            var initial = engine.InitialState();
            var moved = engine.Step(initial, SimulationCommand.Move).State;
            Assert.That(LevelSolver.CanonicalKey(initial), Is.Not.EqualTo(LevelSolver.CanonicalKey(moved)));
            Assert.That(LevelSolver.CanonicalKey(initial), Is.EqualTo(LevelSolver.CanonicalKey(engine.InitialState())));
        }
    }
}
