using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(ProBuilderStairs))]
    [CanEditMultipleObjects]
    public class ProBuilderStairsEditor : UnityEditor.Editor {
        private static readonly int RectSelectHash = "ProBuilderStairsRectSelect".GetHashCode();

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private PathPointHandles _path;

        private void OnEnable() => _path = new PathPointHandles(Repaint);

        private void OnDisable() {
            Tools.hidden = false;
            SetSuspended(target as ProBuilderStairs, false);
        }

        private static void SetSuspended(ProBuilderStairs stairs, bool suspended) {
            if (stairs && stairs.TryGetComponent(out UnityEngine.ProBuilder.ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(ProBuilderStairs.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;

            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (property.name is nameof(ProBuilderStairs.width) or nameof(ProBuilderStairs.stepHeight)
                        or nameof(ProBuilderStairs.solid) or nameof(ProBuilderStairs.thickness)
                        or nameof(ProBuilderStairs.generateCutter) or nameof(ProBuilderStairs.cutterHeadroom)
                        or nameof(ProBuilderStairs.leftTrim) or nameof(ProBuilderStairs.rightTrim)
                        or nameof(ProBuilderStairs.smoothCollider) && usesStats)
                    continue;

                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            if (serializedObject.isEditingMultipleObjects)
                return;

            ProBuilderStairs stairs = (ProBuilderStairs)target;
            EditorGUILayout.LabelField("Steps", stairs.stepCount.ToString());

            DrawProfile(stairs.ProfileLines());

            using (new EditorGUILayout.HorizontalScope()) {
                if (!stairs.stats && GUILayout.Button("Save As Stairs Stats…"))
                    SaveStats(stairs);
                if (stairs.stats && GUILayout.Button("Copy Stats Into Stairs")) {
                    Undo.RecordObject(stairs, "Copy Stairs Stats");
                    stairs.width = stairs.stats.width;
                    stairs.stepHeight = stairs.stats.stepHeight;
                    stairs.solid = stairs.stats.solid;
                    stairs.thickness = stairs.stats.thickness;
                    stairs.generateCutter = stairs.stats.generateCutter;
                    stairs.cutterHeadroom = stairs.stats.cutterHeadroom;
                    stairs.leftTrim = stairs.stats.leftTrim;
                    stairs.rightTrim = stairs.stats.rightTrim;
                    stairs.smoothCollider = stairs.stats.smoothCollider;
                    stairs.stats = null;
                    stairs.Rebuild();
                }
                if (GUILayout.Button("Rebuild"))
                    stairs.Rebuild();
            }
        }

        public static void DrawProfile(IEnumerable<List<Vector2>> lines) {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                ProfilePreview.Graph(lines
                    .Select(points => new ProfileLine { points = points, color = ProfilePreview.Outline, closed = true })
                    .ToList(), null, 140f);
        }

        private static void SaveStats(ProBuilderStairs stairs) {
            string path = StatsSavePanel.Show("Save Stairs Stats", "Stairs Stats",
                "Save these stairs' settings as a reusable Stairs Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            ProBuilderStairsStats asset = CreateInstance<ProBuilderStairsStats>();
            asset.width = stairs.width;
            asset.stepHeight = stairs.stepHeight;
            asset.solid = stairs.solid;
            asset.thickness = stairs.thickness;
            asset.generateCutter = stairs.generateCutter;
            asset.cutterHeadroom = stairs.cutterHeadroom;
            asset.leftTrim = stairs.leftTrim;
            asset.rightTrim = stairs.rightTrim;
            asset.smoothCollider = stairs.smoothCollider;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(stairs, "Assign Stairs Stats");
            stairs.stats = asset;
            EditorUtility.SetDirty(stairs);
        }

        private void OnSceneGUI() {
            ProBuilderStairs stairs = (ProBuilderStairs)target;
            bool editing = stairs && stairs.path != null && Selection.gameObjects.Length == 1;

            if (!editing) {
                _path.selected.Clear();
                Tools.hidden = false;
                SetSuspended(stairs, false);
                return;
            }

            if (_path.selected.Count > 0)
                SetSuspended(stairs, true);

            using (new Handles.DrawingScope(Color.white, stairs.transform.localToWorldMatrix)) {
                PathEdit edit = _path.Do(stairs.path, stairs.pathRadii, false, stairs.transform.rotation);
                if (edit != null) {
                    Undo.RecordObject(stairs, $"{edit.undoName} (Stairs)");
                    stairs.path = edit.points;
                    stairs.pathRadii = edit.radii;
                    SetSuspended(stairs, _path.selected.Count > 0);
                    RebuildQueue.Run(stairs, stairs.Rebuild);
                    return;
                }

                if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect))
                    _path.SelectInRect(rect, stairs.path);
            }

            Tools.hidden = _path.selected.Count > 0;
            SetSuspended(stairs, Tools.hidden);
        }
    }

    [CustomEditor(typeof(ProBuilderStairsStats))]
    public class ProBuilderStairsStatsEditor : UnityEditor.Editor {
        private const int PreviewSteps = 6;
        private const float PreviewTreadDepth = 0.28f;

        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            ProBuilderStairsEditor.DrawProfile(new[] {
                ProBuilderStairs.SampleProfile((ProBuilderStairsStats)target, PreviewSteps, PreviewTreadDepth)
            });
        }
    }
}
