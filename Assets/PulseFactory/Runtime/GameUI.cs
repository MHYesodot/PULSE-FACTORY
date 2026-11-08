using System;
using System.Collections.Generic;
using PulseFactory.Core;
using PulseFactory.Localization;
using PulseFactory.Persistence;
using PulseFactory.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PulseFactory.Presentation
{
    public sealed class GameUI : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform safeRoot;
        private RectTransform menuPage;
        private RectTransform gameplayPage;
        private TMP_FontAsset font;
        private LocalizationCatalog text;
        private LevelSelectionView levelSelection;
        private GameplayHudView hud;
        private CommandControlsView controls;
        private BoardView board;
        private OutcomeView outcome;
        private Button menuLanguage;
        private Button gameLanguage;
        private SaveData menuSave = new SaveData();
        private bool menuReducedMotion;
        private bool gameplayReducedMotion;
        private string exportResultKey = "statsDisclaimer";

        public Canvas Canvas => canvas;
        public BoardView Board => board;
        public CommandControlsView Controls => controls;
        public LevelSelectionView LevelSelection => levelSelection;
        public OutcomeView Outcome => outcome;

        public void Build(TMP_FontAsset fontAsset, LocalizationCatalog localization, LevelCatalog levelCatalog,
            IReadOnlyList<LevelDefinition> levels, Action<int> onSelectLevel, Action<SimulationCommand> onCommand,
            Action onUndo, Action onRestart, Action onLevels, Action onMotion, Action onLanguage, Action onExport,
            Action onNext, Action onRetry)
        {
            font = fontAsset ?? throw new ArgumentNullException(nameof(fontAsset));
            text = localization ?? throw new ArgumentNullException(nameof(localization));
            if (levelCatalog == null) throw new ArgumentNullException(nameof(levelCatalog));
            if (levels == null) throw new ArgumentNullException(nameof(levels));

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 20;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            UIFactory.EnsureEventSystem();
            var background = UIFactory.Background("Factory background", transform);
            UIFactory.Stretch((RectTransform)background.transform);
            safeRoot = UIFactory.Rect("Safe area", transform, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            safeRoot.gameObject.AddComponent<SafeAreaView>();
            menuPage = Page("Level selection page");
            gameplayPage = Page("Gameplay page");
            BuildMenu(menuPage, levelCatalog, levels, onSelectLevel, onExport, onMotion);
            BuildGameplay(gameplayPage, levels, onCommand, onUndo, onRestart, onLevels, onMotion, onNext, onRetry);
            menuLanguage = HeaderLanguage(menuPage, onLanguage);
            gameLanguage = HeaderLanguage(gameplayPage, onLanguage);
            ShowLevelSelect();
            RefreshLocale();
        }

        public void ShowLevelSelect()
        {
            menuPage.gameObject.SetActive(true);
            gameplayPage.gameObject.SetActive(false);
            if (outcome != null) outcome.Hide();
        }

        public void ShowGameplay() 
        {
            menuPage.gameObject.SetActive(false);
            gameplayPage.gameObject.SetActive(true);
        }

        public void SetLevel(SimulationEngine engine, SimulationState state)
        {
            board.SetLevel(engine, state);
            outcome.Hide();
        }

        public void UpdateMenu(SaveData save, bool reducedMotion, string exportResultKey)
        {
            menuSave = save;
            menuReducedMotion = reducedMotion;
            this.exportResultKey = exportResultKey;
            levelSelection.Refresh(text, save, reducedMotion, exportResultKey);
            menuLanguage.GetComponent<ButtonPressFeedback>().SetMotionEnabled(!reducedMotion);
        }

        public void UpdateGameplay(LevelCatalogEntry entry, LevelDefinition level, SimulationEngine engine,
            SimulationState state, int bestStars, bool canUndo, bool animating, bool reducedMotion,
            string feedbackKey, object[] feedbackArgs)
        {
            gameplayReducedMotion = reducedMotion;
            board.SetReducedMotion(reducedMotion);
            hud.Refresh(entry, level, state, text, feedbackKey, feedbackArgs, animating);
            controls.Refresh(text, engine, state, canUndo, animating, reducedMotion);
            gameLanguage.GetComponent<ButtonPressFeedback>().SetMotionEnabled(!reducedMotion);
            outcome.SetReducedMotion(reducedMotion);
        }

        public void UpdateLocale(LocalizationCatalog localization)
        {
            text = localization ?? throw new ArgumentNullException(nameof(localization));
            RefreshLocale();
        }

        public void SetExportResult(string key) => levelSelection.RefreshExportStatus(text, key);

        public void ShowVictory(LevelCatalogEntry entry, int stars, bool finalLevel, bool canAdvance)
        {
            string levelLabel = text.Text("completedLevel", entry.Number, text.Text(entry.TitleKey));
            outcome.ShowVictory(levelLabel, stars, finalLevel, canAdvance, text, !gameplayReducedMotion);
        }

        public void ShowFailure(string key) => outcome.ShowFailure(key, text, !gameplayReducedMotion);

        private void BuildMenu(RectTransform parent, LevelCatalog levelCatalog, IReadOnlyList<LevelDefinition> levels,
            Action<int> onSelectLevel, Action onExport, Action onMotion)
        {
            var holder = new GameObject("Level selector view", typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            levelSelection = holder.AddComponent<LevelSelectionView>();
            levelSelection.Build(parent, font, levelCatalog, levels, onSelectLevel, onExport, onMotion);
        }

        private void BuildGameplay(RectTransform parent, IReadOnlyList<LevelDefinition> levels,
            Action<SimulationCommand> onCommand, Action onUndo, Action onRestart, Action onLevels,
            Action onMotion, Action onNext, Action onRetry)
        {
            var boardFrame = UIFactory.Image("Board frame", parent, UIFactory.Panel, true);
            UIFactory.Shadow(boardFrame, new Color(0, 0, 0, 0.35f), new Vector2(0, -2), 8);
            var frameRect = (RectTransform)boardFrame.transform;
            frameRect.anchorMin = new Vector2(0.035f, 0.305f);
            frameRect.anchorMax = new Vector2(0.965f, 0.69f);
            frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
            var boardViewport = UIFactory.Rect("Board viewport", frameRect, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            boardViewport.offsetMin = new Vector2(10, 10);
            boardViewport.offsetMax = new Vector2(-10, -10);
            board = boardFrame.gameObject.AddComponent<BoardView>();
            board.Build(boardViewport, font, text);

            var hudHolder = new GameObject("Gameplay HUD view", typeof(RectTransform));
            hudHolder.transform.SetParent(parent, false);
            hud = hudHolder.AddComponent<GameplayHudView>();
            hud.Build(parent, font);

            var controlsHolder = new GameObject("Command controls view", typeof(RectTransform));
            controlsHolder.transform.SetParent(parent, false);
            controls = controlsHolder.AddComponent<CommandControlsView>();
            controls.Build(parent, font, onCommand, onUndo, onRestart, onLevels, onMotion, text.RightToLeft);

            outcome = gameObject.AddComponent<OutcomeView>();
            outcome.Build(parent, font, onRetry, onNext, onLevels);
        }

        private Button HeaderLanguage(RectTransform page, Action onLanguage)
        {
            var button = UIFactory.Button("Language", page, font, "", 9, UIFactory.Raised,
                false, () => onLanguage());
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0.76f, 0.95f);
            rect.anchorMax = new Vector2(0.97f, 0.995f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return button;
        }

        private RectTransform Page(string name)
        {
            var page = UIFactory.Rect(name, safeRoot, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFactory.Stretch(page);
            return page;
        }

        private void RefreshLocale()
        {
            SetLanguageButton(menuLanguage, text.Text("languageToggle"), text.RightToLeft);
            SetLanguageButton(gameLanguage, text.Text("languageToggle"), text.RightToLeft);
            levelSelection.Refresh(text, menuSave, menuReducedMotion, exportResultKey);
            board.SetLocalization(text);
            hud.SetLocalization(text);
            outcome.Refresh(text);
        }

        private static void SetLanguageButton(Button button, string value, bool valueIsRightToLeft)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            label.text = value;
            UIFactory.SetTextDirection(label, valueIsRightToLeft);
            label.fontSize = 12;
        }
    }
}
