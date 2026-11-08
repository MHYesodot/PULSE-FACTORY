using System.Collections;
using NUnit.Framework;
using PulseFactory.Core;
using PulseFactory.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PulseFactory.Tests
{
    public sealed class TutorialSceneTests
    {
        [UnityTest]
        public IEnumerator SceneLoadsAndAnimationLocksInputThenUndoAndRestartRestoreBoardState()
        {
            yield return SceneManager.LoadSceneAsync("Tutorial", LoadSceneMode.Single);
            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Session, Is.Not.Null);
            Assert.That(controller.Session.State.Beat, Is.Zero);
            Assert.That(controller.Execute(SimulationCommand.Move), Is.True);
            Assert.That(controller.Session.State.Beat, Is.EqualTo(1), "Logic completes before animation.");
            Assert.That(controller.Session.State.Packages[0].NodeId, Is.EqualTo("J"));
            if (!controller.ReducedMotion)
            {
                Assert.That(controller.IsAnimating, Is.True);
                LogAssert.Expect(LogType.Warning, "Ignored unavailable simulation command: Move");
                Assert.That(controller.Execute(SimulationCommand.Move), Is.False);
                Assert.That(controller.Session.State.Beat, Is.EqualTo(1));
            }
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
    }
}
