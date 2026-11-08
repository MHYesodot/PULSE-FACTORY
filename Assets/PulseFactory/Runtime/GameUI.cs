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
        private TMP_Text menuTitle;
        private TMP_Text menuSubtitle;
        private Button menuLanguage;
        private Button gameLanguage;
        private SaveData menuSave = new SaveData();
        private bool menuReducedMotion;
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
            var background = UIFactory.Image("Factory background", transform, UIFactory.Ink);
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
        }

        public void UpdateGameplay(LevelCatalogEntry entry, LevelDefinition level, SimulationEngine engine,
            SimulationState state, int bestStars, bool canUndo, bool animating, bool reducedMotion,
            string feedbackKey, object[] feedbackArgs)
        {
            hud.Refresh(entry, level, state, text, feedbackKey, feedbackArgs, animating);
            controls.Refresh(text, engine, state, canUndo, animating, reducedMotion);
        }

        public void UpdateLocale(LocalizationCatalog localization)
        {
            text = localization ?? throw new ArgumentNullException(nameof(localization));
            RefreshLocale();
        }

        public void SetExportResult(string key) => levelSelection.RefreshExportStatus(text, key);

        public void ShowVictory(int stars, bool finalLevel, bool canAdvance) =>
            outcome.ShowVictory(stars, finalLevel, canAdvance, text);

        public void ShowFailure(string key) => outcome.ShowFailure(key, text);

        private void BuildMenu(RectTransform parent, LevelCatalog levelCatalog, IReadOnlyList<LevelDefinition> levels,
            Action<int> onSelectLevel, Action onExport, Action onMotion)
        {
            menuTitle = UIFactory.Text("Factory title", parent, font, "", 28, UIFactory.White,
                TextAlignmentOptions.Center, false, true);
            menuTitle.rectTransform.anchorMin = new Vector2(0.05f, 0.86f);
            menuTitle.rectTransform.anchorMax = new Vector2(0.7f, 0.96f);
            menuTitle.rectTransform.offsetMin = menuTitle.rectTransform.offsetMax = Vector2.zero;
            menuSubtitle = UIFactory.Text("Factory subtitle", parent, font, "", 11, UIFactory.Muted,
                TextAlignmentOptions.Center, false, true);
            menuSubtitle.rectTransform.anchorMin = new Vector2(0.05f, 0.82f);
            menuSubtitle.rectTransform.anchorMax = new Vector2(0.95f, 0.87f);
            menuSubtitle.rectTransform.offsetMin = menuSubtitle.rectTransform.offsetMax = Vector2.zero;
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
            var frameRect = (RectTransform)boardFrame.transform;
            frameRect.anchorMin = new Vector2(0.045f, 0.43f);
            frameRect.anchorMax = new Vector2(0.955f, 0.655f);
            frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
            board = boardFrame.gameObject.AddComponent<BoardView>();
            board.Build(frameRect, font, text);

            var hudHolder = new GameObject("Gameplay HUD view", typeof(RectTransform));
            hudHolder.transform.SetParent(parent, false);
            hud = hudHolder.AddComponent<GameplayHudView>();
            hud.Build(parent, font);

            var controlsHolder = new GameObject("Command controls view", typeof(RectTransform));
            controlsHolder.transform.SetParent(parent, false);
            controls = controlsHolder.AddComponent<CommandControlsView>();
            controls.Build(parent, font, onCommand, onUndo, onRestart, onLevels, onMotion);

            outcome = gameObject.AddComponent<OutcomeView>();
            outcome.Build(parent, font, onRetry, onNext, onLevels);
        }

        private Button HeaderLanguage(RectTransform page, Action onLanguage)
        {
            var button = UIFactory.Button("Language", page, font, "", 9, UIFactory.Raised,
                false, () => onLanguage());
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0.73f, 0.955f);
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
            menuTitle.text = text.Text("title");
            menuSubtitle.text = text.Text("menuSubtitle");
            menuTitle.isRightToLeftText = menuSubtitle.isRightToLeftText = text.RightToLeft;
            var language = text.Locale == "en" ? "languageHebrew" : "languageEnglish";
            SetLanguageButton(menuLanguage, text.Text(language), text.RightToLeft);
            SetLanguageButton(gameLanguage, text.Text(language), text.RightToLeft);
            levelSelection.Refresh(text, menuSave, menuReducedMotion, exportResultKey);
            board.SetLocalization(text);
            hud.SetLocalization(text);
            outcome.Refresh(text);
        }

        private static void SetLanguageButton(Button button, string value, bool rtl)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            label.text = value;
            label.isRightToLeftText = rtl;
        }
    }
}
