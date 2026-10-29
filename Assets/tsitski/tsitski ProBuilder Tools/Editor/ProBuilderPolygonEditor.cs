using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(ProBuilderPolygon))]
    [CanEditMultipleObjects]
    public class ProBuilderPolygonEditor : UnityEditor.Editor {
        private static readonly int RectSelectHash = "ProBuilderPolygonRectSelect".GetHashCode();

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private PathPointHandles _outline;
        private readonly List<PathPointHandles> _holes = new();

        private IEnumerable<PathPointHandles> allHandles => _holes.Prepend(_outline);
        private bool anySelected => allHandles.Any(h => h.selected.Count > 0);

        private void OnEnable() => _outline = CreateHandles(HandleTheme.Path);

        private void OnDisable() {
            UnityEditor.Tools.hidden = false;
            SetSuspended(target as ProBuilderPolygon, false);
        }

        private PathPointHandles CreateHandles(Color color) {
            PathPointHandles handles = new(Repaint) { pathColor = color };
            handles.Selected += () => {
                foreach (PathPointHandles other in allHandles)
                    if (other != handles)
                        other.selected.Clear();
            };
            return handles;
        }

        private PathPointHandles LoopHandles(int index) {
            if (index < 0)
                return _outline;

            while (_holes.Count <= index)
                _holes.Add(CreateHandles(HandleTheme.Secondary));
            return _holes[index];
        }

        private static void SetSuspended(ProBuilderPolygon polygon, bool suspended) {
            if (polygon && polygon.TryGetComponent(out ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(ProBuilderPolygon.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;
            bool pathInStats = usesStats && ((ProBuilderPolygon)target).pathInStats;

            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (property.name == nameof(ProBuilderPolygon.thickness) && usesStats)
                    continue;
                if (property.name is nameof(ProBuilderPolygon.path) or nameof(ProBuilderPolygon.pathRadii)
                        or nameof(ProBuilderPolygon.holes) && pathInStats)
                    continue;

                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            if (serializedObject.isEditingMultipleObjects) {
                if (GUILayout.Button("Rebuild"))
                    foreach (ProBuilderPolygon each in targets.Cast<ProBuilderPolygon>())
                        each.Rebuild();
                return;
            }

            ProBuilderPolygon polygon = (ProBuilderPolygon)target;
            if (polygon.skippedHoles > 0)
                EditorGUILayout.HelpBox($"{polygon.skippedHoles} piece(s) of the polygon couldn't be built after cutting the holes. Try moving the holes slightly.",
                    MessageType.Warning);

            DrawPreview(ProBuilderPolygon.Loop(polygon.activePath, polygon.activePathRadii), polygon.HoleLoops());

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Add Hole"))
                    AddHole(polygon);
                if (!polygon.stats && GUILayout.Button("Save As Polygon Stats…"))
                    SaveStats(polygon);
                if (polygon.stats && GUILayout.Button("Copy Stats Into Polygon")) {
                    Undo.RecordObject(polygon, "Copy Polygon Stats");
                    polygon.thickness = polygon.stats.thickness;
                    if (polygon.stats.savePolygon) {
                        polygon.path = new List<Vector3>(polygon.stats.path);
                        polygon.pathRadii = new List<float>(polygon.stats.pathRadii);
                        polygon.holes = polygon.stats.holes.Select(h => h?.Copy()).ToList();
                    }
                    polygon.stats = null;
                    polygon.Rebuild();
                }
                if (GUILayout.Button("Rebuild"))
                    polygon.Rebuild();
            }
        }

        internal static void DrawPreview(List<Vector2> outline, IEnumerable<List<Vector2>> holes) {
            List<ProfileLine> lines = new() { new ProfileLine { points = outline, color = ProfilePreview.Outline, closed = true } };
            lines.AddRange(holes.Where(h => h.Count >= 3).Select(h => new ProfileLine { points = h, color = HandleTheme.Secondary, closed = true }));

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                ProfilePreview.Graph(lines, null, 160f);
        }

        private static void AddHole(ProBuilderPolygon polygon) {
            List<Vector2> outer = ProBuilderPolygon.Loop(polygon.activePath, polygon.activePathRadii);
            if (outer.Count < 3)
                return;

            Vector2 min = outer.Aggregate(Vector2.Min), max = outer.Aggregate(Vector2.Max);
            Vector2 center = (min + max) * 0.5f;
            const float half = 1f;
            float y = polygon.activePath.Average(p => p.y);

            Object owner = polygon.pathInStats ? polygon.stats : polygon;
            Undo.RecordObject(owner, "Add Polygon Hole");
            polygon.activeHoles = new List<PolygonHole>(polygon.activeHoles) {
                new() {
                    path = new List<Vector3> {
                        new(center.x - half, y, center.y - half), new(center.x + half, y, center.y - half),
                        new(center.x + half, y, center.y + half), new(center.x - half, y, center.y + half)
                    }
                }
            };

            if (polygon.pathInStats) {
                EditorUtility.SetDirty(polygon.stats);
                polygon.stats.NotifyChanged();
            }
            polygon.Rebuild();
        }

        private static void SaveStats(ProBuilderPolygon polygon) {
            string path = StatsSavePanel.Show("Save Polygon Stats", "Polygon Stats",
                "Save this polygon's settings as a reusable Polygon Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            ProBuilderPolygonStats asset = CreateInstance<ProBuilderPolygonStats>();
            asset.thickness = polygon.thickness;
            asset.path = new List<Vector3>(polygon.path);
            asset.pathRadii = new List<float>(polygon.pathRadii);
            asset.holes = polygon.holes.Select(h => h?.Copy()).ToList();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(polygon, "Assign Polygon Stats");
            polygon.stats = asset;
            EditorUtility.SetDirty(polygon);
        }

        private void OnSceneGUI() {
            ProBuilderPolygon polygon = (ProBuilderPolygon)target;
            bool editing = polygon && polygon.activePath != null && Selection.gameObjects.Length == 1;

            if (!editing) {
                foreach (PathPointHandles handles in allHandles)
                    handles.selected.Clear();
                UnityEditor.Tools.hidden = false;
                SetSuspended(polygon, false);
                return;
            }

            if (anySelected)
                SetSuspended(polygon, true);

            int holeCount = polygon.activeHoles.Count;
            if (_holes.Count > holeCount)
                _holes.RemoveRange(holeCount, _holes.Count - holeCount);

            List<int> loops = Enumerable.Range(-1, holeCount + 1).ToList();

            using (new Handles.DrawingScope(Color.white, polygon.transform.localToWorldMatrix)) {
                foreach (int index in loops.OrderBy(i => LoopHandles(i).selected.Count > 0))
                    if (DoLoop(polygon, index))
                        return;

                if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect))
                    foreach (int index in loops) {
                        List<Vector3> points = LoopPoints(polygon, index);
                        if (points != null && LoopHandles(index).SelectInRect(rect, points))
                            break;
                    }
            }

            UnityEditor.Tools.hidden = anySelected;
            SetSuspended(polygon, UnityEditor.Tools.hidden);
        }

        private static List<Vector3> LoopPoints(ProBuilderPolygon polygon, int index) =>
            index < 0 ? polygon.activePath : polygon.activeHoles[index]?.path;

        private bool DoLoop(ProBuilderPolygon polygon, int index) {
            List<Vector3> points = LoopPoints(polygon, index);
            if (points is not { Count: > 0 })
                return false;

            List<float> radii = index < 0 ? polygon.activePathRadii : polygon.activeHoles[index].pathRadii;
            PathEdit edit = LoopHandles(index).Do(points, radii, true, polygon.transform.rotation);
            if (edit == null)
                return false;

            float y = polygon.activePath.Count > 0 ? polygon.activePath.Average(p => p.y) : 0f;
            edit.points = edit.points.Select(p => new Vector3(p.x, y, p.z)).ToList();

            ProBuilderPolygonStats stats = polygon.pathInStats ? polygon.stats : null;
            Undo.RecordObject(stats ? stats : polygon, $"{edit.undoName} (Polygon)");

            if (index < 0) {
                polygon.activePath = edit.points;
                polygon.activePathRadii = edit.radii;
            }
            else {
                PolygonHole hole = polygon.activeHoles[index];
                hole.path = edit.points;
                hole.pathRadii = edit.radii;
            }

            if (stats)
                EditorUtility.SetDirty(stats);

            SetSuspended(polygon, anySelected);
            if (stats)
                RebuildQueue.Run(stats, stats.NotifyChanged);
            RebuildQueue.Run(polygon, polygon.Rebuild);
            return true;
        }
    }

    [CustomEditor(typeof(ProBuilderPolygonStats))]
    [CanEditMultipleObjects]
    public class ProBuilderPolygonStatsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            SerializedProperty savePolygon = serializedObject.FindProperty(nameof(ProBuilderPolygonStats.savePolygon));
            bool hidePolygon = !savePolygon.hasMultipleDifferentValues && !savePolygon.boolValue;

            TsitskiInspector.DrawProperties(serializedObject, name => hidePolygon &&
                name is nameof(ProBuilderPolygonStats.path) or nameof(ProBuilderPolygonStats.pathRadii) or nameof(ProBuilderPolygonStats.holes));

            if (serializedObject.isEditingMultipleObjects || hidePolygon)
                return;

            ProBuilderPolygonStats stats = (ProBuilderPolygonStats)target;
            ProBuilderPolygonEditor.DrawPreview(ProBuilderPolygon.Loop(stats.path, stats.pathRadii),
                (stats.holes ?? new List<PolygonHole>()).Select(h => h == null ? new List<Vector2>() : ProBuilderPolygon.Loop(h.path, h.pathRadii)));
        }
    }

    [InitializeOnLoad]
    internal static class ProBuilderPolygonWatcher {
        static ProBuilderPolygonWatcher() => Undo.undoRedoPerformed += () => {
            foreach (ProBuilderPolygon polygon in ProBuilderPolygon.All.ToArray())
                if (polygon)
                    polygon.QueueRebuild();
        };
    }
}
