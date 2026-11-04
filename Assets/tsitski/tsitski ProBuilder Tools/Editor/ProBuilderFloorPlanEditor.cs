using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(ProBuilderFloorPlan))]
    [CanEditMultipleObjects]
    public class ProBuilderFloorPlanEditor : UnityEditor.Editor {
        private static readonly int RectSelectHash = "ProBuilderFloorPlanRectSelect".GetHashCode();

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private PathPointHandles _path;

        private void OnEnable() => _path = new PathPointHandles(Repaint);

        private void OnDisable() {
            UnityEditor.Tools.hidden = false;
            SetSuspended(target as ProBuilderFloorPlan, false);
        }

        private static void SetSuspended(ProBuilderFloorPlan plan, bool suspended) {
            if (plan && plan.TryGetComponent(out ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(ProBuilderFloorPlan.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;
            SerializedProperty floorProperty = serializedObject.FindProperty(nameof(ProBuilderFloorPlan.floor));
            bool usesFloor = floorProperty.hasMultipleDifferentValues || floorProperty.objectReferenceValue;

            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (property.name is nameof(ProBuilderFloorPlan.height) or nameof(ProBuilderFloorPlan.thickness)
                        or nameof(ProBuilderFloorPlan.alignment) or nameof(ProBuilderFloorPlan.wallsAroundHoles)
                        or nameof(ProBuilderFloorPlan.squareCorners) or nameof(ProBuilderFloorPlan.loopCutStep)
                        or nameof(ProBuilderFloorPlan.verticalLoopCutStep) or nameof(ProBuilderFloorPlan.roundingSegments)
                        or nameof(ProBuilderFloorPlan.floorPlanOutlineSettings) && usesStats)
                    continue;

                bool generated = usesFloor && property.name is nameof(ProBuilderFloorPlan.path)
                    or nameof(ProBuilderFloorPlan.pathRadii) or nameof(ProBuilderFloorPlan.closed);
                using (new EditorGUI.DisabledScope(generated))
                    EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            using (new EditorGUILayout.HorizontalScope()) {
                if (!serializedObject.isEditingMultipleObjects) {
                    ProBuilderFloorPlan plan = (ProBuilderFloorPlan)target;
                    if (!plan.stats && GUILayout.Button("Save As Floor Plan Stats…"))
                        SaveStats(plan);
                    if (plan.stats && GUILayout.Button("Copy Stats Into Floor Plan")) {
                        Undo.RecordObject(plan, "Copy Floor Plan Stats");
                        plan.height = plan.stats.height;
                        plan.thickness = plan.stats.thickness;
                        plan.alignment = plan.stats.alignment;
                        plan.wallsAroundHoles = plan.stats.wallsAroundHoles;
                        plan.squareCorners = plan.stats.squareCorners;
                        plan.loopCutStep = plan.stats.loopCutStep;
                        plan.verticalLoopCutStep = plan.stats.verticalLoopCutStep;
                        plan.roundingSegments = plan.stats.roundingSegments;
                        plan.floorPlanOutlineSettings = plan.stats.floorPlanOutlineSettings.Select(c => c?.Copy()).ToArray();
                        plan.stats = null;
                        plan.Rebuild();
                    }
                }

                if (GUILayout.Button("Rebuild"))
                    foreach (ProBuilderFloorPlan plan in targets.Cast<ProBuilderFloorPlan>())
                        plan.Rebuild();
            }
        }

        private static void SaveStats(ProBuilderFloorPlan plan) {
            string path = StatsSavePanel.Show("Save Floor Plan Stats", "Floor Plan Stats",
                "Save this floor plan's settings as a reusable Floor Plan Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            ProBuilderFloorPlanStats asset = CreateInstance<ProBuilderFloorPlanStats>();
            asset.height = plan.height;
            asset.thickness = plan.thickness;
            asset.alignment = plan.alignment;
            asset.wallsAroundHoles = plan.wallsAroundHoles;
            asset.squareCorners = plan.squareCorners;
            asset.loopCutStep = plan.loopCutStep;
            asset.verticalLoopCutStep = plan.verticalLoopCutStep;
            asset.roundingSegments = plan.roundingSegments;
            asset.floorPlanOutlineSettings = plan.floorPlanOutlineSettings.Select(c => c?.Copy()).ToArray();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(plan, "Assign Floor Plan Stats");
            plan.stats = asset;
            UnityEditor.EditorUtility.SetDirty(plan);
        }

        private void OnSceneGUI() {
            ProBuilderFloorPlan plan = (ProBuilderFloorPlan)target;
            bool editing = plan && !plan.floor && plan.path != null && UnityEditor.Selection.gameObjects.Length == 1;

            if (plan && plan.floor && plan.path is { Count: > 1 })
                DrawGeneratedPath(plan);

            if (!editing) {
                _path.selected.Clear();
                UnityEditor.Tools.hidden = false;
                SetSuspended(plan, false);
                return;
            }

            if (_path.selected.Count > 0)
                SetSuspended(plan, true);

            using (new Handles.DrawingScope(Color.white, plan.transform.localToWorldMatrix)) {
                PathEdit edit = _path.Do(plan.path, plan.pathRadii, plan.closed, plan.transform.rotation);
                if (edit != null) {
                    Undo.RecordObject(plan, $"{edit.undoName} (Floor Plan)");
                    plan.path = edit.points;
                    plan.pathRadii = edit.radii;
                    SetSuspended(plan, _path.selected.Count > 0);
                    RebuildQueue.Run(plan, plan.Rebuild);
                    return;
                }

                if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect))
                    _path.SelectInRect(rect, plan.path);
            }

            UnityEditor.Tools.hidden = _path.selected.Count > 0;
            SetSuspended(plan, UnityEditor.Tools.hidden);
        }

        private static void DrawGeneratedPath(ProBuilderFloorPlan plan) {
            if (Event.current.type != EventType.Repaint)
                return;

            using (new Handles.DrawingScope(HandleTheme.Path, plan.transform.localToWorldMatrix)) {
                List<Vector3> points = plan.path;
                Vector3[] line = plan.closed ? points.Append(points[0]).ToArray() : points.ToArray();
                Handles.DrawAAPolyLine(3f, line);
                foreach (Vector3 point in points)
                    Handles.DotHandleCap(0, point, Quaternion.identity, UnityEditor.HandleUtility.GetHandleSize(point) * 0.04f, EventType.Repaint);
            }
        }
    }

    [CustomEditor(typeof(ProBuilderFloorPlanStats))]
    public class ProBuilderFloorPlanStatsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
        }
    }

    [InitializeOnLoad]
    internal static class ProBuilderFloorPlanWatcher {
        static ProBuilderFloorPlanWatcher() {
            ProBuilderEditor.afterMeshModification += FloorsChanged;
            EditorMeshUtility.meshOptimized += (mesh, _) => FloorsChanged(new[] { mesh });
            ProBuilderPolygon.Rebuilt += mesh => FloorsChanged(new[] { mesh });
            Undo.undoRedoPerformed += RebuildAll;
        }

        private static void FloorsChanged(IEnumerable<ProBuilderMesh> meshes) {
            HashSet<ProBuilderMesh> changed = new(meshes.Where(m => m));
            foreach (ProBuilderFloorPlan plan in ProBuilderFloorPlan.All.ToArray())
                if (plan && changed.Contains(plan.floor))
                    plan.QueueRebuild();
        }

        private static void RebuildAll() {
            foreach (ProBuilderFloorPlan plan in ProBuilderFloorPlan.All.ToArray())
                if (plan)
                    plan.QueueRebuild();
        }
    }
}
