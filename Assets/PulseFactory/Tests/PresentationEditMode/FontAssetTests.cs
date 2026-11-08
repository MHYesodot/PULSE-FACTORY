using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace PulseFactory.Tests
{
    public sealed class FontAssetTests
    {
        [Test]
        public void PackagedStaticFontContainsTheBilingualInterfaceGlyphs()
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts/NotoSansHebrew SDF");
            Assert.That(font, Is.Not.Null);
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(font.atlasTextures, Is.Not.Empty);
            foreach (char character in "PULSEFACTORY MOVE English עברית אבהגדהוזחטיךכלםמןנסעףפץצקרשת0123456789+/X")
                Assert.That(font.characterTable.Any(item => item.unicode == character), Is.True,
                    "Missing character U+" + ((int)character).ToString("X4"));
        }
    }
}
