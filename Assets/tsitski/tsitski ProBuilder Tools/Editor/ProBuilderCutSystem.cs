using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ProBuilder;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    public static class ProBuilderCutSystem {
        private const double RebuildInterval = 0.05;
        private const double LightmapUvDelay = 1.0;
        private const double ColliderRepairInterval = 0.5;

        private static readonly HashSet<ProBuilderCutter> Cutters = new();
        private static readonly HashSet<ProBuilderCutter> DirtyCutters = new();
        private static readonly HashSet<MeshFilter> DirtyMeshes = new();
        private static readonly List<ProBuilderCutTarget> Targets = new();
        private static readonly HashSet<ProBuilderMesh> SuspendedMeshes = new();
        private static readonly HashSet<ProBuilderCutTarget> PendingLightmapUvs = new();
        private static readonly Dictionary<ProBuilderCutter, Bounds> CutterBounds = new();
        private static readonly Dictionary<ProBuilderCutTarget, Mesh> SourceMeshes = new();
        private static readonly Dictionary<MeshFilter, Bounds> MeshBounds = new();

        private static double _nextRebuildTime;
        private static double _lastRebuildTime;
        private static double _nextColliderRepairTime;
        private static bool _needsRefresh = true;
        private static bool _resetChanges = true;
        private static bool _rebuildAll;
        private static bool _silentRebuild;
        private static bool _forceRebuild;

        static ProBuilderCutSystem() {
            ProBuilderCutter.Changed += OnCutterChanged;
            EditorMeshUtility.meshOptimized += (mesh, _) => MarkDirty(mesh);
            ProBuilderEditor.afterMeshModification += meshes => {
                foreach (ProBuilderMesh mesh in meshes)
                    MarkDirty(mesh);
            };
            ProBuilderTrim.Rebuilt += MarkDirty;
            ProBuilderArray.Rebuilt += MarkDirty;
            ProBuilderFloorPlan.Rebuilt += MarkDirty;
            ProBuilderPolygon.Rebuilt += MarkDirty;
            ProBuilderStairs.Rebuilt += MarkDirty;
            ProBuilderPillar.Rebuilt += MarkDirty;
            ProBuilderRoof.Rebuilt += MarkDirty;
            Selection.selectionChanged += UpdateEditing;
            ToolManager.activeContextChanged += UpdateEditing;
            EditorSceneManager.sceneOpened += (_, _) => _needsRefresh = _resetChanges = _rebuildAll = true;
            EditorSceneManager.sceneClosed += _ => _needsRefresh = true;
            EditorApplication.hierarchyChanged += () => _needsRefresh = true;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingEditMode)
                    GeneratePendingLightmapUvs();
                if (state == PlayModeStateChange.EnteredEditMode)
                    _needsRefresh = _resetChanges = true;
            };
            EditorApplication.update += Update;
            EditorSceneManager.sceneSaving += (_, _) => GeneratePendingLightmapUvs();
            RebuildQueue.Released += () => _nextRebuildTime = 0;

            if (!SessionState.GetBool("ProBuilderCutSystem.Started", false)) {
                SessionState.SetBool("ProBuilderCutSystem.Started", true);
                _rebuildAll = true;
            }
        }

        internal static void MarkDirty(ProBuilderMesh mesh) {
            if (mesh && mesh.TryGetComponent(out MeshFilter filter))
                DirtyMeshes.Add(filter);
        }

        private static bool IsSuspended(MeshFilter mesh) =>
            mesh.TryGetComponent(out ProBuilderMesh proBuilderMesh) && SuspendedMeshes.Contains(proBuilderMesh);

        private static List<MeshFilter> CandidateMeshes() {
            List<MeshFilter> meshes = new();
            foreach (ProBuilderMesh mesh in UnityCompat.FindAll<ProBuilderMesh>())
                if (mesh.TryGetComponent(out MeshFilter filter))
                    meshes.Add(filter);

            if (!Cutters.Any(cutter => cutter && cutter.cutsMeshes))
                return meshes;

            foreach (MeshFilter filter in UnityCompat.FindAll<MeshFilter>())
                if (IsRegularMesh(filter))
                    meshes.Add(filter);

            return meshes;
        }

        private static bool IsRegularMesh(MeshFilter filter) =>
            filter && !filter.TryGetComponent(out ProBuilderMesh _) && filter.TryGetComponent(out MeshRenderer _) &&
            filter.sharedMesh && (filter.sharedMesh.hideFlags & HideFlags.DontSaveInEditor) == 0 &&
            !ProBuilderCutTarget.IsResult(filter);

        [MenuItem("Tools/tsitski ProBuilder Tools/Generate/Rebuild All Cutters")]
        private static void ForceRebuildAll() {
            RebuildAll();
            _forceRebuild = true;
        }

        private static void RebuildAll() {
            Refresh();
            DirtyMeshes.UnionWith(CandidateMeshes());
            _nextRebuildTime = 0;
        }

        [MenuItem("Tools/tsitski ProBuilder Tools/Generate/Regenerate Cut Lightmap UVs")]
        private static void RegenerateLightmapUvs() {
            foreach (ProBuilderCutTarget target in UnityCompat.FindAll<ProBuilderCutTarget>()) {
                if (!target.resultTransform ||
                    !GameObjectUtility.AreStaticEditorFlagsSet(target.gameObject, StaticEditorFlags.ContributeGI))
                    continue;

                target.GenerateLightmapUvs();
                PendingLightmapUvs.Remove(target);
                if (target.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            }
        }

        private static void OnCutterChanged(ProBuilderCutter cutter) {
            DirtyCutters.Add(cutter);
            _needsRefresh = true;
        }

        private static void Refresh() {
            _needsRefresh = false;

            Cutters.Clear();
            foreach (ProBuilderCutter cutter in UnityCompat.FindAll<ProBuilderCutter>())
                Cutters.Add(cutter);

            Targets.Clear();
            Targets.AddRange(UnityCompat.FindAll<ProBuilderCutTarget>());
            foreach (ProBuilderCutTarget target in SourceMeshes.Keys.Where(t => !t).ToList())
                SourceMeshes.Remove(target);
            SuspendedMeshes.RemoveWhere(m => !m);
        }

        private static void Update() {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (_needsRefresh)
                Refresh();

            if (_resetChanges) {
                _resetChanges = false;
                DirtyCutters.Clear();
                DirtyMeshes.Clear();
                foreach (ProBuilderCutter cutter in Cutters)
                    cutter.transform.hasChanged = false;
                foreach (ProBuilderCutTarget target in Targets)
                    target.transform.hasChanged = false;
                UpdateEditing();
            }

            if (_rebuildAll) {
                _rebuildAll = false;
                _silentRebuild = true;
                RebuildAll();
            }

            foreach (ProBuilderCutter cutter in Cutters) {
                if (!cutter || !cutter.transform.hasChanged)
                    continue;

                cutter.transform.hasChanged = false;
                DirtyCutters.Add(cutter);
            }

            bool repairColliders = EditorApplication.timeSinceStartup >= _nextColliderRepairTime;
            if (repairColliders)
                _nextColliderRepairTime = EditorApplication.timeSinceStartup + ColliderRepairInterval;

            bool dragging = DragAndDrop.objectReferences.Length > 0;
            foreach (ProBuilderCutTarget target in Targets) {
                if (repairColliders && target && target.RepairCollider() && target.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(target.gameObject.scene);

                if (target && target.TryGetComponent(out MeshFilter source)) {
                    if (SourceMeshes.TryGetValue(target, out Mesh previous) && previous != source.sharedMesh)
                        DirtyMeshes.Add(source);
                    SourceMeshes[target] = source.sharedMesh;

                    if (!dragging && !IsSuspended(source) && target.SyncMaterials())
                        DirtyMeshes.Add(source);
                }

                if (!target || !target.transform.hasChanged)
                    continue;

                target.transform.hasChanged = false;
                if (target.TryGetComponent(out MeshFilter mesh))
                    DirtyMeshes.Add(mesh);
            }

            if (DirtyCutters.Count == 0 && DirtyMeshes.Count == 0) {
                _silentRebuild = false;
                if (!RebuildQueue.HandleHeld && EditorApplication.timeSinceStartup - _lastRebuildTime > LightmapUvDelay)
                    GeneratePendingLightmapUvs();
                return;
            }

            if (DirtyCutters.Count > 0 && RebuildQueue.Deferring)
                return;

            if (EditorApplication.timeSinceStartup < _nextRebuildTime)
                return;

            _nextRebuildTime = EditorApplication.timeSinceStartup + RebuildInterval;
            Rebuild();
            _lastRebuildTime = EditorApplication.timeSinceStartup;
            _silentRebuild = false;
        }

        private static void GeneratePendingLightmapUvs() {
            if (PendingLightmapUvs.Count == 0)
                return;

            foreach (ProBuilderCutTarget target in PendingLightmapUvs)
                if (target)
                    target.GenerateLightmapUvs();
            PendingLightmapUvs.Clear();
        }

        private static void Rebuild() {
            List<MeshFilter> allMeshes = CandidateMeshes();
            CutterBounds.Clear();
            MeshBounds.Clear();

            Dictionary<ProBuilderCutter, List<MeshFilter>> cuts = new();
            foreach (ProBuilderCutter cutter in Cutters) {
                if (!IsActive(cutter))
                    continue;

                List<MeshFilter> meshes = new();
                foreach (MeshFilter mesh in allMeshes)
                    if (Cuts(cutter, mesh))
                        meshes.Add(mesh);
                if (meshes.Count > 0)
                    cuts[cutter] = meshes;
            }

            HashSet<MeshFilter> affected = new(DirtyMeshes.Where(m => m));

            foreach (ProBuilderCutTarget target in Targets) {
                if (!target)
                    continue;

                if (target.appliedCutters.Any(c => !c || DirtyCutters.Contains(c)) && target.TryGetComponent(out MeshFilter mesh))
                    affected.Add(mesh);
            }

            foreach (ProBuilderCutter cutter in DirtyCutters)
                if (cutter && cuts.TryGetValue(cutter, out List<MeshFilter> meshes))
                    affected.UnionWith(meshes);

            DirtyCutters.Clear();
            DirtyMeshes.Clear();
            bool force = _forceRebuild;
            _forceRebuild = false;

            List<ProBuilderCutter> involved = cuts.Where(pair => pair.Value.Any(affected.Contains)).Select(pair => pair.Key).ToList();
            foreach (ProBuilderCutter cutter in involved)
                affected.UnionWith(cuts[cutter]);

            Dictionary<MeshFilter, List<ProBuilderCutter>> cuttersByMesh = new();
            foreach ((ProBuilderCutter cutter, List<MeshFilter> meshes) in cuts)
                foreach (MeshFilter mesh in meshes) {
                    if (!cuttersByMesh.TryGetValue(mesh, out List<ProBuilderCutter> list))
                        cuttersByMesh[mesh] = list = new List<ProBuilderCutter>();
                    list.Add(cutter);
                }

            Dictionary<ProBuilderCutter, MeshFilter> owners = new();
            Dictionary<ProBuilderCutter, TunnelCap[]> tunnels = new();
            foreach ((ProBuilderCutter cutter, List<MeshFilter> candidates) in cuts) {
                if (!candidates.Any(affected.Contains))
                    continue;

                owners[cutter] = InsideOwner(cutter, candidates);
                if (cutter.clipsToTunnel)
                    tunnels[cutter] = TunnelCaps(cutter, candidates);
            }

            foreach (MeshFilter mesh in affected.Where(m => !IsSuspended(m))) {
                List<ProBuilderCutter> cutters = cuttersByMesh.TryGetValue(mesh, out List<ProBuilderCutter> list) ? list : new List<ProBuilderCutter>();
                RebuildMesh(mesh, cutters, owners, tunnels, force);
            }
        }

        private static MeshFilter InsideOwner(ProBuilderCutter cutter, List<MeshFilter> candidates) {
            if (candidates.Count <= 1)
                return candidates.FirstOrDefault();

            List<(MeshFilter mesh, float area)> ranked = candidates
                .Select(m => (mesh: m, area: Mathf.Round(ProBuilderCutTarget.PenetrationArea(
                    m.sharedMesh, m.transform.localToWorldMatrix, cutter) * 1000f)))
                .ToList();
            float best = ranked.Max(c => c.area);
            List<MeshFilter> tied = ranked.Where(c => c.area == best).Select(c => c.mesh).ToList();
            return tied.Count == 1
                ? tied[0]
                : tied.OrderBy(m => GlobalObjectId.GetGlobalObjectIdSlow(m).ToString()).First();
        }

        private static TunnelCap[] TunnelCaps(ProBuilderCutter cutter, List<MeshFilter> candidates) {
            TunnelCap[] caps = new TunnelCap[6];

            foreach (MeshFilter mesh in candidates)
                ProBuilderCutTarget.TunnelCaps(mesh.sharedMesh,
                    mesh.transform.localToWorldMatrix, cutter, caps);

            return caps;
        }

        private static string HierarchyKey(Component component) {
            List<string> parts = new();
            for (Transform t = component.transform; t; t = t.parent)
                parts.Add(t.GetSiblingIndex().ToString("D6"));

            parts.Reverse();
            int componentIndex = Array.IndexOf(component.GetComponents<ProBuilderCutter>(), component);
            return $"{component.gameObject.scene.path}/{string.Join("/", parts)}#{componentIndex}";
        }

        private static void RebuildMesh(MeshFilter mesh, List<ProBuilderCutter> cutters,
            Dictionary<ProBuilderCutter, MeshFilter> owners, Dictionary<ProBuilderCutter, TunnelCap[]> tunnels, bool force) {
            cutters = cutters.OrderBy(HierarchyKey, StringComparer.Ordinal).ToList();
            bool changed = true;
            HashSet<ProBuilderCutter> insideCutters = new(cutters.Where(c => !owners.TryGetValue(c, out MeshFilter owner) || owner == mesh));
            bool hasTarget = mesh.TryGetComponent(out ProBuilderCutTarget target);

            if (cutters.Count == 0 && !hasTarget)
                return;

            try {
                if (cutters.Count == 0) {
                    target.Remove();
                    _needsRefresh = true;
                }
                else {
                    if (!hasTarget) {
                        target = mesh.gameObject.AddComponent<ProBuilderCutTarget>();
                        target.transform.hasChanged = false;
                        _needsRefresh = true;
                    }

                    if (target.Rebuild(cutters, insideCutters, tunnels, out bool needsLightmapUvs, _silentRebuild, force)) {
                        target.SetEditing(IsEditing(target));
                        if (target.lastRebuildSkipped)
                            return;

                        if (needsLightmapUvs)
                            PendingLightmapUvs.Add(target);
                        else
                            PendingLightmapUvs.Remove(target);
                    }
                    else {
                        changed = hasTarget;
                        target.Remove();
                        _needsRefresh = true;
                    }
                }
            }
            catch (Exception e) {
                Debug.LogException(e, mesh);
            }

            if (changed && mesh.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(mesh.gameObject.scene);
        }

        private static bool IsActive(ProBuilderCutter cutter) =>
            cutter && cutter.isActiveAndEnabled && cutter.size.x > 0f && cutter.size.y > 0f && cutter.size.z > 0f;

        private static bool Cuts(ProBuilderCutter cutter, MeshFilter mesh) {
            if (!mesh || mesh.gameObject.scene != cutter.gameObject.scene || !mesh.gameObject.activeInHierarchy)
                return false;

            bool isProBuilder = mesh.TryGetComponent(out ProBuilderMesh proBuilderMesh);
            if (isProBuilder ? !cutter.cutsProBuilder || !proBuilderMesh.enabled : !cutter.cutsMeshes)
                return false;

            if (!MeshBounds.TryGetValue(mesh, out Bounds meshBounds)) {
                meshBounds = mesh.sharedMesh
                    ? WorldBounds(mesh.sharedMesh.bounds, mesh.transform.localToWorldMatrix)
                    : new Bounds(Vector3.positiveInfinity, Vector3.zero);
                MeshBounds[mesh] = meshBounds;
            }

            if (!CutterBounds.TryGetValue(cutter, out Bounds cutterBounds))
                CutterBounds[cutter] = cutterBounds = cutter.worldBounds;

            if (float.IsInfinity(meshBounds.center.x) || !cutterBounds.Intersects(meshBounds))
                return false;

            if ((isProBuilder && ProBuilderArray.IsSource(proBuilderMesh)) || ProBuilderCutTarget.IsResult(mesh))
                return false;

            if (mesh.TryGetComponent(out ProBuilderBakedCuts baked) && baked.cutters.Contains(cutter))
                return false;

            if (cutter.ignorePrefabMeshes && PrefabUtility.GetOutermostPrefabInstanceRoot(mesh.gameObject))
                return false;

            GameObject ignoreRoot = PrefabUtility.GetNearestPrefabInstanceRoot(cutter.gameObject);
            if (mesh.transform.IsChildOf(ignoreRoot ? ignoreRoot.transform : cutter.transform))
                return false;

            if (!cutter.cutThroughParent && cutter.transform.IsChildOf(mesh.transform))
                return false;

            if (!cutter.cutThroughParent && cutter.transform.parent) {
                MeshFilter parentMesh = cutter.transform.parent.GetComponentInParent<MeshFilter>(true);
                if (parentMesh && mesh.transform.IsChildOf(parentMesh.transform))
                    return false;
            }

            return true;
        }

        private static Bounds WorldBounds(Bounds local, Matrix4x4 matrix) {
            Bounds bounds = new(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f))));
            return bounds;
        }

        public static void SetSuspended(ProBuilderMesh mesh, bool suspended) {
            if (!mesh || (suspended ? !SuspendedMeshes.Add(mesh) : !SuspendedMeshes.Remove(mesh)))
                return;

            if (!suspended) {
                MarkDirty(mesh);
                _nextRebuildTime = 0;
            }

            if (mesh.TryGetComponent(out ProBuilderCutTarget target))
                target.SetEditing(IsEditing(target));
        }

        private static bool IsEditing(ProBuilderCutTarget target) =>
            (target.TryGetComponent(out MeshFilter mesh) && IsSuspended(mesh)) ||
            ToolManager.activeContextType != typeof(GameObjectToolContext) &&
            Selection.gameObjects.Contains(target.gameObject);

        private static void UpdateEditing() {
            foreach (ProBuilderCutTarget target in Targets)
                if (target)
                    target.SetEditing(IsEditing(target));
        }
    }
}
