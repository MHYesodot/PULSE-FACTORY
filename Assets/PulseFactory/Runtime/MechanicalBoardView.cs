using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PulseFactory.Core;
using UnityEngine;
using UnityEngine.UI;
using EventType = PulseFactory.Core.EventType;

namespace PulseFactory.Presentation
{
    public sealed class MechanicalBoardView : MonoBehaviour
    {
        private const float HorizontalScale = 3.3066667f;
        private const float DepthScale = 5f;
        private const float ModuleLength = 0.75f;
        private const float DeckHeight = 0.34f;
        private const float BridgeLift = 0.58f;
        private const int BoardLayer = 0;
        private const string SettingsResource = "PulseFactoryMechanical/MechanicalBoardSettings";

        private readonly Dictionary<string, Transform> nodes = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> packages = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> pivots = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Renderer>> activeRoutes =
            new Dictionary<string, List<Renderer>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Renderer>> inactiveRoutes =
            new Dictionary<string, List<Renderer>>(StringComparer.Ordinal);
        private RectTransform viewport;
        private RawImage output;
        private Camera boardCamera;
        private Light keyLight;
        private RenderTexture renderTexture;
        private Transform boardRoot;
        private MechanicalBoardAssets assets;
        private SimulationEngine engine;
        private SimulationState state;
        private bool visible;
        private bool reducedMotion;
        private float transitionStart;
        private float transitionDuration;
        private readonly List<ArmTransition> armTransitions = new List<ArmTransition>(2);

        public bool IsVisible => visible && output != null && output.enabled;
        public bool IsPresenting { get; private set; }
        public bool IsConfigured { get; private set; }
        public IReadOnlyCollection<string> PackageIds => packages.Keys;
        public IReadOnlyCollection<string> DiverterIds => pivots.Keys;
        internal IReadOnlyCollection<string> RouteEdgeIds => activeRoutes.Keys;
        internal RenderTexture RenderTarget => renderTexture;
        internal int RendererCount => boardRoot.GetComponentsInChildren<Renderer>(true).Length;
        internal Vector2 ViewportSize => viewport.rect.size;
        internal string[] ActiveRouteIds => activeRoutes
            .Where(pair => pair.Value.Any(renderer => renderer != null && renderer.gameObject.activeSelf))
            .Select(pair => pair.Key).ToArray();

        private sealed class ArmTransition
        {
            public string NodeId;
            public Quaternion From;
            public Quaternion To;
        }

        public bool Build(RectTransform boardViewport)
        {
            viewport = boardViewport != null ? boardViewport : throw new ArgumentNullException(nameof(boardViewport));
            var settings = Resources.Load<MechanicalBoardSettings>(SettingsResource);
            if (settings == null || !HasRequiredAssets(settings.Assets))
            {
                Debug.LogWarning("Mechanical board assets are unavailable; using the existing UI board.");
                return false;
            }
            assets = settings.Assets;

            var outputObject = new GameObject("Mechanical board image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            outputObject.transform.SetParent(viewport, false);
            output = outputObject.GetComponent<RawImage>();
            output.raycastTarget = false;
            output.color = Color.white;
            var outputRect = (RectTransform)output.transform;
            outputRect.anchorMin = Vector2.zero;
            outputRect.anchorMax = Vector2.one;
            outputRect.offsetMin = Vector2.zero;
            outputRect.offsetMax = Vector2.zero;

            var cameraObject = new GameObject("Mechanical board camera");
            cameraObject.transform.SetParent(transform, false);
            boardCamera = cameraObject.AddComponent<Camera>();
            boardCamera.enabled = false;
            boardCamera.orthographic = true;
            boardCamera.orthographicSize = 3.0f;
            boardCamera.clearFlags = CameraClearFlags.SolidColor;
            boardCamera.backgroundColor = new Color(0.055f, 0.075f, 0.085f);
            boardCamera.nearClipPlane = 0.05f;
            boardCamera.farClipPlane = 40f;
            boardCamera.cullingMask = 1 << BoardLayer;
            boardCamera.allowHDR = false;
            boardCamera.allowMSAA = false;
            boardCamera.depth = 1;

            var lightObject = new GameObject("Mechanical board key light");
            lightObject.transform.SetParent(transform, false);
            keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.1f;
            keyLight.color = new Color(1f, 0.91f, 0.79f);
            keyLight.shadows = LightShadows.Soft;
            keyLight.transform.rotation = Quaternion.Euler(38, -28, 0);

            boardRoot = new GameObject("Mechanical Level 2 board").transform;
            boardRoot.SetParent(transform, false);
            boardRoot.gameObject.layer = BoardLayer;
            CreateDeck();
            ConfigureCameraViewport();
            SetVisible(false);
            IsConfigured = true;
            return true;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (output != null) output.enabled = value;
            if (boardCamera != null) boardCamera.enabled = value;
            if (keyLight != null) keyLight.enabled = value;
        }

        public void SetReducedMotion(bool value) => reducedMotion = value;

        public void SetLevel(SimulationEngine simulation, SimulationState initialState)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            engine = simulation;
            state = initialState ?? throw new ArgumentNullException(nameof(initialState));
            IsPresenting = false;
            armTransitions.Clear();
            nodes.Clear();
            packages.Clear();
            pivots.Clear();
            activeRoutes.Clear();
            inactiveRoutes.Clear();
            for (int i = boardRoot.childCount - 1; i >= 0; i--)
                Destroy(boardRoot.GetChild(i).gameObject);
            CreateDeck();

            var nodeDefinitions = engine.Level.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            foreach (var node in engine.Level.Nodes)
            {
                var location = NodePosition(node);
                GameObject prefab = node.Type switch
                {
                    NodeType.Source => assets.SourceStation,
                    NodeType.Destination => node.Accepts == "red" ? assets.RedDestination : assets.BlueDestination,
                    NodeType.Diverter => assets.DiverterAssembly,
                    _ => null
                };
                if (prefab == null)
                {
                    var pivotObject = new GameObject("Transfer " + node.Id);
                    pivotObject.transform.SetParent(boardRoot, false);
                    pivotObject.transform.localPosition = location;
                    nodes.Add(node.Id, pivotObject.transform);
                    continue;
                }

                var station = Instantiate(prefab, boardRoot);
                station.name = node.Type + " " + node.Id;
                station.transform.localPosition = location;
                station.transform.localRotation = Quaternion.identity;
                nodes.Add(node.Id, station.transform);
                if (node.Type == NodeType.Diverter)
                {
                    var pivot = FindChild(station.transform, "ASSET_DIVERTER_ArmPivot");
                    if (pivot == null)
                        throw new InvalidOperationException("Imported diverter prefab has no ASSET_DIVERTER_ArmPivot.");
                    pivots.Add(node.Id, pivot);
                }
            }

            foreach (var edge in engine.Level.Edges)
                CreateEdge(edge, nodeDefinitions[edge.From]);
            SetRouteState(initialState.Switches, false);
            CreatePackages(initialState);
            ConfigureCameraViewport();
        }

        public IEnumerator Present(SimulationResult result, float duration)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            state = result.State;
            var moved = result.Events.Where(item => item.Type == EventType.Moved).ToArray();
            var delivered = new HashSet<string>(
                result.Events.Where(item => item.Type == EventType.Delivered).Select(item => item.PackageId),
                StringComparer.Ordinal);
            SetRouteState(result.State.Switches, duration > 0 && !reducedMotion);
            if (duration <= 0 || reducedMotion)
            {
                armTransitions.Clear();
                IsPresenting = false;
                CreatePackages(result.State);
                yield break;
            }

            IsPresenting = true;
            if (moved.Length == 0)
            {
                yield return AnimateArms(duration);
                IsPresenting = false;
                yield break;
            }

            var starts = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            var ends = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            foreach (var movement in moved)
            {
                if (!packages.TryGetValue(movement.PackageId, out var package))
                {
                    var definition = engine.Level.Spawns.Select(spawn => spawn.Package)
                        .First(item => item.Id == movement.PackageId);
                    package = Instantiate(definition.Kind == "red" ? assets.RedPackage : assets.BluePackage, boardRoot).transform;
                    package.name = "Package " + movement.PackageId;
                    package.localPosition = NodePosition(engine.Level.Nodes.First(node => node.Id == movement.From));
                    packages.Add(movement.PackageId, package);
                }
                starts[movement.PackageId] = package.localPosition;
                ends[movement.PackageId] = NodePosition(engine.Level.Nodes.First(node => node.Id == movement.To));
            }

            transitionStart = Time.unscaledTime;
            transitionDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0;
            try
            {
                while (elapsed < transitionDuration)
                {
                    elapsed = Time.unscaledTime - transitionStart;
                    float fraction = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / transitionDuration));
                    foreach (var movement in moved)
                    {
                        if (!packages.TryGetValue(movement.PackageId, out var package)) continue;
                        var from = starts[movement.PackageId];
                        var to = ends[movement.PackageId];
                        var position = Vector3.Lerp(from, to, fraction);
                        if (movement.From == "J1" && movement.To == "B")
                            position.y += BridgeLiftAt(fraction);
                        package.localPosition = position;
                        if (delivered.Contains(movement.PackageId))
                            package.localScale = Vector3.one * Mathf.Lerp(1f, 0.65f, Mathf.InverseLerp(0.72f, 1f, fraction));
                    }
                    UpdateArmTransitions();
                    yield return null;
                }

                foreach (var movement in moved)
                {
                    if (delivered.Contains(movement.PackageId))
                    {
                        if (packages.TryGetValue(movement.PackageId, out var package))
                            Destroy(package.gameObject);
                        packages.Remove(movement.PackageId);
                    }
                    else if (packages.TryGetValue(movement.PackageId, out var package))
                    {
                        package.localPosition = ends[movement.PackageId];
                        package.localScale = Vector3.one;
                    }
                }
            }
            finally
            {
                IsPresenting = false;
            }
        }

        private IEnumerator AnimateArms(float duration)
        {
            float started = Time.unscaledTime;
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed = Time.unscaledTime - started;
                UpdateArmTransitions();
                yield return null;
            }
            armTransitions.Clear();
        }

        private void UpdateArmTransitions()
        {
            float fraction = Mathf.SmoothStep(0, 1,
                Mathf.Clamp01((Time.unscaledTime - transitionStart) / Mathf.Max(transitionDuration, 0.01f)));
            foreach (var transition in armTransitions)
                if (pivots.TryGetValue(transition.NodeId, out var pivot))
                    pivot.localRotation = Quaternion.Slerp(transition.From, transition.To, fraction);
            if (fraction >= 1) armTransitions.Clear();
        }

        private void CreateDeck()
        {
            if (boardRoot == null || assets == null || assets.BoardDeck == null) return;
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "Mechanical board deck";
            deck.transform.SetParent(boardRoot, false);
            deck.transform.localPosition = new Vector3(0, -0.12f, 0);
            deck.transform.localScale = new Vector3(3.9f, 0.24f, 5.85f);
            SetLayerRecursively(deck, BoardLayer);
            Assign(deck.GetComponent<Renderer>(), assets.BoardDeck);
            RemoveCollider(deck);
        }

        private void CreateEdge(EdgeDefinition edge, NodeDefinition from)
        {
            var start = NodePosition(engine.Level.Nodes.First(node => node.Id == edge.From));
            var end = NodePosition(engine.Level.Nodes.First(node => node.Id == edge.To));
            var distance = Vector3.Distance(start, end);
            int count = Mathf.RoundToInt(distance / ModuleLength);
            if (count < 1)
                throw new InvalidOperationException("Level edge is too short for a mechanical conveyor: " + edge.Id);

            var active = new List<Renderer>(count);
            var inactive = new List<Renderer>(count);
            bool selected = from.Type != NodeType.Diverter || engine.Route(from, state.Switches) == edge.To;
            for (int index = 0; index < count; index++)
            {
                float t = (index + 0.5f) / count;
                var center = Vector3.Lerp(start, end, t);
                center.y = RouteHeight(edge.Id, t) - DeckHeight;
                var tangent = RouteTangent(edge.Id, start, end, t);
                var module = Instantiate(assets.ConveyorModule, boardRoot);
                module.name = $"Conveyor {edge.Id} {index}";
                module.transform.localPosition = center;
                module.transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up)
                    * Quaternion.Euler(0, -90, 0);
                SetLayerRecursively(module, BoardLayer);
                var markers = CreateRouteMarkers(module.transform, edge.Id, index, selected);
                active.Add(markers.Active);
                inactive.Add(markers.Inactive);
            }
            activeRoutes.Add(edge.Id, active);
            inactiveRoutes.Add(edge.Id, inactive);
        }

        private (Renderer Active, Renderer Inactive) CreateRouteMarkers(
            Transform parent, string edgeId, int index, bool selected)
        {
            var active = GameObject.CreatePrimitive(PrimitiveType.Cube);
            active.name = $"Active route {edgeId} {index}";
            active.transform.SetParent(parent, false);
            active.transform.localPosition = new Vector3(0, 0.39f, -0.005f);
            active.transform.localScale = new Vector3(0.4f, 0.018f, 0.025f);
            SetLayerRecursively(active, BoardLayer);
            Assign(active.GetComponent<Renderer>(), assets.ActiveRoute);
            RemoveCollider(active);
            active.SetActive(selected);

            var inactive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            inactive.name = $"Inactive route {edgeId} {index}";
            inactive.transform.SetParent(parent, false);
            inactive.transform.localPosition = new Vector3(0, 0.39f, -0.005f);
            inactive.transform.localScale = new Vector3(0.4f, 0.018f, 0.025f);
            SetLayerRecursively(inactive, BoardLayer);
            Assign(inactive.GetComponent<Renderer>(), assets.InactiveRoute);
            RemoveCollider(inactive);
            inactive.SetActive(!selected);
            return (active.GetComponent<Renderer>(), inactive.GetComponent<Renderer>());
        }

        private void SetRouteState(SwitchState switches, bool animate)
        {
            var previousAngles = pivots.ToDictionary(pair => pair.Key,
                pair => pair.Value.localRotation, StringComparer.Ordinal);
            foreach (var edge in engine.Level.Edges)
            {
                var source = engine.Level.Nodes.First(node => node.Id == edge.From);
                bool selected = source.Type != NodeType.Diverter || engine.Route(source, switches) == edge.To;
                SetRouteIndicators(edge.Id, selected);
            }

            armTransitions.Clear();
            foreach (var pair in pivots)
            {
                var node = engine.Level.Nodes.First(item => item.Id == pair.Key);
                var targetNodeId = engine.Route(node, switches);
                var origin = NodePosition(node);
                var target = NodePosition(engine.Level.Nodes.First(item => item.Id == targetNodeId));
                var direction = new Vector3(target.x - origin.x, 0, target.z - origin.z);
                float yaw = Mathf.Atan2(direction.x, direction.z);
                var to = Quaternion.Euler(0, yaw, 0);
                if (animate && previousAngles.TryGetValue(pair.Key, out var from)
                    && Quaternion.Angle(from, to) > 0.1f)
                    armTransitions.Add(new ArmTransition { NodeId = pair.Key, From = from, To = to });
                else
                    pair.Value.localRotation = to;
            }
            if (armTransitions.Count > 0)
            {
                transitionStart = Time.unscaledTime;
                transitionDuration = 0.24f;
            }
        }

        private void SetRouteIndicators(string edgeId, bool selected)
        {
            SetRendererMaterial(activeRoutes[edgeId], assets.ActiveRoute, selected);
            SetRendererMaterial(inactiveRoutes[edgeId], assets.InactiveRoute, !selected);
        }

        private void SetRendererMaterial(List<Renderer> renderers, Material material, bool enabled)
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.gameObject.SetActive(enabled);
                if (!enabled) continue;
                renderer.sharedMaterial = material;
            }
        }

        private void CreatePackages(SimulationState current)
        {
            foreach (var package in packages.Values)
                if (package != null) Destroy(package.gameObject);
            packages.Clear();
            foreach (var package in current.Packages)
            {
                var prefab = package.Package.Kind == "red" ? assets.RedPackage : assets.BluePackage;
                if (prefab == null)
                    throw new InvalidOperationException("Missing package prefab for " + package.Package.Kind);
                var instance = Instantiate(prefab, boardRoot);
                instance.name = "Package " + package.Package.Id;
                instance.transform.localPosition = NodePosition(engine.Level.Nodes.First(node => node.Id == package.NodeId));
                packages.Add(package.Package.Id, instance.transform);
            }
        }

        internal static Vector3 NodePosition(NodeDefinition node)
        {
            return new Vector3((node.X - 0.5f) * HorizontalScale, 0,
                (node.Y - 0.5f) * DepthScale);
        }

        internal Vector3 PackagePosition(string packageId) =>
            packages.TryGetValue(packageId, out var package)
                ? package.localPosition
                : throw new ArgumentException("Unknown package: " + packageId, nameof(packageId));

        internal void RenderImmediately()
        {
            ConfigureCameraViewport();
            if (visible && boardCamera != null && boardCamera.enabled)
                boardCamera.Render();
        }

        private static bool HasRequiredAssets(MechanicalBoardAssets value) =>
            value != null
            && value.ConveyorModule != null
            && value.DiverterAssembly != null
            && value.RedPackage != null
            && value.BluePackage != null
            && value.SourceStation != null
            && value.RedDestination != null
            && value.BlueDestination != null
            && value.BoardDeck != null
            && value.ActiveRoute != null
            && value.InactiveRoute != null;

        internal static float RouteHeight(string edgeId, float t)
        {
            if (edgeId != "J1B") return DeckHeight;
            float lift;
            if (t <= 0.12f || t >= 0.88f) lift = 0;
            else if (t < 0.42f)
            {
                float ratio = (t - 0.12f) / 0.30f;
                lift = BridgeLift * 0.5f * (1 - Mathf.Cos(Mathf.PI * ratio));
            }
            else if (t <= 0.58f) lift = BridgeLift;
            else
            {
                float ratio = (t - 0.58f) / 0.30f;
                lift = BridgeLift * 0.5f * (1 + Mathf.Cos(Mathf.PI * ratio));
            }
            return DeckHeight + lift;
        }

        internal static float BridgeLiftAt(float t) =>
            Mathf.Max(0, RouteHeight("J1B", t) - DeckHeight);

        internal static Vector3 RouteTangent(string edgeId, Vector3 start, Vector3 end, float t)
        {
            float delta = 0.001f;
            float before = Mathf.Max(0, t - delta);
            float after = Mathf.Min(1, t + delta);
            var first = Vector3.Lerp(start, end, before);
            var second = Vector3.Lerp(start, end, after);
            first.y = RouteHeight(edgeId, before);
            second.y = RouteHeight(edgeId, after);
            return (second - first).normalized;
        }

        private void ConfigureCameraViewport()
        {
            if (viewport == null || boardCamera == null || !viewport.gameObject.activeInHierarchy) return;
            float scale = viewport.GetComponentInParent<Canvas>().scaleFactor;
            int width = Mathf.Max(64, Mathf.RoundToInt(viewport.rect.width * scale));
            int height = Mathf.Max(64, Mathf.RoundToInt(viewport.rect.height * scale));
            if (renderTexture == null || renderTexture.width != width || renderTexture.height != height)
            {
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Destroy(renderTexture);
                }
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Pulse Factory mechanical board",
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                renderTexture.Create();
                boardCamera.targetTexture = renderTexture;
                output.texture = renderTexture;
            }
            boardCamera.aspect = (float)width / height;
            float requiredHeight = Mathf.Max(5.85f, 5.2f) * 0.5f;
            float requiredWidth = 3.9f * 0.5f / boardCamera.aspect;
            boardCamera.orthographicSize = Mathf.Max(requiredHeight, requiredWidth) * 1.25f;
            boardCamera.transform.position = new Vector3(0.18f, 19.5f, -0.65f);
            boardCamera.transform.rotation = Quaternion.Euler(88, 0, 0);
        }

        private void LateUpdate()
        {
            if (viewport == null) return;
            ConfigureCameraViewport();
            if (!visible || !boardCamera.enabled) return;
            if (armTransitions.Count > 0) UpdateArmTransitions();
        }

        private static Transform FindChild(Transform root, string exactName)
        {
            var pending = new Stack<Transform>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (current.name.IndexOf(exactName, StringComparison.Ordinal) >= 0)
                    return current;
                for (int i = 0; i < current.childCount; i++)
                    pending.Push(current.GetChild(i));
            }
            return null;
        }

        private static void Assign(Renderer renderer, Material material)
        {
            if (renderer != null && material != null) renderer.sharedMaterial = material;
        }

        private static void RemoveCollider(GameObject target)
        {
            foreach (var component in target.GetComponents<Component>())
                if (component.GetType().Name.EndsWith("Collider", StringComparison.Ordinal))
                    Destroy(component);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private void OnDestroy()
        {
            if (boardCamera != null) boardCamera.targetTexture = null;
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }
    }
}
