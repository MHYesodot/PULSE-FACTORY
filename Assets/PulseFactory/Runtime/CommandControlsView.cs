using System;
using System.Collections.Generic;
using PulseFactory.Core;
using PulseFactory.Localization;
using PulseFactory.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PulseFactory.Presentation
{
    public sealed class CommandControlsView : MonoBehaviour
    {
        private readonly Dictionary<SimulationCommand, Button> commands = new Dictionary<SimulationCommand, Button>();
        private Button undo;
        private Button restart;
        private Button back;
        private Button motion;
        private TMP_FontAsset font;
        private bool rtl;
        private Action<SimulationCommand> commandAction;
        private Action undoAction;
        private Action restartAction;
        private Action backAction;
        private Action motionAction;

        public void Build(RectTransform parent, TMP_FontAsset fontAsset, Action<SimulationCommand> onCommand,
            Action onUndo, Action onRestart, Action onBack, Action onMotion, bool rightToLeft)
        {
            font = fontAsset;
            rtl = rightToLeft;
            commandAction = onCommand;
            undoAction = onUndo;
            restartAction = onRestart;
            backAction = onBack;
            motionAction = onMotion;
            var root = UIFactory.Rect("Command controls", parent, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFactory.Stretch(root);
            var commandLabels = new[] { "move", "pulseA", "pulseB" };
            var commandValues = new[] { SimulationCommand.Move, SimulationCommand.PulseA, SimulationCommand.PulseB };
            for (int i = 0; i < commandValues.Length; i++)
            {
                var command = commandValues[i];
                var button = UIFactory.Button("Command " + command, root, font, "", 17,
                    i == 0 ? new Color(0.12f, 0.38f, 0.34f)
                        : i == 1 ? new Color(0.28f, 0.22f, 0.46f) : new Color(0.15f, 0.28f, 0.44f),
                    rtl, () => commandAction(command));
                var rect = (RectTransform)button.transform;
                int visualIndex = rtl ? commandValues.Length - 1 - i : i;
                float left = 0.035f + visualIndex * 0.315f;
                rect.anchorMin = new Vector2(left, 0.145f);
                rect.anchorMax = new Vector2(left + 0.29f, 0.255f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                commands.Add(command, button);
            }

            undo = AddUtilityButton(root, "Undo", 0, () => undoAction());
            restart = AddUtilityButton(root, "Restart", 1, () => restartAction());
            back = AddUtilityButton(root, "Levels", 2, () => backAction());
            motion = AddUtilityButton(root, "Motion", 3, () => motionAction());
        }

        public Button CommandButton(SimulationCommand command) => commands[command];

        public void Refresh(LocalizationCatalog text, SimulationEngine engine, SimulationState state,
            bool canUndo, bool animating, bool reducedMotion)
        {
            rtl = text.RightToLeft;
            var keys = new Dictionary<SimulationCommand, string>
            {
                [SimulationCommand.Move] = "move",
                [SimulationCommand.PulseA] = "pulseA",
                [SimulationCommand.PulseB] = "pulseB"
            };
            foreach (var pair in commands)
            {
                SetButtonText(pair.Value, text.Text(keys[pair.Key]), rtl);
                pair.Value.interactable = !animating && engine.IsAvailable(state, pair.Key);
                pair.Value.GetComponent<ButtonPressFeedback>().SetMotionEnabled(!reducedMotion);
            }
            SetButtonText(undo, text.Text("undo"), rtl);
            SetButtonText(restart, text.Text("restart"), rtl);
            SetButtonText(back, text.Text("back"), rtl);
            SetButtonText(motion, text.Text(reducedMotion ? "motionOff" : "motionOn"), rtl);
            undo.interactable = !animating && canUndo;
            restart.interactable = !animating;
            back.interactable = !animating;
            motion.interactable = !animating;
            undo.GetComponent<ButtonPressFeedback>().SetMotionEnabled(!reducedMotion);
            restart.GetComponent<ButtonPressFeedback>().SetMotionEnabled(!reducedMotion);
            back.GetComponent<ButtonPressFeedback>().SetMotionEnabled(!reducedMotion);
            motion.GetComponent<ButtonPressFeedback>().SetMotionEnabled(!reducedMotion);
        }

        private Button AddUtilityButton(Transform parent, string name, int order, UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.Button(name, parent, font, "", 12,
                order == 0 ? new Color(0.13f, 0.18f, 0.25f) : UIFactory.Raised, rtl, action);
            var rect = (RectTransform)button.transform;
            int visualOrder = rtl ? 3 - order : order;
            float left = 0.025f + visualOrder * 0.2425f;
            rect.anchorMin = new Vector2(left, 0.025f);
            rect.anchorMax = new Vector2(left + 0.2225f, 0.12f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return button;
        }

        private static void SetButtonText(Button button, string value, bool rightToLeft)
        {
            var text = button.GetComponentInChildren<TMP_Text>();
            text.text = value;
            text.isRightToLeftText = rightToLeft;
            text.fontSize = value.Length > 16 ? 10 : 13;
        }
    }
}
