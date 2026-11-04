using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProBuilderMesh = UnityEngine.ProBuilder.ProBuilderMesh;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(ProBuilderArray))]
    [CanEditMultipleObjects]
    public class ProBuilderArrayEditor : UnityEditor.Editor {
        private static readonly int RectSelectHash = "ProBuilderArrayRectSelect".GetHashCode();

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private PathPointHandles _path;

        private void OnEnable() => _path = new PathPointHandles(Repaint);

        private void OnDisable() {
            Tools.hidden = false;
            SetSuspended(target as ProBuilderArray, false);
        }

        private static void SetSuspended(ProBuilderArray array, bool suspended) {
            if (array && array.TryGetComponent(out ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(ProBuilderArray.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;
            ProBuilderArray array = (ProBuilderArray)target;
            bool pathInStats = usesStats && array.pathInStats;
            bool sourceInStats = usesStats && array.stats.source;
            bool pillarInStats = usesStats && array.stats.pillarStats;
            bool usesPillar = !serializedObject.isEditingMultipleObjects && array.activePillarStats;
            ProBuilderArrayStats[] previousStats = targets.Cast<ProBuilderArray>().Select(a => a.stats).ToArray();

            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (property.name is nameof(ProBuilderArray.spacing) or nameof(ProBuilderArray.offset)
                        or nameof(ProBuilderArray.fitSpacing) or nameof(ProBuilderArray.alignToPath)
                        or nameof(ProBuilderArray.splitAtCorners) or nameof(ProBuilderArray.divisionLength)
                        or nameof(ProBuilderArray.skipCorners) or nameof(ProBuilderArray.skipDivisions)
                        or nameof(ProBuilderArray.roundingSegments) && usesStats)
                    continue;
                if (property.name == nameof(ProBuilderArray.source) && (sourceInStats || usesPillar))
                    continue;
                if (property.name == nameof(ProBuilderArray.pillarStats) && pillarInStats)
                    continue;
                if (property.name is nameof(ProBuilderArray.pillarHeight) or nameof(ProBuilderArray.mergeIntoMesh) && (usesStats || !usesPillar))
                    continue;
                if (property.name == nameof(ProBuilderArray.cornerSide) && !usesPillar)
                    continue;
                if (property.name is nameof(ProBuilderArray.path) or nameof(ProBuilderArray.pathRadii)
                        or nameof(ProBuilderArray.closed) && pathInStats)
                    continue;

                EditorGUILayout.PropertyField(property, true);
            }

            if (serializedObject.ApplyModifiedProperties()) {
                for (int i = 0; i < targets.Length; i++) {
                    ProBuilderArray changed = (ProBuilderArray)targets[i];
                    if (changed.stats != previousStats[i] && changed.stats && changed.stats.storeSource && changed.stats.sourceMesh.hasData)
                        LoadSource(changed);
                }
            }

            if (serializedObject.isEditingMultipleObjects)
                return;

            ArraySpacingPreview.Draw(array.activeSpacingSettings);

            using (new EditorGUILayout.HorizontalScope()) {
                ProBuilderMesh source = array.activeSource;
                if (source && GUILayout.Button("Select Source"))
                    Selection.activeObject = source.gameObject;
                if (!array.source && !sourceInStats && GUILayout.Button("Create Source")) {
                    Undo.RecordObject(array, "Create Array Source");
                    array.CreateSource();
                    array.Rebuild();
                }
                if (GUILayout.Button("Rebuild"))
                    array.Rebuild();
            }

            bool canRotate = array.source && array.source.gameObject.scene.IsValid() || array.sourceInStats;
            using (new EditorGUI.DisabledScope(!canRotate))
                if (GUILayout.Button("Rotate Source 90° Y"))
                    RotateSource(array, Quaternion.Euler(0f, 90f, 0f));

            using (new EditorGUILayout.HorizontalScope()) {
                if (!array.stats && GUILayout.Button("Save As Array Stats…"))
                    SaveStats(array);
                if (array.stats && GUILayout.Button("Copy Stats Into Array"))
                    CopyStats(array);
            }

            if (array.stats) {
                using (new EditorGUILayout.HorizontalScope()) {
                    using (new EditorGUI.DisabledScope(!array.source))
                        if (GUILayout.Button(new GUIContent("Save Source To Stats", "Copy this array's source mesh into the stats.")))
                            ProBuilderArrayWatcher.StoreSource(array);

                    using (new EditorGUI.DisabledScope(!array.stats.sourceMesh.hasData))
                        if (GUILayout.Button(new GUIContent("Load Source From Stats", "Replace this array's source mesh with the one in the stats.")))
                            LoadSource(array);
                }
            }
        }

        private static void SaveStats(ProBuilderArray array) {
            string path = StatsSavePanel.Show("Save Array Stats", "Array Stats",
                "Save this array's settings as a reusable Array Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            ProBuilderArrayStats asset = CreateInstance<ProBuilderArrayStats>();
            asset.spacing = array.spacing;
            asset.offset = array.offset;
            asset.fitSpacing = array.fitSpacing;
            asset.splitAtCorners = array.splitAtCorners;
            asset.divisionLength = array.divisionLength;
            asset.skipCorners = array.skipCorners;
            asset.skipDivisions = array.skipDivisions;
            asset.alignToPath = array.alignToPath;
            asset.roundingSegments = array.roundingSegments;
            asset.pillarStats = array.pillarStats;
            asset.pillarHeight = array.pillarHeight;
            asset.mergeIntoMesh = array.mergeIntoMesh;
            asset.path = new List<Vector3>(array.path);
            asset.pathRadii = new List<float>(array.pathRadii);
            asset.closed = array.closed;
            if (array.source && EditorUtility.IsPersistent(array.source)) {
                asset.source = array.source;
            }
            else if (array.source) {
                asset.storeSource = true;
                asset.sourceMesh = ArraySourceData.Capture(array.source);
            }
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(array, "Assign Array Stats");
            array.stats = asset;
            EditorUtility.SetDirty(array);
        }

        private static void CopyStats(ProBuilderArray array) {
            ProBuilderArrayStats stats = array.stats;
            Undo.RecordObject(array, "Copy Array Stats");
            array.spacing = stats.spacing;
            array.offset = stats.offset;
            array.fitSpacing = stats.fitSpacing;
            array.splitAtCorners = stats.splitAtCorners;
            array.divisionLength = stats.divisionLength;
            array.skipCorners = stats.skipCorners;
            array.skipDivisions = stats.skipDivisions;
            array.alignToPath = stats.alignToPath;
            array.roundingSegments = stats.roundingSegments;
            array.pillarHeight = stats.pillarHeight;
            array.mergeIntoMesh = stats.mergeIntoMesh;
            if (stats.pillarStats)
                array.pillarStats = stats.pillarStats;
            if (array.sourceInStats)
                LoadSource(array);
            else if (stats.source)
                array.source = stats.source;
            if (stats.storePath) {
                array.path = new List<Vector3>(stats.path);
                array.pathRadii = new List<float>(stats.pathRadii);
                array.closed = stats.closed;
            }
            array.stats = null;
            array.Rebuild();
        }

        private static void RotateSource(ProBuilderArray array, Quaternion rotation) {
            ProBuilderMesh source = array.source;

            if (source && source.gameObject.scene.IsValid()) {
                Undo.RecordObject(source, "Rotate Array Source");
                source.positions = source.positions.Select(p => rotation * p).ToArray();
                source.ToMesh();
                source.Refresh();
                EditorUtility.SetDirty(source);

                if (array.stats && array.stats.storeSource)
                    ProBuilderArrayWatcher.StoreSource(array);
                else
                    array.Rebuild();
                return;
            }

            if (!array.sourceInStats)
                return;

            Undo.RecordObject(array.stats, "Rotate Array Source");
            ArraySourceData data = array.stats.sourceMesh;
            data.positions = data.positions.Select(p => rotation * p).ToList();
            EditorUtility.SetDirty(array.stats);
            array.stats.NotifyChanged();
        }

        private static void LoadSource(ProBuilderArray array) {
            ArraySourceData data = array.stats.sourceMesh;
            if (array.source && array.source.gameObject.scene.IsValid()) {
                Undo.RecordObjects(new Object[] { array.source, array.source.transform }, "Load Array Source");
                if (array.source.TryGetComponent(out MeshRenderer renderer))
                    Undo.RecordObject(renderer, "Load Array Source");
                data.ApplyTo(array.source);
                EditorUtility.SetDirty(array.source);
            }
            else {
                Undo.RecordObject(array, "Load Array Source");
                array.CreateSourceFrom(data);
            }

            array.Rebuild();
        }

        private void OnSceneGUI() {
            ProBuilderArray array = (ProBuilderArray)target;
            bool editing = array && array.activePath != null && Selection.gameObjects.Length == 1;

            if (!editing) {
                _path.selected.Clear();
                Tools.hidden = false;
                SetSuspended(array, false);
                return;
            }

            if (_path.selected.Count > 0)
                SetSuspended(array, true);

            Matrix4x4 local = array.transform.localToWorldMatrix;
            using (new Handles.DrawingScope(Color.white, local)) {
                PathEdit edit = _path.Do(array.activePath, array.activePathRadii, array.activeClosed, array.transform.rotation);
                if (edit != null) {
                    Undo.RecordObject(array.pathInStats ? array.stats : array, $"{edit.undoName} (Array)");
                    array.activePath = edit.points;
                    array.activePathRadii = edit.radii;

                    ProBuilderArrayStats stats = array.pathInStats ? array.stats : null;
                    if (stats)
                        EditorUtility.SetDirty(stats);

                    SetSuspended(array, _path.selected.Count > 0);
                    if (stats)
                        RebuildQueue.Run(stats, stats.NotifyChanged);
                    RebuildQueue.Run(array, array.Rebuild);
                    return;
                }

                if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect))
                    _path.SelectInRect(rect, array.activePath);
            }

            Tools.hidden = _path.selected.Count > 0;
            SetSuspended(array, Tools.hidden);
        }
    }
}
