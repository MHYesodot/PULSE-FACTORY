using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PulseFactory.Core;
using PulseFactory.Localization;
using PulseFactory.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using EventType = PulseFactory.Core.EventType;

namespace PulseFactory.Presentation
{
    public sealed class BoardView : MonoBehaviour
    {
        private readonly Dictionary<string, RectTransform> nodeTransforms = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, TMP_Text> nodeLabels = new Dictionary<string, TMP_Text>();
        private readonly Dictionary<string, DiverterView> diverters = new Dictionary<string, DiverterView>();
        private readonly List<(EdgeDefinition Edge, ConveyorView View)> conveyors = new List<(EdgeDefinition, ConveyorView)>();
        private readonly Dictionary<string, PackageDefinition> packageDefinitions = new Dictionary<string, PackageDefinition>();
        private RectTransform boardRect;
        private RectTransform packageLayer;
        private TMP_FontAsset font;
        private LocalizationCatalog text;
        private SimulationEngine engine;
        private SimulationState currentState;
        private Vector2 lastSize;
        private bool isRightToLeft;
        private bool presenting;
        private const float NodeSize = 48f;

        public void Build(RectTransform parent, TMP_FontAsset fontAsset, LocalizationCatalog localization)
        {
            font = fontAsset;
            text = localization;
            boardRect = UIFactory.Rect("Factory board", parent, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFactory.Stretch(boardRect);
            var background = UIFactory.Image("Board surface", boardRect, new Color(0.055f, 0.085f, 0.13f), true);
            UIFactory.Stretch((RectTransform)background.transform);
            packageLayer = UIFactory.Rect("Packages", boardRect, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFactory.Stretch(packageLayer);
            isRightToLeft = localization.RightToLeft;
        }

        public void SetLevel(SimulationEngine simulation, SimulationState state)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            engine = simulation;
            currentState = state ?? throw new ArgumentNullException(nameof(state));
            ClearBoard();
            packageDefinitions.Clear();
            foreach (var spawn in engine.Level.Spawns)
                packageDefinitions.Add(spawn.Package.Id, spawn.Package);

            foreach (var edge in engine.Level.Edges)
            {
                var segment = new GameObject("Conveyor " + edge.Id, typeof(RectTransform), typeof(ConveyorView))
                    .GetComponent<ConveyorView>();
                segment.transform.SetParent(boardRect, false);
                segment.Build(font, isRightToLeft);
                conveyors.Add((edge, segment));
            }

            foreach (var node in engine.Level.Nodes)
            {
                var nodeRoot = UIFactory.Rect("Node " + node.Id, boardRect, Vector2.zero, Vector2.zero,
                    new Vector2(0.5f, 0.5f), new Vector2(NodeSize, NodeSize + 26), Vector2.zero);
                nodeTransforms.Add(node.Id, nodeRoot);
                TMP_Text caption;
                if (node.Type == NodeType.Diverter)
                {
                    var diverter = nodeRoot.gameObject.AddComponent<DiverterView>();
                    diverter.Build(nodeRoot, font, node.Group.Value, node.Inverted, isRightToLeft);
                    diverters.Add(node.Id, diverter);
                    caption = UIFactory.Text("Node label", nodeRoot, font, "", 10, UIFactory.Muted,
                        TextAlignmentOptions.Center, isRightToLeft, true);
                }
                else
                {
                    var baseColor = node.Type == NodeType.Source ? new Color(0.16f, 0.34f, 0.49f)
                        : node.Type == NodeType.Destination ? DestinationColor(node.Accepts)
                        : new Color(0.19f, 0.26f, 0.34f);
                    var plate = UIFactory.Image("Node plate", nodeRoot, baseColor, true);
                    var plateRect = (RectTransform)plate.transform;
                    plateRect.sizeDelta = new Vector2(NodeSize, NodeSize);
                    var outline = plate.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.75f, 0.84f, 0.93f, 0.55f);
                    outline.effectDistance = new Vector2(1.5f, -1.5f);
                    string icon = node.Type == NodeType.Destination ? text.Text(node.Accepts + "Symbol")
                        : node.Type == NodeType.Source ? text.Text("sourceIcon") : ">";
                    var iconText = UIFactory.Text("Node icon", plate.transform, font, icon, 20,
                        Color.white, TextAlignmentOptions.Center, isRightToLeft, true);
                    UIFactory.Stretch(iconText.rectTransform, 3, 3);
                    caption = UIFactory.Text("Node label", nodeRoot, font, "", 10, UIFactory.Muted,
                        TextAlignmentOptions.Center, isRightToLeft, true);
                }
                var labelRect = caption.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0);
                labelRect.sizeDelta = new Vector2(100, 25);
                labelRect.anchoredPosition = new Vector2(0, -13);
                nodeLabels.Add(node.Id, caption);
            }
            packageLayer.SetAsLastSibling();
            lastSize = Vector2.zero;
            Reflow();
            SetSwitches(state.Switches);
            RenderPackages(state);
        }

        public void SetLocalization(LocalizationCatalog localization)
        {
            text = localization;
            isRightToLeft = localization.RightToLeft;
            foreach (var pair in diverters)
                pair.Value.SetState(currentState.Switches, text);
            UpdateNodeLabels();
        }

        public IEnumerator Present(SimulationResult result, float duration)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            currentState = result.State;
            SetSwitches(result.State.Switches);
            var moves = result.Events.Where(e => e.Type == EventType.Moved).ToArray();
            if (duration <= 0 || moves.Length == 0)
            {
                RenderPackages(result.State);
                yield break;
            }

            presenting = true;
            try
            {
                ClearPackages();
                var views = new Dictionary<string, PackageView>();
                foreach (var move in moves)
                {
                    var view = CreatePackage(packageDefinitions[move.PackageId]);
                    views.Add(move.PackageId, view);
                    view.SetPosition(Position(move.From));
                }
                var delivered = new HashSet<string>(result.Events.Where(e => e.Type == EventType.Delivered)
                    .Select(e => e.PackageId), StringComparer.Ordinal);

                float elapsed = 0;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float fraction = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
                    foreach (var move in moves)
                    {
                        var view = views[move.PackageId];
                        view.SetPosition(Vector2.Lerp(Position(move.From), Position(move.To), fraction));
                        if (delivered.Contains(move.PackageId) && elapsed > duration * 0.72f)
                            view.SetDelivery(Mathf.Clamp01((elapsed - duration * 0.72f) / (duration * 0.28f)));
                    }
                    yield return null;
                }
                RenderPackages(result.State);
            }
            finally { presenting = false; }
        }

        private void LateUpdate()
        {
            if (engine == null || presenting) return;
            var size = boardRect.rect.size;
            if ((size - lastSize).sqrMagnitude > 1f) Reflow();
        }

        private void Reflow()
        {
            lastSize = boardRect.rect.size;
            if (engine == null || lastSize.x < 1 || lastSize.y < 1) return;
            var nodes = engine.Level.Nodes;
            float minX = nodes.Min(node => node.X);
            float maxX = nodes.Max(node => node.X);
            float minY = nodes.Min(node => node.Y);
            float maxY = nodes.Max(node => node.Y);
            float width = Mathf.Max(maxX - minX, 0.01f);
            float height = Mathf.Max(maxY - minY, 0.01f);
            float scale = Mathf.Min(Mathf.Max(40, lastSize.x - 104) / width,
                Mathf.Max(40, lastSize.y - 100) / height);
            var center = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
            Vector2 Point(string id)
            {
                var node = nodes.First(item => item.Id == id);
                return new Vector2((node.X - center.x) * scale, (node.Y - center.y) * scale);
            }

            foreach (var node in nodes)
                nodeTransforms[node.Id].anchoredPosition = Point(node.Id);
            foreach (var segment in conveyors)
            {
                var start = Point(segment.Edge.From);
                var end = Point(segment.Edge.To);
                segment.View.Layout(start, end, 22, engine.Route(nodes.First(n => n.Id == segment.Edge.From),
                    currentState.Switches) == segment.Edge.To);
            }
            UpdateNodeLabels();
            if (currentState != null) RenderPackages(currentState);
        }

        private void UpdateNodeLabels()
        {
            if (engine == null || text == null) return;
            foreach (var node in engine.Level.Nodes)
            {
                string key = node.Type == NodeType.Source ? "nodeSource"
                    : node.Type == NodeType.Diverter ? "nodeDiverter"
                    : node.Type == NodeType.Destination ? "nodeDestination" : "nodeConveyor";
                string label = node.Type == NodeType.Diverter
                    ? text.Text(key, text.Text(node.Group == SwitchGroup.A ? "switchA" : "switchB"))
                    : node.Type == NodeType.Destination ? text.Text(key, text.Text(node.Accepts))
                    : text.Text(key);
                nodeLabels[node.Id].text = label;
                nodeLabels[node.Id].isRightToLeftText = text.RightToLeft;
            }
        }

        private void SetSwitches(SwitchState switches)
        {
            foreach (var diverter in diverters.Values) diverter.SetState(switches, text);
            if (engine != null)
                foreach (var segment in conveyors)
                {
                    var from = engine.Level.Nodes.First(node => node.Id == segment.Edge.From);
                    segment.View.SetActive(engine.Route(from, switches) == segment.Edge.To);
                }
        }

        private Vector2 Position(string nodeId) => nodeTransforms[nodeId].anchoredPosition;

        private void RenderPackages(SimulationState state)
        {
            ClearPackages();
            foreach (var package in state.Packages)
            {
                var view = CreatePackage(package.Package);
                view.SetPosition(Position(package.NodeId));
            }
        }

        private PackageView CreatePackage(PackageDefinition package)
        {
            var view = new GameObject("Package " + package.Id, typeof(RectTransform), typeof(PackageView))
                .GetComponent<PackageView>();
            view.transform.SetParent(packageLayer, false);
            view.Build(view.transform, font, package.Kind, text.Text(package.Kind + "Symbol"), text.RightToLeft);
            return view;
        }

        private void ClearBoard()
        {
            foreach (var node in nodeTransforms.Values)
                if (node != null) Destroy(node.gameObject);
            foreach (var segment in conveyors)
                if (segment.View != null) Destroy(segment.View.gameObject);
            nodeTransforms.Clear();
            nodeLabels.Clear();
            diverters.Clear();
            conveyors.Clear();
            ClearPackages();
        }

        private void ClearPackages()
        {
            if (packageLayer == null) return;
            for (int i = packageLayer.childCount - 1; i >= 0; i--)
                Destroy(packageLayer.GetChild(i).gameObject);
        }

        private static Color DestinationColor(string kind) => kind == "red"
            ? new Color(0.42f, 0.19f, 0.23f) : new Color(0.16f, 0.3f, 0.48f);
    }
}
