using System;
using System.Collections.Generic;
using System.Linq;
using PulseFactory.Core;
using PulseFactory.Localization;
using UnityEngine;
using EventType = PulseFactory.Core.EventType;

namespace PulseFactory.Presentation
{
    public sealed class BoardRenderer
    {
        private readonly SimulationEngine engine;
        private readonly Dictionary<string, NodeDefinition> nodes;
        private readonly Dictionary<string, PackageDefinition> packages;
        private SimulationEvent[] movements = Array.Empty<SimulationEvent>();
        private float animationStart;
        private float animationDuration;

        public BoardRenderer(SimulationEngine engine)
        {
            this.engine = engine;
            nodes = engine.Level.Nodes.ToDictionary(n => n.Id);
            packages = engine.Level.Spawns.ToDictionary(s => s.Package.Id, s => s.Package);
            foreach (var package in packages.Values)
                if (package.Kind != "red" && package.Kind != "blue")
                    throw new ArgumentException("The placeholder renderer supports red and blue kinds only: " + package.Kind);
        }

        public bool IsAnimating(float now) => movements.Length > 0 && now < animationStart + animationDuration;
        public void Animate(SimulationResult result, float now, bool reducedMotion)
        {
            movements = result.Events.Where(e => e.Type == EventType.Moved).ToArray();
            animationStart = now;
            animationDuration = reducedMotion ? 0f : 0.32f;
        }
        public void Reset() { movements = Array.Empty<SimulationEvent>(); }

        public void Draw(Rect bounds, SimulationState state, LocalizationCatalog text, GUIStyle label, float now)
        {
            Fill(bounds, new Color(0.08f, 0.12f, 0.19f));
            float minX = nodes.Values.Min(n => n.X), maxX = nodes.Values.Max(n => n.X);
            float minY = nodes.Values.Min(n => n.Y), maxY = nodes.Values.Max(n => n.Y);
            Vector2 Position(string id)
            {
                var n = nodes[id];
                float x = maxX == minX ? 0.5f : (n.X - minX) / (maxX - minX);
                float y = maxY == minY ? 0.5f : (n.Y - minY) / (maxY - minY);
                return new Vector2(bounds.x + 60 + x * (bounds.width - 120), bounds.y + 48 + y * (bounds.height - 125));
            }

            foreach (var edge in engine.Level.Edges)
            {
                var from = nodes[edge.From];
                bool selected = from.Type != NodeType.Diverter || engine.Route(from, state.Switches) == edge.To;
                var color = selected ? new Color(0.3f, 0.9f, 0.65f) : new Color(0.28f, 0.32f, 0.39f);
                var start = Position(edge.From);
                var end = Position(edge.To);
                Line(start, end, selected ? 6 : 3, color);
                var direction = (end - start).normalized;
                var tip = Vector2.Lerp(start, end, 0.68f);
                var normal = new Vector2(-direction.y, direction.x);
                Line(tip, tip - direction * 13 + normal * 7, 3, color);
                Line(tip, tip - direction * 13 - normal * 7, 3, color);
            }

            foreach (var node in engine.Level.Nodes)
            {
                var point = Position(node.Id);
                var color = node.Type == NodeType.Destination ? PackageColor(node.Accepts)
                    : node.Type == NodeType.Diverter ? new Color(0.45f, 0.32f, 0.7f) : new Color(0.2f, 0.35f, 0.5f);
                Fill(new Rect(point.x - 26, point.y - 24, 52, 48), color);
                string caption;
                if (node.Type == NodeType.Destination)
                {
                    GUI.Label(new Rect(point.x - 24, point.y - 20, 48, 36), text.Display(node.Accepts + "Symbol"), label);
                    caption = text.Display(node.Accepts) + "\n" + text.Display("destinationCount",
                        state.Deliveries.Count(d => d.DestinationId == node.Id), node.RequiredCount);
                }
                else if (node.Type == NodeType.Diverter) caption = text.Display("diverter", node.Group.Value);
                else if (node.Type == NodeType.Source) caption = text.Display("source");
                else caption = node.Id;
                GUI.Label(new Rect(point.x - 60, point.y + 25, 120, 45), caption, label);
            }

            if (IsAnimating(now))
            {
                float fraction = Mathf.SmoothStep(0, 1, Mathf.Clamp01((now - animationStart) / animationDuration));
                foreach (var move in movements)
                    DrawPackage(Vector2.Lerp(Position(move.From), Position(move.To), fraction), packages[move.PackageId], text, label);
            }
            else
                foreach (var package in state.Packages)
                    DrawPackage(Position(package.NodeId), package.Package, text, label);
        }

        private static Color PackageColor(string kind) =>
            kind == "red" ? new Color(0.85f, 0.22f, 0.25f) : new Color(0.15f, 0.48f, 0.95f);

        private static void DrawPackage(Vector2 position, PackageDefinition package, LocalizationCatalog text, GUIStyle label)
        {
            Fill(new Rect(position.x - 19, position.y - 19, 38, 38), Color.white);
            Fill(new Rect(position.x - 16, position.y - 16, 32, 32), PackageColor(package.Kind));
            GUI.Label(new Rect(position.x - 16, position.y - 17, 32, 34), text.Display(package.Kind + "Symbol"), label);
        }

        public static void Fill(Rect rectangle, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rectangle, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void Line(Vector2 start, Vector2 end, float thickness, Color color)
        {
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = LineMatrix(previous, start, end);
            Fill(new Rect(0, -thickness / 2, Vector2.Distance(start, end), thickness), color);
            GUI.matrix = previous;
        }

        internal static Matrix4x4 LineMatrix(Matrix4x4 parent, Vector2 start, Vector2 end)
        {
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            // Rotate in board-local space before applying the portrait viewport transform.
            return parent * Matrix4x4.TRS(start, Quaternion.Euler(0, 0, angle), Vector3.one);
        }
    }
}
