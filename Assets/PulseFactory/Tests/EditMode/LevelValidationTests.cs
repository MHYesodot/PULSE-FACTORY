using System;
using System.Linq;
using NUnit.Framework;
using PulseFactory.Core;

namespace PulseFactory.Tests
{
    public sealed class LevelValidationTests
    {
        [TestCase("\"schemaVersion\": 1", "\"schemaVersion\": 2")]
        [TestCase("\"maxBeats\": 6", "\"maxBeats\": 0")]
        [TestCase("\"threeStarBeats\": 4", "\"threeStarBeats\": 6")]
        [TestCase("\"id\": \"J\", \"type\"", "\"id\": \"S\", \"type\"")]
        [TestCase("\"to\": \"J\"", "\"to\": \"missing\"")]
        [TestCase("\"group\": \"A\"", "\"group\": \"C\"")]
        [TestCase("\"group\": \"A\"", "\"group\": \"0\"")]
        [TestCase("\"group\": \"A\",", "")]
        [TestCase("\"trueEdge\": \"JB\"", "\"trueEdge\": \"JR\"")]
        [TestCase("\"trueEdge\": \"JB\"", "\"trueEdge\": \"SJ\"")]
        [TestCase("\"beat\": 1", "\"beat\": 0")]
        [TestCase("\"beat\": 3", "\"beat\": 7")]
        [TestCase("\"beat\": 3", "\"beat\": 1")]
        [TestCase("\"source\": \"S\"", "\"source\": \"J\"")]
        [TestCase("\"id\": \"blue-1\"", "\"id\": \"red-1\"")]
        [TestCase("\"kind\": \"blue\"", "\"kind\": \"green\"")]
        [TestCase("\"type\": \"Source\"", "\"type\": \"Unknown\"")]
        [TestCase("\"requiredCount\": 1", "\"requiredCount\": 0")]
        [TestCase("\"id\": \"JB\",", "\"id\": \"JR\",")]
        [TestCase("\"x\": 0.5,", "")]
        public void MalformedDefinitionsAreRejected(string original, string replacement)
        {
            string json = TestResources.Read("Levels/tutorial.json");
            Assert.That(json.Contains(original), Is.True, "Mutation must actually alter the fixture.");
            Assert.Throws<LevelValidationException>(() => LevelJson.Parse(json.Replace(original, replacement)));
        }

        [TestCase("")]
        [TestCase("{}")]
        [TestCase("null")]
        [TestCase("{ broken json")]
        public void MissingOrMalformedJsonFailsClearly(string json) =>
            Assert.Throws<LevelValidationException>(() => LevelJson.Parse(json));

        [Test]
        public void CycleIsRejected()
        {
            var level = TestResources.Tutorial();
            var nodes = level.Nodes.Concat(new[]
            {
                new NodeDefinition("C1", NodeType.Conveyor, 1, 1),
                new NodeDefinition("C2", NodeType.Conveyor, 2, 1)
            });
            var edges = level.Edges.Concat(new[] { new EdgeDefinition("cycle1", "C1", "C2"), new EdgeDefinition("cycle2", "C2", "C1") });
            var error = Assert.Throws<LevelValidationException>(() =>
                new LevelDefinition("cycle", 1, nodes, edges, level.Spawns, level.InitialSwitches, 6, 4, 5));
            Assert.That(error.Message, Does.Contain("cycle"));
        }

        [Test]
        public void MissingDestinationsAreRejected()
        {
            var error = Assert.Throws<LevelValidationException>(() => new LevelDefinition("no-destinations", 1,
                new[] { new NodeDefinition("C", NodeType.Conveyor, 0, 0) },
                new[] { new EdgeDefinition("loop", "C", "C") }, Array.Empty<PackageSpawn>(),
                new SwitchState(false, false), 1, 1, 1));
            Assert.That(error.Message, Does.Contain("destination"));
        }

        [Test]
        public void DefinitionsDefensivelyCopyCollections()
        {
            var source = TestResources.Tutorial();
            var nodes = source.Nodes.ToArray();
            var level = new LevelDefinition(source.Id, 1, nodes, source.Edges, source.Spawns, source.InitialSwitches, 6, 4, 5);
            nodes[0] = null;
            Assert.That(level.Nodes[0].Id, Is.EqualTo("S"));
        }
    }
}
