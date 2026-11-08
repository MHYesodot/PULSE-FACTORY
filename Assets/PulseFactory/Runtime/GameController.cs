using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using PulseFactory.Core;
using PulseFactory.Localization;
using PulseFactory.Persistence;
using PulseFactory.Presentation;
using TMPro;
using UnityEngine;

namespace PulseFactory.UI
{
    public sealed class GameController : MonoBehaviour
    {
        private LocalizationCatalog english;
        private LocalizationCatalog hebrew;
        private LocalizationCatalog text;
        private LevelCatalog catalog;
        private LevelDefinition[] levels;
        private SaveStore saveStore;
        private SaveData save;
        private PlaytestStatsStore stats;
        private TMP_FontAsset font;
        private GameUI ui;
        private LevelCatalogEntry currentEntry;
        private float levelStartedAt;
        private bool animationRunning;
        private bool loadFailed;
        private string warningKey;
        private string exportResultKey = "statsDisclaimer";
        private const float PackageAnimationSeconds = 0.34f;

        public GameSession Session { get; private set; }
        public bool IsAnimating => animationRunning;
        public bool ReducedMotion => save != null && save.ReducedMotion;
        public string Locale => text?.Locale ?? "en";
        public int CurrentLevelNumber => currentEntry?.Number ?? 0;
        public GameUI UI => ui;

#if UNITY_EDITOR
        internal static string TestSavePath { get; set; }
        internal static string TestStatsPath { get; set; }
#endif

        private void Awake()
        {
            try
            {
                LoadContent();
                ConfigureLocalStores();
                CreateUI();
                RefreshMenu();
            }
            catch (Exception ex) when (ex is LevelValidationException || ex is SerializationException
                || ex is IOException || ex is ArgumentException || ex is InvalidOperationException
                || ex is KeyNotFoundException)
            {
                loadFailed = true;
                Debug.LogError("Pulse Factory initialization failed: " + ex);
            }
        }

        private void LoadContent()
        {
            english = new LocalizationCatalog(Resource("Localization/en"));
            hebrew = new LocalizationCatalog(Resource("Localization/he"));
            english.ValidateKeys(hebrew);
            english.ValidateRequiredKeys("title", "menuSubtitle", "levelSelect", "levelCounter", "playtestLevel",
                "levelSummary", "locked", "playtestExport", "exportComplete", "exportFailed", "statsDisclaimer",
                "move", "pulseA", "pulseB", "undo", "restart", "back", "nextLevel", "playAgain", "tryAgain",
                "motionOn", "motionOff", "languageEnglish", "languageHebrew", "beat", "switches", "progress",
                "best", "playing", "animating", "victory", "levelComplete", "allComplete", "failedTitle",
                "failureCollision", "failureWrongDestination", "failureDestinationFull", "failureBeatLimit",
                "switchOn", "switchOff", "switchA", "switchB", "source", "nodeSource", "nodeConveyor",
                "nodeDiverter", "nodeDestination", "red", "blue", "redSymbol", "blueSymbol", "sourceIcon", "saveWarning");

            catalog = LevelCatalog.Parse(Resource("Levels/catalog"));
            levels = catalog.Levels.Select(entry =>
            {
                var definition = LevelJson.Parse(Resource(entry.Resource));
                if (!string.Equals(definition.Id, entry.LevelId, StringComparison.Ordinal))
                    throw new LevelValidationException("Catalog ID does not match level resource: " + entry.Resource);
                return definition;
            }).ToArray();
            foreach (var entry in catalog.Levels)
                english.ValidateRequiredKeys(entry.TitleKey, entry.InstructionKey);

            font = Resources.Load<TMP_FontAsset>("Fonts/NotoSansHebrew SDF");
            if (font == null)
                throw new InvalidOperationException("Missing packaged bilingual TMP font at Resources/Fonts/NotoSansHebrew SDF.asset.");
        }

        private void ConfigureLocalStores()
        {
            text = english;
            string saveName = Application.isBatchMode ? "progress-batch.json" : "progress.json";
            string statsName = Application.isBatchMode ? "playtest-stats-batch.json" : "playtest-stats.json";
            string savePath = Path.Combine(Application.persistentDataPath, saveName);
            string statsPath = Path.Combine(Application.persistentDataPath, statsName);
#if UNITY_EDITOR
            savePath = TestSavePath ?? savePath;
            statsPath = TestStatsPath ?? statsPath;
#endif
            saveStore = new SaveStore(savePath, Debug.LogWarning);
            save = saveStore.Load();
            stats = new PlaytestStatsStore(statsPath, Debug.LogWarning);
            if (saveStore.HadLoadError) warningKey = "saveWarning";
            text = save.Locale == "he" ? hebrew : english;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        private void CreateUI()
        {
            var root = new GameObject("Pulse Factory UI", typeof(RectTransform));
            ui = root.AddComponent<GameUI>();
            ui.Build(font, text, catalog, levels, number => { SelectLevel(number); },
                command => { Execute(command); }, Undo, Restart,
                ReturnToLevelSelect, ToggleReducedMotion, ToggleLocale, () => { ExportPlaytestData(); },
                NextLevel, Restart);
            ui.UpdateLocale(text);
            RefreshMenu();
        }

        private static string Resource(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException("Missing resource: " + path);
            return asset.text;
        }

        public bool SelectLevel(int number)
        {
            if (loadFailed || animationRunning) return false;
            if (number < 1 || number > catalog.Levels.Count)
                throw new ArgumentOutOfRangeException(nameof(number));
            if (number > save.HighestUnlockedLevel)
            {
                Debug.LogWarning("Ignored locked level selection: " + number);
                return false;
            }

            currentEntry = catalog.Levels[number - 1];
            Session = new GameSession(levels[number - 1]);
            levelStartedAt = Time.realtimeSinceStartup;
            stats.RecordStart(currentEntry.LevelId);
            ui.ShowGameplay();
            ui.SetLevel(Session.Engine, Session.State);
            warningKey = saveStore.HadLoadError ? "saveWarning" : null;
            RefreshGameplay();
            return true;
        }

        public bool Execute(SimulationCommand command)
        {
            if (loadFailed || Session == null || IsAnimating
                || !Session.Engine.IsAvailable(Session.State, command))
            {
                Debug.LogWarning("Ignored unavailable simulation command: " + command);
                return false;
            }

            var result = Session.Execute(command);
            stats.RecordCommand(currentEntry.LevelId, result.State.Result.Status == LevelStatus.Failed);
            if (result.State.Result.Status == LevelStatus.Victory)
            {
                save.RecordVictory(Session.Engine.Level.Id, currentEntry.Number, result.State.Result.Stars);
                Persist();
                stats.RecordCompletion(currentEntry.LevelId,
                    Math.Max(0, Time.realtimeSinceStartup - levelStartedAt));
            }
            animationRunning = true;
            RefreshGameplay("animating");
            StartCoroutine(Animate(result));
            return true;
        }

        private IEnumerator Animate(SimulationResult result)
        {
            yield return ui.Board.Present(result, ReducedMotion ? 0 : PackageAnimationSeconds);
            animationRunning = false;
            RefreshGameplay();
            if (result.State.Result.Status == LevelStatus.Victory)
            {
                bool finalLevel = currentEntry.Number == catalog.Levels.Count;
                ui.ShowVictory(result.State.Result.Stars, finalLevel, !finalLevel);
            }
            else if (result.State.Result.Status == LevelStatus.Failed)
                ui.ShowFailure("failure" + result.State.Result.Failure);
        }

        public void Undo()
        {
            if (Session == null || IsAnimating || !Session.CanUndo)
            {
                Debug.LogWarning("Ignored unavailable undo.");
                return;
            }
            Session.Undo();
            stats.RecordUndo(currentEntry.LevelId);
            ui.Outcome.Hide();
            ui.Board.SetLevel(Session.Engine, Session.State);
            RefreshGameplay();
        }

        public void Restart()
        {
            if (Session == null || IsAnimating)
            {
                Debug.LogWarning("Ignored unavailable restart.");
                return;
            }
            Session.Restart();
            levelStartedAt = Time.realtimeSinceStartup;
            stats.RecordRestart(currentEntry.LevelId);
            ui.Outcome.Hide();
            ui.Board.SetLevel(Session.Engine, Session.State);
            RefreshGameplay();
        }

        public void ReturnToLevelSelect()
        {
            if (IsAnimating) return;
            Session = null;
            currentEntry = null;
            ui.ShowLevelSelect();
            RefreshMenu();
        }

        public void ToggleLocale()
        {
            text = text.Locale == "en" ? hebrew : english;
            save.Locale = text.Locale;
            Persist();
            ui.UpdateLocale(text);
            RefreshMenu();
            RefreshGameplay();
        }

        public void ToggleReducedMotion()
        {
            save.ReducedMotion = !save.ReducedMotion;
            Persist();
            RefreshMenu();
            RefreshGameplay();
        }

        public bool ExportPlaytestData()
        {
            string directory = Path.Combine(Application.persistentDataPath, "exports");
            string path = stats.Export(directory);
            exportResultKey = path == null ? "exportFailed" : "exportComplete";
            if (path != null) Debug.Log("Anonymous playtest data exported to " + path);
            ui.SetExportResult(exportResultKey);
            RefreshMenu();
            return path != null;
        }

        private void OnDestroy()
        {
            if (ui == null) return;
            if (Application.isPlaying) Destroy(ui.gameObject);
            else DestroyImmediate(ui.gameObject);
        }

        private void NextLevel()
        {
            if (currentEntry == null || currentEntry.Number >= catalog.Levels.Count) return;
            SelectLevel(currentEntry.Number + 1);
        }

        private void Persist()
        {
            if (!saveStore.Save(save)) warningKey = "saveWarning";
            else if (warningKey == "saveWarning") warningKey = null;
        }

        private void RefreshMenu()
        {
            if (ui != null && save != null)
                ui.UpdateMenu(save, ReducedMotion, warningKey ?? exportResultKey);
        }

        private void RefreshGameplay(string overrideKey = null)
        {
            if (ui == null || Session == null || currentEntry == null) return;
            string key = overrideKey ?? FeedbackKey();
            object[] args = Session.State.Result.Status == LevelStatus.Victory
                ? new object[] { Session.State.Result.Stars }
                : Array.Empty<object>();
            int stars = save.BestResults.FirstOrDefault(item => item.LevelId == currentEntry.LevelId)?.Stars ?? 0;
            ui.UpdateGameplay(currentEntry, Session.Engine.Level, Session.Engine, Session.State, stars,
                Session.CanUndo, IsAnimating, ReducedMotion, key, args);
        }

        private string FeedbackKey()
        {
            if (IsAnimating) return "animating";
            if (Session.State.Result.Status == LevelStatus.Failed)
                return "failure" + Session.State.Result.Failure;
            if (Session.State.Result.Status == LevelStatus.Victory) return "victory";
            if (warningKey != null) return warningKey;
            return "playing";
        }

        internal static Rect FitPortraitViewport(Rect safe, float screenHeight)
        {
            float scale = Mathf.Min(safe.width / 480f, safe.height / 900f);
            var size = new Vector2(480 * scale, 900 * scale);
            return new Rect(safe.x + (safe.width - size.x) / 2,
                screenHeight - safe.yMax + (safe.height - size.y) / 2, size.x, size.y);
        }
    }
}
