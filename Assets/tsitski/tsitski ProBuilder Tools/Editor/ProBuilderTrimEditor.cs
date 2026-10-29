using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProBuilderMesh = UnityEngine.ProBuilder.ProBuilderMesh;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(ProBuilderTrim))]
    [CanEditMultipleObjects]
    public class ProBuilderTrimEditor : UnityEditor.Editor {
        private static readonly int RectSelectHash = "ProBuilderTrimRectSelect".GetHashCode();

        private Vector2 _rectStart;
        private Vector2 _rectEnd;
        private PolygonPointHandles _profile;
        private PathPointHandles _path;

        private static ProBuilderTrimEditor _active;

        internal static bool TryGetSelectedPathPoints(out ProBuilderTrim trim, out HashSet<int> selected) {
            trim = _active ? _active.target as ProBuilderTrim : null;
            selected = trim ? new HashSet<int>(_active._path.selected) : null;
            return trim && Selection.activeGameObject == trim.gameObject && selected.Count > 0;
        }

        internal static void ClearSelectedPathPoints() {
            if (_active)
                _active._path.selected.Clear();
        }

        private void OnEnable() {
            _active = this;
            _profile = new PolygonPointHandles(Repaint) { pointColor = HandleTheme.Profile };
            _path = new PathPointHandles(Repaint);
            _profile.Selected += () => _path.selected.Clear();
            _path.Selected += () => _profile.selected.Clear();
        }

        private void OnDisable() {
            if (_active == this)
                _active = null;
            Tools.hidden = false;
            SetSuspended(target as ProBuilderTrim, false);
        }

        private static void SetSuspended(ProBuilderTrim trim, bool suspended) {
            if (trim && trim.TryGetComponent(out ProBuilderMesh mesh))
                ProBuilderCutSystem.SetSuspended(mesh, suspended);
        }

        private bool anySelected => _path.selected.Count > 0 || _profile.selected.Count > 0;

        public override void OnInspectorGUI() {
            serializedObject.Update();
            SerializedProperty statsProperty = serializedObject.FindProperty(nameof(ProBuilderTrim.stats));
            bool usesStats = !statsProperty.hasMultipleDifferentValues && statsProperty.objectReferenceValue;
            bool pathInStats = usesStats && ((ProBuilderTrim)target).pathInStats;

            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (property.name is nameof(ProBuilderTrim.profile) or nameof(ProBuilderTrim.profileRadii)
                        or nameof(ProBuilderTrim.loopCutStep) or nameof(ProBuilderTrim.roundBevel) or nameof(ProBuilderTrim.flattenAngledEnds)
                        or nameof(ProBuilderTrim.roundingSegments) or nameof(ProBuilderTrim.profileMirror) && usesStats)
                    continue;
                if (property.name is nameof(ProBuilderTrim.path) or nameof(ProBuilderTrim.pathRadii)
                        or nameof(ProBuilderTrim.closed) && pathInStats)
                    continue;

                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            if (serializedObject.isEditingMultipleObjects)
                return;

            ProBuilderTrim trim = (ProBuilderTrim)target;
            ProfilePreview.Draw(trim.RoundedProfile(), true, trim.activeProfile, edited => ApplySnap(trim, edited));

            using (new EditorGUILayout.HorizontalScope()) {
                if (!trim.stats && GUILayout.Button("Save Profile As Trim Stats…"))
                    SaveStats(trim);
                if (trim.stats && GUILayout.Button("Copy Profile Into Trim")) {
                    Undo.RecordObject(trim, "Copy Trim Profile");
                    trim.profile = new List<Vector2>(trim.stats.profile);
                    trim.profileRadii = new List<float>(trim.stats.profileRadii);
                    trim.roundingSegments = trim.stats.roundingSegments;
                    trim.profileMirror = trim.stats.profileMirror;
                    if (trim.stats.storePath) {
                        trim.path = new List<Vector3>(trim.stats.path);
                        trim.pathRadii = new List<float>(trim.stats.pathRadii);
                        trim.closed = trim.stats.closed;
                    }
                    trim.loopCutStep = trim.stats.loopCutStep;
                    trim.roundBevel = trim.stats.roundBevel;
                    trim.flattenAngledEnds = trim.stats.flattenAngledEnds;
                    trim.stats = null;
                    trim.Rebuild();
                }
                if (GUILayout.Button("Rebuild"))
                    trim.Rebuild();
            }
        }


        private static void ApplySnap(ProBuilderTrim trim, List<Vector2> edited) {
            if (trim.stats) {
                ApplySnap(trim.stats, edited);
                return;
            }

            Undo.RecordObject(trim, "Snap Trim Profile");
            trim.profile = edited;
            trim.Rebuild();
        }

        internal static void ApplySnap(ProBuilderTrimStats stats, List<Vector2> edited) {
            Undo.RecordObject(stats, "Snap Trim Profile");
            stats.profile = edited;
            EditorUtility.SetDirty(stats);
            stats.NotifyChanged();
        }

        private static void SaveStats(ProBuilderTrim trim) {
            string path = StatsSavePanel.Show("Save Trim Stats", "Trim Stats",
                "Save this trim's profile as a reusable Trim Stats asset.");
            if (string.IsNullOrEmpty(path))
                return;

            ProBuilderTrimStats asset = CreateInstance<ProBuilderTrimStats>();
            asset.profile = new List<Vector2>(trim.profile);
            asset.profileRadii = new List<float>(trim.profileRadii);
            asset.roundingSegments = trim.roundingSegments;
            asset.profileMirror = trim.profileMirror;
            asset.path = new List<Vector3>(trim.path);
            asset.pathRadii = new List<float>(trim.pathRadii);
            asset.closed = trim.closed;
            asset.loopCutStep = trim.loopCutStep;
            asset.roundBevel = trim.roundBevel;
            asset.flattenAngledEnds = trim.flattenAngledEnds;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(trim, "Assign Trim Stats");
            trim.stats = asset;
            EditorUtility.SetDirty(trim);
        }

        private void OnSceneGUI() {
            ProBuilderTrim trim = (ProBuilderTrim)target;
            bool editing = trim && trim.activePath != null && Selection.gameObjects.Length == 1;

            if (!editing) {
                _path.selected.Clear();
                _profile.selected.Clear();
                Tools.hidden = false;
                SetSuspended(trim, false);
                return;
            }

            if (anySelected)
                SetSuspended(trim, true);

            Matrix4x4 local = trim.transform.localToWorldMatrix;
            Matrix4x4 profileSpace = local * trim.ProfileFrame();
            List<Vector2> profile = trim.activeProfile;

            bool DoProfile() {
                if (profile == null)
                    return false;

                using (new Handles.DrawingScope(Color.white, profileSpace))
                    return ProfileHandles(trim, profile);
            }

            bool DoPath() {
                using (new Handles.DrawingScope(Color.white, local))
                    return PathHandles(trim);
            }

            bool preferProfile = _profile.selected.Count > 0;
            if (preferProfile ? DoPath() || DoProfile() : DoProfile() || DoPath())
                return;

            if (SceneRectSelection.Handle(RectSelectHash, ref _rectStart, ref _rectEnd, out Rect rect)) {
                bool pathHit;
                using (new Handles.DrawingScope(Color.white, local))
                    pathHit = _path.SelectInRect(rect, trim.activePath);

                if (!pathHit && profile != null) {
                    using (new Handles.DrawingScope(Color.white, profileSpace))
                        _profile.SelectInRect(rect, profile);
                }
            }

            Tools.hidden = anySelected;
            SetSuspended(trim, Tools.hidden);
        }

        private bool ProfileHandles(ProBuilderTrim trim, List<Vector2> profile) {
            PointEdit edit = _profile.Do(profile, trim.activeProfileRadii);
            if (edit == null)
                return false;

            ProBuilderTrimStats stats = trim.stats;
            if (stats) {
                Undo.RecordObject(stats, $"{edit.undoName} (Trim Profile)");
                stats.profile = edit.points;
                stats.profileRadii = edit.radii;
                EditorUtility.SetDirty(stats);
            }
            else {
                Undo.RecordObject(trim, $"{edit.undoName} (Trim Profile)");
                trim.profile = edit.points;
                trim.profileRadii = edit.radii;
            }

            SetSuspended(trim, anySelected);
            if (stats)
                RebuildQueue.Run(stats, stats.NotifyChanged);
            RebuildQueue.Run(trim, trim.Rebuild);
            return true;
        }

        private bool PathHandles(ProBuilderTrim trim) {
            PathEdit edit = _path.Do(trim.activePath, trim.activePathRadii, trim.activeClosed, trim.transform.rotation);
            if (edit == null)
                return false;

            Undo.RecordObject(trim.pathInStats ? trim.stats : trim, $"{edit.undoName} (Trim)");
            trim.activePath = edit.points;
            trim.activePathRadii = edit.radii;

            ProBuilderTrimStats stats = trim.pathInStats ? trim.stats : null;
            if (stats)
                EditorUtility.SetDirty(stats);

            SetSuspended(trim, anySelected);
            if (stats)
                RebuildQueue.Run(stats, stats.NotifyChanged);
            RebuildQueue.Run(trim, trim.Rebuild);
            return true;
        }
    }
}
