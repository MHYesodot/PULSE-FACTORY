#if UNITY_EDITOR
using NUnit.Framework;
using PulseFactory.Presentation;
using PulseFactory.UI;
using UnityEngine;

namespace PulseFactory.Tests
{
    public sealed class PresentationTests
    {
        [TestCase(480, 900)]
        [TestCase(1600, 900)]
        [TestCase(1080, 1920)]
        public void DiagonalLinesStayAttachedToTheirNodesInsideTransformedBoard(int width, int height)
        {
            var viewport = GameController.FitPortraitViewport(new Rect(0, 0, width, height), height);
            float scale = viewport.width / 480f;
            var parent = Matrix4x4.TRS(new Vector3(viewport.x, viewport.y, 0), Quaternion.identity,
                new Vector3(scale, scale, 1));
            var start = new Vector2(240, 327);
            foreach (var end in new[] { new Vector2(75, 438), new Vector2(405, 438), new Vector2(240, 228) })
            {
                var matrix = BoardRenderer.LineMatrix(parent, start, end);
                var actualStart = matrix.MultiplyPoint3x4(Vector3.zero);
                var actualEnd = matrix.MultiplyPoint3x4(new Vector3(Vector2.Distance(start, end), 0, 0));
                Assert.That(Vector3.Distance(actualStart, parent.MultiplyPoint3x4(start)), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(actualEnd, parent.MultiplyPoint3x4(end)), Is.LessThan(0.001f));
                Assert.That(actualStart.y, Is.LessThan(viewport.y + 515 * scale));
                Assert.That(actualEnd.y, Is.LessThan(viewport.y + 515 * scale),
                    "Conveyor geometry must not extend down into the controls.");
            }
        }

        [Test]
        public void NineBySixteenViewportUsesFullHeightAndAtLeastNinetyFourPercentWidth()
        {
            var viewport = GameController.FitPortraitViewport(new Rect(0, 0, 1080, 1920), 1920);
            Assert.That(viewport.height, Is.EqualTo(1920).Within(0.01));
            Assert.That(viewport.width / 1080, Is.GreaterThan(0.94f));
            Assert.That(viewport.xMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(viewport.xMax, Is.LessThanOrEqualTo(1080));
            Assert.That(viewport.y, Is.Zero.Within(0.01));
        }

        [Test]
        public void PortraitLayoutRespectsAsymmetricSafeAreaAndCentersInWideWindows()
        {
            var safe = new Rect(0, 102, 1179, 2316);
            var viewport = GameController.FitPortraitViewport(safe, 2556);
            Assert.That(viewport.xMin, Is.GreaterThanOrEqualTo(safe.xMin));
            Assert.That(viewport.xMax, Is.LessThanOrEqualTo(safe.xMax));
            Assert.That(viewport.yMin, Is.GreaterThanOrEqualTo(2556 - safe.yMax));
            Assert.That(viewport.yMax, Is.LessThanOrEqualTo(2556 - safe.yMin));
            var wide = GameController.FitPortraitViewport(new Rect(0, 0, 1600, 900), 900);
            Assert.That(wide, Is.EqualTo(new Rect(560, 0, 480, 900)));
        }
    }
}
#endif
