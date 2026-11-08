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
        public void HebrewLocalizationPreservesLogicalTextForTextMeshProsRtlLayout()
        {
            var he = new LocalizationCatalog(TestResources.Read("Localization/he.json"));
            Assert.That(he.Display("beat", 4, 6), Is.EqualTo("פעימה 4 / 6"));
            Assert.That(he.Display("switches", 1, 0), Is.EqualTo("א: 1     ב: 0"));
            Assert.That(he.RightToLeft, Is.True);
        }

        [Test]
        public void HebrewWrapPreservesLogicalTextAndLineOrder()
        {
            var he = new LocalizationCatalog(TestResources.Read("Localization/he.json"));
            Assert.That(he.DisplayWrapped("tutorial", line => line.Length <= 10),
                Is.EqualTo("הדרכה:\nניתוב אדום\nוכחול"));
        }

        [Test]
        public void MissingLocaleKeyIsRejected()
        {
            var en = new LocalizationCatalog(TestResources.Read("Localization/en.json"));
            var broken = new LocalizationCatalog(TestResources.Read("Localization/he.json").Replace("\"key\": \"move\"", "\"key\": \"other\""));
            Assert.Throws<SerializationException>(() => en.ValidateKeys(broken));
        }

        [Test]
        public void RequiredKeyMissingFromBothLocalesIsRejectedBeforeRendering()
        {
            var en = new LocalizationCatalog(TestResources.Read("Localization/en.json").Replace("\"key\": \"move\"", "\"key\": \"other\""));
            var he = new LocalizationCatalog(TestResources.Read("Localization/he.json").Replace("\"key\": \"move\"", "\"key\": \"other\""));
            Assert.DoesNotThrow(() => en.ValidateKeys(he));
            Assert.Throws<SerializationException>(() => en.ValidateRequiredKeys("move", "loadError"));
        }

        [Test]
        public void LevelCatalogLocalizationKeysAndInstructionPlaceholdersAreSynchronized()
        {
            var catalog = PulseFactory.Core.LevelCatalog.Parse(TestResources.Read("Levels/catalog.json"));
            var en = new LocalizationCatalog(TestResources.Read("Localization/en.json"));
            var he = new LocalizationCatalog(TestResources.Read("Localization/he.json"));
            en.ValidateKeys(he);
            foreach (var level in catalog.Levels)
            {
                Assert.DoesNotThrow(() => en.ValidateRequiredKeys(level.TitleKey, level.InstructionKey));
                Assert.That(en.Text(level.TitleKey), Is.Not.Empty);
                Assert.That(he.Text(level.TitleKey), Is.Not.Empty);
                Assert.That(en.Text(level.InstructionKey), Is.Not.Empty);
                Assert.That(he.Text(level.InstructionKey), Is.Not.Empty);
            }
        }

        [Test]
        public void AnonymousPlaytestCountersPersistAndExportWithoutAnIdentifier()
        {
            var statsPath = Path.Combine(directory, "stats.json");
            var stats = new PlaytestStatsStore(statsPath, warnings.Add);
            stats.RecordStart("tutorial");
            stats.RecordCommand("tutorial", false);
            stats.RecordCommand("tutorial", true);
            stats.RecordUndo("tutorial");
            stats.RecordRestart("tutorial");
            stats.RecordCompletion("tutorial", 12.5);
            var loaded = new PlaytestStatsStore(statsPath, warnings.Add).Data;
            var level = loaded.Levels.Single();
            Assert.That(loaded.SchemaVersion, Is.EqualTo(1));
            Assert.That(level.LevelId, Is.EqualTo("tutorial"));
            Assert.That(level.Starts, Is.EqualTo(1));
            Assert.That(level.Completions, Is.EqualTo(1));
            Assert.That(level.Commands, Is.EqualTo(2));
            Assert.That(level.FailedAttempts, Is.EqualTo(1));
            Assert.That(level.UndoUses, Is.EqualTo(1));
            Assert.That(level.Restarts, Is.EqualTo(1));
            Assert.That(level.TotalCompletionSeconds, Is.EqualTo(12.5));
            string exportDirectory = Path.Combine(directory, "exports");
            string exportPath = stats.Export(exportDirectory);
            Assert.That(File.Exists(exportPath), Is.True);
            string json = File.ReadAllText(exportPath);
            Assert.That(json, Does.Contain("FailedAttempts"));
            Assert.That(json.ToLowerInvariant(), Does.Not.Contain("sessionid"));
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void CorruptPlaytestStatisticsAreReportedAndFreshDataIsUsed()
        {
            Directory.CreateDirectory(directory);
            string statsPath = Path.Combine(directory, "stats.json");
            File.WriteAllText(statsPath, "broken stats");
            var stats = new PlaytestStatsStore(statsPath, warnings.Add);
            Assert.That(stats.Data.Levels, Is.Empty);
            Assert.That(warnings.Count, Is.EqualTo(1));
            Assert.That(File.ReadAllText(statsPath), Is.EqualTo("broken stats"));
            stats.RecordStart("tutorial");
            Assert.That(File.ReadAllText(statsPath + ".bak"), Is.EqualTo("broken stats"));
        }
    }
}
