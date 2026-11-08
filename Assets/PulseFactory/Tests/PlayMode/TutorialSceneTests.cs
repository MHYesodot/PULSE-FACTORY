#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PulseFactory.Core;
using PulseFactory.Presentation;
using PulseFactory.Persistence;
using PulseFactory.UI;
using TMPro;
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
        private string screenshotDirectory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "pulse-factory-playmode-" + Guid.NewGuid().ToString("N"));
            savePath = Path.Combine(directory, "progress.json");
            statsPath = Path.Combine(directory, "playtest-stats.json");
            screenshotDirectory = Environment.GetEnvironmentVariable("PULSE_FACTORY_SCREENSHOTS");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
                Directory.CreateDirectory(screenshotDirectory);
            GameController.TestSavePath = savePath;
            GameController.TestStatsPath = statsPath;
        }
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

        private static void AssertBoardNodesUseCenteredAnchors(GameController controller)
        {
            var board = controller.UI.Board.transform.Find("Board viewport/Factory board");
            Assert.That(board, Is.Not.Null);
            foreach (Transform child in board)
            {
                if (!child.name.StartsWith("Node ", StringComparison.Ordinal)) continue;
                var rect = (RectTransform)child;
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)), child.name);
                Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)), child.name);
            }
        }

        private static void AssertLanguageLabels(GameController controller, string expected, bool rtl)
        {
            var labels = controller.UI.Canvas.GetComponentsInChildren<TMP_Text>(true)
                .Where(label => label.transform.parent != null && label.transform.parent.name == "Language")
                .ToArray();
            Assert.That(labels.Length, Is.EqualTo(2));
            foreach (var label in labels)
            {
                Assert.That(label.text, Is.EqualTo(expected));
                Assert.That(label.isRightToLeftText, Is.EqualTo(rtl));
            }
        }
        #endif

        private static IEnumerator Command(GameController controller, SimulationCommand command)
        {
            int previousBeat = controller.Session.State.Beat;
            Assert.That(controller.Execute(command), Is.True);
            Assert.That(controller.Session.State.Beat, Is.EqualTo(previousBeat + 1));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(controller.IsAnimating, Is.False);
        }

        private IEnumerator CaptureReviewScreen(string fileName)
        {
            if (string.IsNullOrWhiteSpace(screenshotDirectory)) yield break;
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(screenshotDirectory, fileName);
            var screenCapture = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("UnityEngine.ScreenCapture"))
                .FirstOrDefault(type => type != null);
            Assert.That(screenCapture, Is.Not.Null, "Unity's screen-capture module is unavailable.");
            var capture = screenCapture.GetMethod("CaptureScreenshot", new[] { typeof(string), typeof(int) });
            Assert.That(capture, Is.Not.Null, "Unity screenshot capture API is unavailable.");
            capture.Invoke(null, new object[] { path, 1 });
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(File.Exists(path), Is.True, "Unity did not create screenshot: " + path);
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

        [UnityTest]
        public IEnumerator PhaseFourAScreensShowLevelSelectionBilingualGameplayVictoryAndLevelTwo()
        {
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                Screen.SetResolution(540, 960, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            var controller = Controller();
            Assert.That(controller.Session, Is.Null);
            yield return CaptureReviewScreen("01-level-selection.png");

            Assert.That(controller.SelectLevel(1), Is.True);
            yield return null;
            AssertBoardNodesUseCenteredAnchors(controller);
            controller.ToggleLocale();
            Assert.That(controller.Locale, Is.EqualTo("he"));
            AssertLanguageLabels(controller, "English", false);
            yield return CaptureReviewScreen("02-gameplay-hebrew.png");

            controller.ToggleLocale();
            Assert.That(controller.Locale, Is.EqualTo("en"));
            AssertLanguageLabels(controller, "עברית", true);
            yield return CaptureReviewScreen("03-gameplay-english.png");

            yield return Command(controller, SimulationCommand.Move);
            yield return Command(controller, SimulationCommand.Move);
            yield return Command(controller, SimulationCommand.Move);
            yield return Command(controller, SimulationCommand.PulseA);
            Assert.That(controller.Session.State.Result.Status, Is.EqualTo(LevelStatus.Victory));
            Assert.That(controller.Session.State.Beat, Is.EqualTo(4));
            Assert.That(controller.Session.State.Deliveries.Select(item => item.DestinationId),
                Is.EqualTo(new[] { "R", "B" }));
            yield return new WaitForSecondsRealtime(0.25f);
            yield return CaptureReviewScreen("04-victory.png");

            controller.ReturnToLevelSelect();
            yield return CaptureReviewScreen("06-level-selection-progress.png");
            Assert.That(controller.SelectLevel(2), Is.True);
            Assert.That(controller.CurrentLevelNumber, Is.EqualTo(2));
            Assert.That(controller.Session.Engine.Level.Id, Is.EqualTo("shared-control"));
            yield return null;
            AssertBoardNodesUseCenteredAnchors(controller);
            var bridgeLayer = controller.UI.Board.transform.Find("Board viewport/Factory board/Raised crossover bridges");
            Assert.That(bridgeLayer, Is.Not.Null);
            Assert.That(bridgeLayer.childCount, Is.GreaterThanOrEqualTo(1),
                "Level 2's crossing routes must have a raised, non-junction bridge.");
            var sharedSwitches = controller.UI.Board.GetComponentsInChildren<DiverterView>(true);
            Assert.That(sharedSwitches.Length, Is.EqualTo(2));
            Assert.That(sharedSwitches.All(diverter => !diverter.SwitchIsOn), Is.True);
            controller.ToggleLocale();
            AssertLanguageLabels(controller, "English", false);
            yield return CaptureReviewScreen("09-level-two-hebrew.png");
            controller.ToggleLocale();
            AssertLanguageLabels(controller, "עברית", true);
            yield return CaptureReviewScreen("05-gameplay-level-two.png");
            Assert.That(controller.Execute(SimulationCommand.PulseA), Is.True);
            yield return new WaitForSecondsRealtime(0.06f);
            Assert.That(sharedSwitches.All(diverter => diverter.SwitchIsOn), Is.True,
                "Both Level 2 diverters must reflect the same Pulse A beat.");
            yield return CaptureReviewScreen("07-level-two-after-pulse.png");
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(controller.IsAnimating, Is.False);
            Assert.That(controller.Execute(SimulationCommand.Move), Is.True);
            yield return new WaitForSecondsRealtime(0.15f);
            yield return CaptureReviewScreen("08-package-moving.png");
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(controller.IsAnimating, Is.False);
            Assert.That(controller.Session.State.Result.Status, Is.EqualTo(LevelStatus.Victory));
            Assert.That(controller.Session.State.Deliveries.Select(item => item.DestinationId),
                Is.EquivalentTo(new[] { "R", "B" }));
        }

        [TestCase("English", false)]
        [TestCase("עברית", true)]
        [TestCase("LEVEL 2 · Shared control", false)]
        [TestCase("שלב 2 · שליטה משותפת", true)]
        public void TextDirectionFollowsFirstStrongCharacter(string value, bool expectedRtl)
        {
            Assert.That(UIFactory.IsRightToLeftContent(value, !expectedRtl), Is.EqualTo(expectedRtl));
        }
    }
}
