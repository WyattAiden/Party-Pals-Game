using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tsitski.ProBuilderTools {
    internal struct ProfileLine {
        public List<Vector2> points;
        public Color color;
        public bool closed;
    }

    internal static class ProfilePreview {
        public static readonly Color Outline = new(1f, 0.4f, 0.1f, 1f);

        private static readonly Color Background = new(0.16f, 0.16f, 0.16f, 1f);
        private static readonly Color Axis = new(1f, 1f, 1f, 0.15f);
        private static readonly Color Guide = new(1f, 1f, 1f, 0.35f);
        private const string ToleranceKey = "Tsitski.ProBuilderTools.TrimProfileSnapTolerance";

        private static float tolerance {
            get => EditorPrefs.GetFloat(ToleranceKey, 0.005f);
            set => EditorPrefs.SetFloat(ToleranceKey, Mathf.Max(0f, value));
        }

        public static void Draw(List<Vector2> outline, bool closed, List<Vector2> snapSource, Action<List<Vector2>> applySnap) =>
            Draw(new List<ProfileLine> { new() { points = outline, color = Outline, closed = closed } }, null, snapSource, applySnap);

        public static void Draw(List<ProfileLine> lines, IReadOnlyList<float> guides, List<Vector2> snapSource, Action<List<Vector2>> applySnap,
            float height = 140f) {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                Graph(lines, guides, height);

                using (new EditorGUILayout.HorizontalScope()) {
                    tolerance = EditorGUILayout.FloatField("Snap Tolerance", tolerance);
                    if (GUILayout.Button("Snap Profile", GUILayout.Width(100f)) && snapSource is { Count: > 0 })
                        applySnap(Snap(snapSource, tolerance));
                }
            }
        }

        public static void Graph(List<ProfileLine> lines, IReadOnlyList<float> guides, float height) {
            Rect rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, Background);
            List<Vector2> all = lines.Where(l => l.points != null).SelectMany(l => l.points).ToList();
            if (all.Count < 2)
                return;

            Vector2 min = Vector2.zero, max = Vector2.zero;
            foreach (Vector2 p in all) {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            Rect inner = new(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f);
            Vector2 extent = Vector2.Max(max - min, Vector2.one * 1e-4f);
            float scale = Mathf.Min(inner.width / extent.x, inner.height / extent.y);
            Vector2 offset = new(
                inner.x + (inner.width - extent.x * scale) * 0.5f,
                inner.y + (inner.height - extent.y * scale) * 0.5f);

            Vector3 ToGui(Vector2 p) => new(offset.x + (p.x - min.x) * scale, offset.y + (max.y - p.y) * scale);

            Handles.color = Axis;
            Handles.DrawLine(new Vector3(rect.x, ToGui(Vector2.zero).y), new Vector3(rect.xMax, ToGui(Vector2.zero).y));
            Handles.DrawLine(new Vector3(ToGui(Vector2.zero).x, rect.y), new Vector3(ToGui(Vector2.zero).x, rect.yMax));

            if (guides != null) {
                Handles.color = Guide;
                foreach (float y in guides) {
                    float guiY = ToGui(new Vector2(0f, y)).y;
                    Handles.DrawDottedLine(new Vector3(rect.x, guiY), new Vector3(rect.xMax, guiY), 3f);
                }
            }

            foreach (ProfileLine line in lines) {
                if (line.points == null || line.points.Count < 2)
                    continue;

                List<Vector3> gui = line.points.Select(ToGui).ToList();
                if (line.closed)
                    gui.Add(gui[0]);

                Handles.color = line.color;
                Handles.DrawAAPolyLine(2f, gui.ToArray());
            }
        }

        public static List<Vector2> Snap(List<Vector2> profile, float tolerance) {
            float[] xs = SnapAxis(profile.Select(p => p.x).ToArray(), tolerance);
            float[] ys = SnapAxis(profile.Select(p => p.y).ToArray(), tolerance);
            return xs.Select((x, i) => new Vector2(x, ys[i])).ToList();
        }

        private static float[] SnapAxis(float[] values, float tolerance) {
            int[] order = Enumerable.Range(0, values.Length).OrderBy(i => values[i]).ToArray();
            float[] snapped = (float[])values.Clone();

            int start = 0;
            for (int i = 1; i <= order.Length; i++) {
                if (i < order.Length && values[order[i]] - values[order[i - 1]] <= tolerance)
                    continue;

                float average = 0f;
                for (int j = start; j < i; j++)
                    average += values[order[j]];
                average /= i - start;

                for (int j = start; j < i; j++)
                    snapped[order[j]] = average;

                start = i;
            }

            return snapped;
        }
    }
}
