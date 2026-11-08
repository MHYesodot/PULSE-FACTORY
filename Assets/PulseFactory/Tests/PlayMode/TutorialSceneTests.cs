#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PulseFactory.Core;
using PulseFactory.Persistence;
using PulseFactory.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PulseFactory.Tests
{
    public sealed class TutorialSceneTests
    {
        private string directory;
        private string savePath;
        private string statsPath;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "pulse-factory-playmode-" + Guid.NewGuid().ToString("N"));
            savePath = Path.Combine(directory, "progress.json");
            statsPath = Path.Combine(directory, "playtest-stats.json");
            GameController.TestSavePath = savePath;
            GameController.TestStatsPath = statsPath;
        }
        #endif

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var controller in UnityEngine.Object.FindObjectsByType<GameController>(FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(controller.gameObject);
            yield return null;
            GameController.TestSavePath = null;
            GameController.TestStatsPath = null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static GameController Controller()
        {
            var controllers = UnityEngine.Object.FindObjectsByType<GameController>(FindObjectsSortMode.None);
            Assert.That(controllers.Length, Is.EqualTo(1));
            return controllers[0];
        }

        private static IEnumerator Command(GameController controller, SimulationCommand command)
        {
            int previousBeat = controller.Session.State.Beat;
            Assert.That(controller.Execute(command), Is.True);
            Assert.That(controller.Session.State.Beat, Is.EqualTo(previousBeat + 1));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(controller.IsAnimating, Is.False);
        }

        [UnityTest]
        public IEnumerator SceneLoadsAndAnimationLocksInputThenUndoAndRestartRestoreBoardState()
        {
            yield return SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            var controller = Controller();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Session, Is.Null, "The scene opens at level selection.");
            Assert.That(controller.UI.Canvas, Is.Not.Null);
            Assert.That(controller.SelectLevel(1), Is.True);
            Assert.That(controller.Session, Is.Not.Null);
            Assert.That(controller.Session.State.Beat, Is.Zero);
            controller.UI.Controls.CommandButton(SimulationCommand.Move).onClick.Invoke();
            Assert.That(controller.Session.State.Beat, Is.EqualTo(1), "Logic completes before animation.");
            Assert.That(controller.Session.State.Packages[0].NodeId, Is.EqualTo("J"));
            Assert.That(controller.ReducedMotion, Is.False);
            Assert.That(controller.IsAnimating, Is.True);
            LogAssert.Expect(LogType.Warning, "Ignored unavailable simulation command: Move");
            Assert.That(controller.Execute(SimulationCommand.Move), Is.False);
            LogAssert.Expect(LogType.Warning, "Ignored unavailable restart.");
            controller.Restart();
            LogAssert.Expect(LogType.Warning, "Ignored unavailable undo.");
            controller.Undo();
            Assert.That(controller.Session.State.Beat, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(controller.IsAnimating, Is.False);
            controller.Undo();
            Assert.That(controller.Session.State.Beat, Is.Zero);
            Assert.That(controller.Session.State.Packages, Is.Empty);
            Assert.That(controller.Execute(SimulationCommand.Move), Is.True);
            yield return new WaitForSecondsRealtime(0.4f);
            controller.Restart();
            Assert.That(controller.Session.State.Beat, Is.Zero);
            Assert.That(controller.Session.CanUndo, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TutorialWinsOnBeatFourRejectsTerminalInputAndPersistsAcrossSceneReload()
        {
            yield return SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            var controller = Controller();
            Assert.That(controller.SelectLevel(1), Is.True);
            LogAssert.Expect(LogType.Warning, "Ignored unavailable simulation command: PulseB");
            Assert.That(controller.Execute(SimulationCommand.PulseB), Is.False);
            Assert.That(controller.Session.State.Beat, Is.Zero);
            yield return Command(controller, SimulationCommand.Move);
            yield return Command(controller, SimulationCommand.Move);
            Assert.That(controller.Session.State.Deliveries.Single().DestinationId, Is.EqualTo("R"));
            yield return Command(controller, SimulationCommand.Move);
            var beforeVictory = controller.Session.State;
            yield return Command(controller, SimulationCommand.PulseA);
            Assert.That(controller.Session.State.Result.Status, Is.EqualTo(LevelStatus.Victory));
            Assert.That(controller.Session.State.Beat, Is.EqualTo(4));
            Assert.That(controller.Session.State.Result.Stars, Is.EqualTo(3));
            Assert.That(controller.Session.State.Deliveries.Select(d => d.DestinationId), Is.EqualTo(new[] { "R", "B" }));
            LogAssert.Expect(LogType.Warning, "Ignored unavailable simulation command: Move");
            Assert.That(controller.Execute(SimulationCommand.Move), Is.False);
            controller.Undo();
            Assert.That(controller.Session.State, Is.SameAs(beforeVictory));
            Assert.That(controller.IsAnimating, Is.False);
            var saved = new SaveStore(savePath, message => Assert.Fail(message)).Load();
            Assert.That(saved.HighestUnlockedLevel, Is.EqualTo(2));
            Assert.That(saved.BestResults.Single().Stars, Is.EqualTo(3));
            var playtest = new PlaytestStatsStore(statsPath, message => Assert.Fail(message)).Data.Levels.Single();
            Assert.That(playtest.Starts, Is.EqualTo(1));
            Assert.That(playtest.Commands, Is.EqualTo(4));
            Assert.That(playtest.Completions, Is.EqualTo(1));
            yield return SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            controller = Controller();
            Assert.That(controller.SelectLevel(1), Is.True);
            Assert.That(controller.Session.State.Beat, Is.Zero);
            Assert.That(new SaveStore(savePath, message => Assert.Fail(message)).Load().BestResults.Single().Stars, Is.EqualTo(3));
            yield return Command(controller, SimulationCommand.Move);
            controller.Restart();
            Assert.That(controller.Session.State.Beat, Is.Zero);
            Assert.That(controller.Session.State.Packages, Is.Empty);
            Assert.That(controller.Session.State.Deliveries, Is.Empty);
            Assert.That(controller.Session.State.Switches.A, Is.False);
        }

        [UnityTest]
        public IEnumerator CompletingTutorialUnlocksAndStartsTheNextAuthoredLevel()
        {
            yield return SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            var controller = Controller();
            Assert.That(controller.SelectLevel(1), Is.True);
            yield return Command(controller, SimulationCommand.Move);
            yield return Command(controller, SimulationCommand.Move);
            yield return Command(controller, SimulationCommand.Move);
            yield return Command(controller, SimulationCommand.PulseA);

            Assert.That(controller.Session.State.Result.Status, Is.EqualTo(LevelStatus.Victory));
            Assert.That(controller.SelectLevel(2), Is.True);
            Assert.That(controller.Session.Engine.Level.Id, Is.EqualTo("shared-control"));
            yield return Command(controller, SimulationCommand.PulseA);
            yield return Command(controller, SimulationCommand.Move);
            Assert.That(controller.Session.State.Result.Status, Is.EqualTo(LevelStatus.Victory));
            Assert.That(new SaveStore(savePath, message => Assert.Fail(message)).Load().HighestUnlockedLevel,
                Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator WrongRouteFailsAndUndoAllowsCorrection()
        {
            yield return SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            var controller = Controller();
            Assert.That(controller.SelectLevel(1), Is.True);
            for (int i = 0; i < 3; i++) yield return Command(controller, SimulationCommand.Move);
            var previous = controller.Session.State;
            yield return Command(controller, SimulationCommand.Move);
            Assert.That(controller.Session.State.Result.Failure, Is.EqualTo(FailureReason.WrongDestination));
            Assert.That(controller.Session.State.Packages.Single().NodeId, Is.EqualTo("J"));
            Assert.That(controller.Session.State.Deliveries.Count, Is.EqualTo(1));
            Assert.That(controller.IsAnimating, Is.False);
            LogAssert.Expect(LogType.Warning, "Ignored unavailable simulation command: PulseA");
            Assert.That(controller.Execute(SimulationCommand.PulseA), Is.False);
            controller.Undo();
            Assert.That(controller.Session.State, Is.SameAs(previous));
            yield return Command(controller, SimulationCommand.PulseA);
            Assert.That(controller.Session.State.Result.Status, Is.EqualTo(LevelStatus.Victory));
        }

        [UnityTest]
        public IEnumerator SavedHebrewAndReducedMotionSettingsSurviveReloadWithoutRenderingErrors()
        {
            var store = new SaveStore(savePath, message => Assert.Fail(message));
            Assert.That(store.Save(new SaveData { Locale = "he", ReducedMotion = true }), Is.True);
            yield return SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            var controller = Controller();
            Assert.That(controller.SelectLevel(1), Is.True);
            Assert.That(controller.ReducedMotion, Is.True);
            Assert.That(controller.Execute(SimulationCommand.Move), Is.True);
            yield return null;
            Assert.That(controller.IsAnimating, Is.False);
            yield return null;
            yield return SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            Assert.That(Controller().ReducedMotion, Is.True);
            Assert.That(store.Load().Locale, Is.EqualTo("he"));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CorruptSaveWarnsButDoesNotPreventGameplay()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(savePath, "broken json");
            LogAssert.Expect(LogType.Warning, new Regex("^Save could not be loaded;"));
            yield return SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            var controller = Controller();
            Assert.That(controller.SelectLevel(1), Is.True);
            Assert.That(controller.Session.State.Beat, Is.Zero);
            Assert.That(File.ReadAllText(savePath), Is.EqualTo("broken json"));
            yield return Command(controller, SimulationCommand.Move);
            Assert.That(controller.Session.State.Packages.Single().NodeId, Is.EqualTo("J"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
