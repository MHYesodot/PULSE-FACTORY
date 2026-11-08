using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using NUnit.Framework;
using PulseFactory.Localization;
using PulseFactory.Persistence;

namespace PulseFactory.Tests
{
    public sealed class ServicesTests
    {
        private string directory;
        private string path;
        private List<string> warnings;
        private SaveStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "pulse-factory-tests-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "progress.json");
            warnings = new List<string>();
            store = new SaveStore(path, warnings.Add);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void MissingSaveInitializesWithoutWarning()
        {
            var data = store.Load();
            Assert.That(data.HighestUnlockedLevel, Is.EqualTo(1));
            Assert.That(data.BestResults, Is.Empty);
            Assert.That(data.Locale, Is.EqualTo("en"));
            Assert.That(store.HadLoadError, Is.False);
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void SaveRoundTripPreservesSettingsAndBestStars()
        {
            var data = store.Load();
            data.RecordVictory("tutorial", 1, 3);
            data.RecordVictory("tutorial", 1, 1);
            data.Locale = "he";
            data.ReducedMotion = true;
            Assert.That(store.Save(data), Is.True);
            var loaded = store.Load();
            Assert.That(loaded.BestResults.Single().Stars, Is.EqualTo(3));
            Assert.That(loaded.HighestUnlockedLevel, Is.EqualTo(2));
            Assert.That(loaded.Locale, Is.EqualTo("he"));
            Assert.That(loaded.ReducedMotion, Is.True);
            Assert.That(store.Save(loaded), Is.True);
            Assert.That(File.Exists(path + ".bak"), Is.True);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
            Assert.That(warnings, Is.Empty);
        }

        [TestCase("invalid json")]
        [TestCase("{}")]
        [TestCase("null")]
        [TestCase("{\"SchemaVersion\":999,\"HighestUnlockedLevel\":1,\"BestResults\":[],\"Locale\":\"en\",\"ReducedMotion\":false}")]
        [TestCase("{\"SchemaVersion\":1,\"HighestUnlockedLevel\":0,\"BestResults\":[],\"Locale\":\"en\",\"ReducedMotion\":false}")]
        public void CorruptOrIncompatibleSaveReportsErrorAndRetainsOriginal(string contents)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, contents);
            var loaded = store.Load();
            Assert.That(loaded.HighestUnlockedLevel, Is.EqualTo(1));
            Assert.That(loaded.BestResults, Is.Empty);
            Assert.That(store.HadLoadError, Is.True);
            Assert.That(warnings.Count, Is.EqualTo(1));
            Assert.That(File.ReadAllText(path), Is.EqualTo(contents));
            Assert.That(store.Save(loaded), Is.True);
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(contents));
        }

        [Test]
        public void StorageFailureIsReportedRatherThanClaimingSuccess()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "not-a-directory"), "blocker");
            var blocked = new SaveStore(Path.Combine(directory, "not-a-directory", "save.json"), warnings.Add);
            Assert.That(blocked.Save(new SaveData()), Is.False);
            Assert.That(warnings, Is.Not.Empty);
        }

        [Test]
        public void InvalidSaveCannotBeWritten()
        {
            var data = new SaveData { Locale = "unsupported" };
            Assert.Throws<SerializationException>(() => store.Save(data));
            Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        public void LocaleKeysAndPlaceholdersAreSynchronized()
        {
            var en = new LocalizationCatalog(TestResources.Read("Localization/en.json"));
            var he = new LocalizationCatalog(TestResources.Read("Localization/he.json"));
            Assert.DoesNotThrow(() => en.ValidateKeys(he));
            foreach (var key in en.Keys)
            {
                Assert.That(en.Display(key, 1, 2), Is.Not.Empty);
                Assert.That(he.Display(key, 1, 2), Is.Not.Empty);
            }
            Assert.That(en.RightToLeft, Is.False);
            Assert.That(he.RightToLeft, Is.True);
            Assert.That(en.Display("beat", 4, 6), Is.EqualTo("Beat 4 / 6"));
            Assert.Throws<KeyNotFoundException>(() => en.Text("missing"));
        }

        [Test]
        public void HebrewVisualOrderPreservesNumbersAndLatinTokens()
        {
            Assert.That(LocalizationCatalog.HebrewVisualOrder("פעימה 12"), Is.EqualTo("12 המיעפ"));
            Assert.That(LocalizationCatalog.HebrewVisualOrder("פעימה PULSE"), Is.EqualTo("PULSE המיעפ"));
            Assert.That(LocalizationCatalog.HebrewVisualOrder("אב\nגד"), Is.EqualTo("בא\nדג"));
        }

        [Test]
        public void HebrewWrapsLogicalLinesBeforeVisualOrdering()
        {
            var he = new LocalizationCatalog(TestResources.Read("Localization/he.json"));
            Assert.That(he.DisplayWrapped("tutorial", line => line.Length <= 10),
                Is.EqualTo(":הכרדה\nםודא בותינ\nלוחכו"));
        }

        [Test]
        public void MissingLocaleKeyIsRejected()
        {
            var en = new LocalizationCatalog(TestResources.Read("Localization/en.json"));
            var broken = new LocalizationCatalog(TestResources.Read("Localization/he.json").Replace("\"key\": \"move\"", "\"key\": \"other\""));
            Assert.Throws<SerializationException>(() => en.ValidateKeys(broken));
        }
    }
}
