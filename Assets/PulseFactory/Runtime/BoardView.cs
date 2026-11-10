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
        private readonly Dictionary<string, StationView> stations = new Dictionary<string, StationView>();
        private readonly Dictionary<string, DiverterView> diverters = new Dictionary<string, DiverterView>();
        private readonly List<(EdgeDefinition Edge, ConveyorView View)> conveyors = new List<(EdgeDefinition, ConveyorView)>();
        private readonly List<(EdgeDefinition Edge, RectTransform Root, Image Route)> bridges =
            new List<(EdgeDefinition, RectTransform, Image)>();
        private readonly Dictionary<string, PackageDefinition> packageDefinitions = new Dictionary<string, PackageDefinition>();
        private RectTransform boardRect;
        private RectTransform packageLayer;
        private RectTransform crossingLayer;
        private TMP_FontAsset font;
        private LocalizationCatalog text;
        private SimulationEngine engine;
        private SimulationState currentState;
        private Vector2 lastSize;
        private bool isRightToLeft;
        private bool presenting;
        private bool reducedMotion;
        public void Build(RectTransform parent, TMP_FontAsset fontAsset, LocalizationCatalog localization)
        {
            font = fontAsset;
            text = localization;
            boardRect = UIFactory.Rect("Factory board", parent, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFactory.Stretch(boardRect);
            var deck = UIFactory.Image("Machined deck surround", boardRect, UIFactory.Steel, true);
            UIFactory.Stretch((RectTransform)deck.transform, 2, 2);
            var deckSurface = UIFactory.Image("Recessed factory deck", boardRect, UIFactory.Panel, true);
            UIFactory.Stretch((RectTransform)deckSurface.transform, 6, 6);
            var surfaceOutline = deckSurface.gameObject.AddComponent<Outline>();
            surfaceOutline.effectColor = new Color(UIFactory.SteelLight.r, UIFactory.SteelLight.g,
                UIFactory.SteelLight.b, 0.3f);
            surfaceOutline.effectDistance = new Vector2(1, -1);
            for (int i = 0; i < 4; i++)
            {
                var fastener = UIFactory.Circle("Deck fastener", boardRect, UIFactory.SteelLight);
                var fastenerRect = (RectTransform)fastener.transform;
                fastenerRect.anchorMin = fastenerRect.anchorMax = new Vector2(i % 2 == 0 ? 0.035f : 0.965f,
                    i < 2 ? 0.04f : 0.96f);
                fastenerRect.sizeDelta = new Vector2(5, 5);
                fastenerRect.anchoredPosition = Vector2.zero;
            }
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

            crossingLayer = UIFactory.Rect("Raised crossover bridges", boardRect, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFactory.Stretch(crossingLayer);

            foreach (var node in engine.Level.Nodes)
            {
                var nodeRoot = UIFactory.Rect("Node " + node.Id, boardRect,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(UIFactory.BoardNodeSize, UIFactory.BoardNodeSize + 42), Vector2.zero);
                nodeTransforms.Add(node.Id, nodeRoot);
                TMP_Text caption;
                if (node.Type == NodeType.Diverter)
                {
                    var diverter = nodeRoot.gameObject.AddComponent<DiverterView>();
                    diverter.Build(nodeRoot, font, node.Group.Value, isRightToLeft);
                    diverters.Add(node.Id, diverter);
                    caption = UIFactory.Text("Node label", nodeRoot, font, "", 10, UIFactory.Muted,
                        TextAlignmentOptions.Center, isRightToLeft, true);
                }
                else
                {
                    if (node.Type == NodeType.Source || node.Type == NodeType.Destination)
                    {
                        var station = nodeRoot.gameObject.AddComponent<StationView>();
                        station.Build(nodeRoot, font, node, text, isRightToLeft);
                        stations.Add(node.Id, station);
                    }
                    else
                    {
                        var roller = UIFactory.Circle("Transfer roller", nodeRoot, UIFactory.Steel);
                        ((RectTransform)roller.transform).sizeDelta = new Vector2(42, 42);
                        var hub = UIFactory.Circle("Roller hub", roller.transform, UIFactory.SteelLight);
                        ((RectTransform)hub.transform).sizeDelta = new Vector2(14, 14);
                    }
                    caption = UIFactory.Text("Node label", nodeRoot, font, "", 10, UIFactory.Muted,
                        TextAlignmentOptions.Center, isRightToLeft, true);
                }
                var labelRect = caption.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.sizeDelta = new Vector2(node.Type == NodeType.Destination ? 72 : 86, 18);
                labelRect.anchoredPosition = new Vector2(0, -(UIFactory.BoardNodeSize * 0.5f + 12));
                nodeLabels.Add(node.Id, caption);
            }
            crossingLayer.SetAsLastSibling();
            foreach (var node in nodeTransforms.Values) node.SetAsLastSibling();
            packageLayer.SetAsLastSibling();
            lastSize = Vector2.zero;
            Reflow();
            SetSwitches(state.Switches, false);
            UpdateDestinations(state);
            RenderPackages(state);
        }

        public void SetLocalization(LocalizationCatalog localization)
        {
            text = localization;
            isRightToLeft = localization.RightToLeft;
            foreach (var pair in diverters)
                pair.Value.SetState(currentState.Switches, text, false);
            UpdateNodeLabels();
        }

        public void SetReducedMotion(bool value) => reducedMotion = value;

        public void SetVisible(bool value)
        {
            if (boardRect != null) boardRect.gameObject.SetActive(value);
        }

        public IEnumerator Present(SimulationResult result, float duration)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            currentState = result.State;
            var moves = result.Events.Where(e => e.Type == EventType.Moved).ToArray();
            bool pulse = result.Events.Any(e => e.Type == EventType.SwitchToggled);
            if (duration <= 0)
            {
                SetSwitches(result.State.Switches, false);
                RenderPackages(result.State);
                yield break;
            }

            presenting = true;
            try
            {
                SetSwitches(result.State.Switches, true);
                float signalDuration = pulse ? Mathf.Min(UIFactory.PulseSignalSeconds, duration * 0.4f) : 0f;
                if (signalDuration > 0) yield return new WaitForSecondsRealtime(signalDuration);
                if (moves.Length == 0)
                {
                    RenderPackages(result.State);
                    yield break;
                }

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
                float movementDuration = Mathf.Max(0.01f, duration - signalDuration);
                while (elapsed < movementDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float fraction = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / movementDuration));
                    foreach (var move in moves)
                    {
                        var view = views[move.PackageId];
                        view.SetPosition(Vector2.Lerp(Position(move.From), Position(move.To), fraction));
                        view.SetTravel(fraction);
                        if (delivered.Contains(move.PackageId) && elapsed > movementDuration * 0.72f)
                            view.SetDelivery(Mathf.Clamp01((elapsed - movementDuration * 0.72f) / (movementDuration * 0.28f)));
                    }
                    yield return null;
                }
                RenderPackages(result.State);
                foreach (var delivery in result.Events.Where(e => e.Type == EventType.Delivered))
                    if (stations.TryGetValue(delivery.To, out var station)) station.SetDelivered(true, !reducedMotion);
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
                segment.View.Layout(start, end, UIFactory.ConveyorThickness, engine.Route(nodes.First(n => n.Id == segment.Edge.From),
                    currentState.Switches) == segment.Edge.To);
            }
            RebuildBridges(Point);
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
                    : node.Type == NodeType.Destination ? text.Text(node.Accepts)
                    : text.Text(key);
                nodeLabels[node.Id].text = label;
                UIFactory.SetTextDirection(nodeLabels[node.Id], text.RightToLeft);
            }
        }

        private void SetSwitches(SwitchState switches, bool animate)
        {
            foreach (var pair in diverters)
            {
                var node = engine.Level.Nodes.First(item => item.Id == pair.Key);
                pair.Value.SetState(switches, text, animate && !reducedMotion);
                var route = engine.Route(node, switches);
                pair.Value.SetRoute(nodeTransforms[route].anchoredPosition - nodeTransforms[node.Id].anchoredPosition,
                    animate && !reducedMotion);
            }
            if (engine != null)
                foreach (var segment in conveyors)
                {
                    var from = engine.Level.Nodes.First(node => node.Id == segment.Edge.From);
                    segment.View.SetActive(engine.Route(from, switches) == segment.Edge.To, animate && !reducedMotion);
                }
            UpdateBridgeStates(switches);
        }

        private Vector2 Position(string nodeId) => nodeTransforms[nodeId].anchoredPosition;

        private void RenderPackages(SimulationState state)
        {
            UpdateDestinations(state);
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

        private void UpdateDestinations(SimulationState state)
        {
            var completed = state.Deliveries.GroupBy(delivery => delivery.DestinationId)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            foreach (var node in engine.Level.Nodes)
            {
                if (!stations.TryGetValue(node.Id, out var station)) continue;
                bool delivered = completed.TryGetValue(node.Id, out int count) && count >= node.RequiredCount;
                station.SetDelivered(delivered, false);
            }
        }

        private void ClearBoard()
        {
            foreach (var node in nodeTransforms.Values)
                if (node != null) Destroy(node.gameObject);
            foreach (var segment in conveyors)
                if (segment.View != null) Destroy(segment.View.gameObject);
            if (crossingLayer != null)
            {
                Destroy(crossingLayer.gameObject);
                crossingLayer = null;
            }
            nodeTransforms.Clear();
            nodeLabels.Clear();
            stations.Clear();
            diverters.Clear();
            conveyors.Clear();
            bridges.Clear();
            ClearPackages();
        }

        private void ClearPackages()
        {
            if (packageLayer == null) return;
            for (int i = packageLayer.childCount - 1; i >= 0; i--)
                Destroy(packageLayer.GetChild(i).gameObject);
        }

        private void RebuildBridges(Func<string, Vector2> point)
        {
            if (crossingLayer == null) return;
            for (int i = crossingLayer.childCount - 1; i >= 0; i--)
                Destroy(crossingLayer.GetChild(i).gameObject);
            bridges.Clear();
            for (int first = 0; first < conveyors.Count; first++)
                for (int second = first + 1; second < conveyors.Count; second++)
                {
                    var lower = conveyors[first].Edge;
                    var upper = conveyors[second].Edge;
                    if (lower.From == upper.From || lower.From == upper.To
                        || lower.To == upper.From || lower.To == upper.To) continue;
                    if (!TryGetSegmentIntersection(point(lower.From), point(lower.To),
                        point(upper.From), point(upper.To), out var crossing)) continue;

                    var root = UIFactory.Rect("Non-junction overpass", crossingLayer,
                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(40, 22), crossing);
                    var bridge = UIFactory.Image("Raised bridge housing", root, UIFactory.Steel, true);
                    UIFactory.Stretch((RectTransform)bridge.transform);
                    UIFactory.Shadow(bridge, new Color(0, 0, 0, 0.5f), new Vector2(0, -1), 3);
                    var bridgeOutline = bridge.gameObject.AddComponent<Outline>();
                    bridgeOutline.effectColor = UIFactory.SteelLight;
                    bridgeOutline.effectDistance = new Vector2(1, -1);
                    var track = UIFactory.Image("Bridge tread", root, UIFactory.BeltSurface, true);
                    UIFactory.Stretch((RectTransform)track.transform, 4, 4);
                    var route = UIFactory.Image("Bridge live route", root, UIFactory.Muted, true);
                    var routeRect = (RectTransform)route.transform;
                    routeRect.anchorMin = new Vector2(0.2f, 0.5f);
                    routeRect.anchorMax = new Vector2(0.78f, 0.5f);
                    routeRect.sizeDelta = new Vector2(0, 2);
                    routeRect.anchoredPosition = Vector2.zero;
                    var layout = ConveyorView.CalculateLayout(point(upper.From), point(upper.To));
                    root.localRotation = Quaternion.Euler(0, 0, layout.AngleDegrees);
                    bridges.Add((upper, root, route));
                }
            UpdateBridgeStates(currentState.Switches);
        }

        private void UpdateBridgeStates(SwitchState switches)
        {
            foreach (var bridge in bridges)
            {
                var from = engine.Level.Nodes.First(node => node.Id == bridge.Edge.From);
                bool selected = engine.Route(from, switches) == bridge.Edge.To;
                bridge.Route.color = selected ? UIFactory.Mint : new Color(0.45f, 0.53f, 0.59f, 0.72f);
            }
        }

        internal static bool TryGetSegmentIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            out Vector2 intersection)
        {
            Vector2 first = b - a;
            Vector2 second = d - c;
            float denominator = first.x * second.y - first.y * second.x;
            if (Mathf.Abs(denominator) < 0.001f)
            {
                intersection = Vector2.zero;
                return false;
            }
            Vector2 offset = c - a;
            float alongFirst = (offset.x * second.y - offset.y * second.x) / denominator;
            float alongSecond = (offset.x * first.y - offset.y * first.x) / denominator;
            if (alongFirst <= 0.1f || alongFirst >= 0.9f || alongSecond <= 0.1f || alongSecond >= 0.9f)
            {
                intersection = Vector2.zero;
                return false;
            }
            intersection = a + first * alongFirst;
            return true;
        }
    }
}
