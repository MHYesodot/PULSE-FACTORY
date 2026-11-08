using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using PulseFactory.Core;
using PulseFactory.Localization;
using PulseFactory.Persistence;
using PulseFactory.Presentation;
using UnityEngine;

namespace PulseFactory.UI
{
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private string levelResource = "Levels/tutorial";
        [SerializeField] private int levelNumber = 1;
        private LocalizationCatalog english;
        private LocalizationCatalog hebrew;
        private LocalizationCatalog text;
        private SaveStore store;
        private SaveData save;
        private BoardRenderer board;
        private Font font;
        private bool hebrewFontAvailable;
        private string warningKey;
        private bool loadFailed;
        private GUIStyle label;
        private GUIStyle title;
        private GUIStyle button;
        public GameSession Session { get; private set; }
        public bool IsAnimating => board != null && board.IsAnimating(Time.unscaledTime);
        public bool ReducedMotion => save != null && save.ReducedMotion;
#if UNITY_EDITOR
        internal static string TestSavePath { get; set; }
#endif

        private void Awake()
        {
            try
            {
                english = new LocalizationCatalog(Resource("Localization/en"));
                english.ValidateRequiredKeys("loadError");
                text = english;
                hebrew = new LocalizationCatalog(Resource("Localization/he"));
                english.ValidateKeys(hebrew);
                english.ValidateRequiredKeys("title", "tutorial", "move", "pulseA", "pulseB", "undo", "restart",
                    "language", "motionOn", "motionOff", "beat", "switches", "progress", "best", "playing",
                    "animating", "victory", "failureCollision", "failureWrongDestination", "failureDestinationFull",
                    "failureBeatLimit", "hint", "schedule", "legend", "source", "diverter", "red", "blue",
                    "redSymbol", "blueSymbol", "destinationCount", "saveWarning", "fontWarning");
                font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Arial Hebrew", "Noto Sans Hebrew", "DejaVu Sans" }, 22);
                hebrewFontAvailable = font != null && Enumerable.Range(0x05D0, 27).All(c => font.HasCharacter((char)c));
                // Batch-mode tests never touch the player's real progression.
                string filename = Application.isBatchMode ? "progress-batch.json" : "progress.json";
                string savePath = Path.Combine(Application.persistentDataPath, filename);
#if UNITY_EDITOR
                savePath = TestSavePath ?? savePath;
#endif
                store = new SaveStore(savePath, Debug.LogWarning);
                save = store.Load();
                if (store.HadLoadError) warningKey = "saveWarning";
                if (save.Locale == "he")
                {
                    if (hebrewFontAvailable) text = hebrew;
                    else { warningKey = "fontWarning"; Debug.LogWarning(english.Text("fontWarning")); }
                }
                Session = new GameSession(LevelJson.Parse(Resource(levelResource)));
                board = new BoardRenderer(Session.Engine);
                Application.targetFrameRate = 60;
                Screen.orientation = ScreenOrientation.Portrait;
            }
            catch (Exception ex) when (ex is LevelValidationException || ex is SerializationException
                || ex is IOException || ex is ArgumentException || ex is InvalidOperationException)
            {
                loadFailed = true;
                Debug.LogError("Pulse Factory initialization failed: " + ex);
            }
        }

        private static string Resource(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException("Missing resource: " + path);
            return asset.text;
        }

        public bool Execute(SimulationCommand command)
        {
            if (loadFailed || Session == null || IsAnimating || !Session.Engine.IsAvailable(Session.State, command))
            {
                Debug.LogWarning("Ignored unavailable simulation command: " + command);
                return false;
            }
            var result = Session.Execute(command);
            board.Animate(result, Time.unscaledTime, save.ReducedMotion);
            if (result.State.Result.Status == LevelStatus.Victory)
            {
                save.RecordVictory(Session.Engine.Level.Id, levelNumber, result.State.Result.Stars);
                Persist();
            }
            return true;
        }

        public void Undo()
        {
            if (Session == null || IsAnimating || !Session.CanUndo)
            { Debug.LogWarning("Ignored unavailable undo."); return; }
            Session.Undo();
            board.Reset();
        }

        public void Restart()
        {
            if (Session == null || IsAnimating)
            { Debug.LogWarning("Ignored unavailable restart."); return; }
            Session.Restart();
            board.Reset();
        }

        private void Persist()
        {
            if (!store.Save(save)) warningKey = "saveWarning";
        }

        private void ToggleLocale()
        {
            if (text == english && !hebrewFontAvailable)
            {
                warningKey = "fontWarning";
                Debug.LogWarning(english.Text("fontWarning"));
                return;
            }
            text = text == english ? hebrew : english;
            save.Locale = text.Locale;
            Persist();
        }

        private void OnDestroy() { if (font != null) Destroy(font); }

        private void OnGUI()
        {
            if (text == null) return; // Missing English resources are a fatal, logged content error.
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label)
                { font = font, fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                label.normal.textColor = Color.white;
                title = new GUIStyle(label) { fontSize = 30, fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20, wordWrap = true };
            }
            var viewport = FitPortraitViewport(Screen.safeArea, Screen.height);
            float scale = viewport.width / 480f;
            if (scale <= 0) return;
            var previousMatrix = GUI.matrix;
            bool previousEnabled = GUI.enabled;
            var origin = new Vector3(viewport.x, viewport.y, 0);
            GUI.matrix = Matrix4x4.TRS(origin, Quaternion.identity, new Vector3(scale, scale, 1));
            BoardRenderer.Fill(new Rect(0, 0, 480, 900), new Color(0.035f, 0.055f, 0.10f));
            GUI.Label(new Rect(15, 8, 450, 45), text.Display("title"), title);
            if (loadFailed)
                LocalizedLabel(new Rect(25, 100, 430, 200), "loadError");
            else DrawGame();
            GUI.enabled = previousEnabled;
            GUI.matrix = previousMatrix;
        }

        internal static Rect FitPortraitViewport(Rect safe, float screenHeight)
        {
            float scale = Mathf.Min(safe.width / 480f, safe.height / 900f);
            var size = new Vector2(480 * scale, 900 * scale);
            return new Rect(safe.x + (safe.width - size.x) / 2,
                screenHeight - safe.yMax + (safe.height - size.y) / 2, size.x, size.y);
        }

        private void DrawGame()
        {
            var state = Session.State;
            var level = Session.Engine.Level;
            GUI.Label(new Rect(15, 53, 450, 30), text.Display("tutorial"), label);
            GUI.Label(new Rect(15, 86, 450, 28), text.Display("beat", state.Beat, level.MaxBeats), label);
            GUI.Label(new Rect(15, 115, 450, 28), text.Display("switches", state.Switches.A ? 1 : 0, state.Switches.B ? 1 : 0), label);
            GUI.Label(new Rect(15, 144, 450, 28), text.Display("progress", state.Deliveries.Count, level.Spawns.Count), label);
            board.Draw(new Rect(15, 180, 450, 335), state, text, label, Time.unscaledTime);
            GUI.Label(new Rect(15, 517, 450, 25), text.Display("legend"), label);
            string statusKey;
            if (IsAnimating) statusKey = "animating";
            else if (state.Result.Status == LevelStatus.Victory) statusKey = "victory";
            else if (state.Result.Status == LevelStatus.Failed) statusKey = "failure" + state.Result.Failure;
            else statusKey = "playing";
            LocalizedLabel(new Rect(20, 545, 440, 52), statusKey, state.Result.Stars);

            CommandButton(0, "move", SimulationCommand.Move);
            CommandButton(1, "pulseA", SimulationCommand.PulseA);
            CommandButton(2, "pulseB", SimulationCommand.PulseB);
            GUI.enabled = !IsAnimating && Session.CanUndo;
            if (GUI.Button(ControlRect(0, 2, 665), text.Display("undo"), button)) Undo();
            GUI.enabled = !IsAnimating;
            if (GUI.Button(ControlRect(1, 2, 665), text.Display("restart"), button)) Restart();
            if (GUI.Button(ControlRect(0, 2, 727), text.Display("language"), button)) ToggleLocale();
            if (GUI.Button(ControlRect(1, 2, 727), text.Display(save.ReducedMotion ? "motionOff" : "motionOn"), button))
            { save.ReducedMotion = !save.ReducedMotion; Persist(); }
            GUI.enabled = true;
            int best = save.BestResults.FirstOrDefault(p => p.LevelId == level.Id)?.Stars ?? 0;
            GUI.Label(new Rect(15, 785, 450, 25), text.Display("best", best), label);
            LocalizedLabel(new Rect(15, 812, 450, 32), "schedule");
            LocalizedLabel(new Rect(15, 845, 450, 45), warningKey ?? "hint");
        }

        private void LocalizedLabel(Rect area, string key, params object[] arguments)
        {
            var wrapped = text.DisplayWrapped(key, value => label.CalcSize(new GUIContent(value)).x <= area.width, arguments);
            GUI.Label(area, wrapped, label);
        }

        private Rect ControlRect(int index, int count, float y)
        {
            if (text.RightToLeft) index = count - 1 - index;
            float width = (450f - (count - 1) * 10) / count;
            return new Rect(15 + index * (width + 10), y, width, 54);
        }

        private void CommandButton(int index, string key, SimulationCommand command)
        {
            GUI.enabled = !IsAnimating && Session.Engine.IsAvailable(Session.State, command);
            if (GUI.Button(ControlRect(index, 3, 603), text.Display(key), button)) Execute(command);
        }
    }
}
