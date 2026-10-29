using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Serialization;

using Tsitski.Core;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    [AddComponentMenu("tsitski ProBuilder Tools/ProBuilder Array")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProBuilderMesh))]
    public class ProBuilderArray : MonoBehaviour, IPathOwner, IGenerated {
        public const float MinSpacing = 0.01f;
        private const int MaxInstances = 10000;

        [Tooltip(Tooltips.ArrayStats)] public ProBuilderArrayStats stats;
        [Tooltip(Tooltips.ArraySource)] public ProBuilderMesh source;
        [Tooltip(Tooltips.ArrayPillarStats)] public ProBuilderPillarStats pillarStats;
        [Tooltip(Tooltips.ArrayPillarHeight)] [Min(0.01f)] public float pillarHeight = 3f;
        [Tooltip(Tooltips.MergeIntoMesh)] public bool mergeIntoMesh;
        [Tooltip(Tooltips.ArrayCornerSide)] public ProBuilderPillar.CornerSide cornerSide;
        [Tooltip(Tooltips.Spacing)] [Min(MinSpacing)] public float spacing = 1f;
        [Tooltip(Tooltips.Offset)] [Min(0f)] public float offset;
        [Tooltip(Tooltips.FitSpacing)] [FormerlySerializedAs("spacePerSegment")] [FormerlySerializedAs("fitToPath")] public bool fitSpacing;
        [Tooltip(Tooltips.SplitAtCorners)] public bool splitAtCorners = true;
        [Tooltip(Tooltips.DivisionLength)] [Min(0f)] public float divisionLength;
        [Tooltip(Tooltips.SkipCorners)] [FormerlySerializedAs("avoidCorners")] public bool skipCorners;
        [Tooltip(Tooltips.SkipDivisions)] public bool skipDivisions = true;
        [Tooltip(Tooltips.AlignToPath)] public bool alignToPath = true;
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.ArrayPath)] public List<Vector3> path = new() { Vector3.zero };
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();
        [Tooltip(Tooltips.Closed)] public bool closed;

        [SerializeField] [HideInInspector] private List<ProBuilderPillar> generatedPillars = new();
        [SerializeField] [HideInInspector] internal MonoBehaviour generator;
        MonoBehaviour IGenerated.generator { get => generator; set => generator = value; }

        internal static bool suppressReset;

        public static readonly HashSet<ProBuilderArray> All = new();
        public static event Action<ProBuilderMesh> Rebuilt;

#if UNITY_EDITOR
#endif

        public ProBuilderMesh activeSource => stats && stats.source ? stats.source : source;
        public bool sourceInStats => stats && stats.storeSource && stats.sourceMesh != null && stats.sourceMesh.hasData;
        public ProBuilderPillarStats activePillarStats => stats && stats.pillarStats ? stats.pillarStats : pillarStats;
        public float activePillarHeight => Mathf.Max(0.01f, stats ? stats.pillarHeight : pillarHeight);
        public bool activeMergeIntoMesh => stats ? stats.mergeIntoMesh : mergeIntoMesh;
        public float activeSpacing => Mathf.Max(MinSpacing, stats ? stats.spacing : spacing);
        public float activeOffset => Mathf.Max(0f, stats ? stats.offset : offset);
        public ArraySpacing activeSpacingSettings => stats
            ? new ArraySpacing {
                spacing = activeSpacing, offset = activeOffset, fitSpacing = stats.fitSpacing, splitAtCorners = stats.splitAtCorners,
                divisionLength = stats.divisionLength, skipCorners = stats.skipCorners, skipDivisions = stats.skipDivisions
            }
            : new ArraySpacing {
                spacing = activeSpacing, offset = activeOffset, fitSpacing = fitSpacing, splitAtCorners = splitAtCorners,
                divisionLength = divisionLength, skipCorners = skipCorners, skipDivisions = skipDivisions
            };
        public bool activeAlignToPath => stats ? stats.alignToPath : alignToPath;
        public int activeRoundingSegments => Mathf.Max(1, stats ? stats.roundingSegments : roundingSegments);
        public bool pathInStats => stats && stats.storePath;
        public bool activeClosed => pathInStats ? stats.closed : closed;

        List<Vector3> IPathOwner.localPath { get => path; set => path = value; }

        public List<Vector3> activePath {
            get => pathInStats ? stats.path : path;
            set {
                if (pathInStats)
                    stats.path = value;
                else
                    path = value;
            }
        }

        public List<float> activePathRadii {
            get => pathInStats ? stats.pathRadii : pathRadii;
            set {
                if (pathInStats)
                    stats.pathRadii = value;
                else
                    pathRadii = value;
            }
        }

        public static bool IsSource(ProBuilderMesh mesh) => mesh && All.Any(a => a && a.activeSource == mesh);

        private void Reset() {
            TsitskiProBuilderSettings.ApplyDefaultStaticFlags(gameObject);
            if (source || suppressReset)
                return;

            if (TryGetComponent(out ProBuilderMesh mesh) && mesh.faceCount > 0)
                CreateSourceFrom(mesh);
            else
                CreateSource();
        }

        public void CreateSource() {
            ProBuilderMesh cube = ShapeGenerator.GenerateCube(PivotLocation.Center, Vector3.one * 0.25f);
            if (cube.TryGetComponent(out MeshRenderer cubeRenderer))
                cubeRenderer.sharedMaterial = TsitskiProBuilderSettings.DefaultMaterial;
            AdoptSource(cube);
        }

        public void CreateSourceFrom(ProBuilderMesh original) {
            GameObject child = new("Array Source");
            ProBuilderMesh copy = child.AddComponent<ProBuilderMesh>();
            copy.Clear();
            copy.ToMesh();
            copy.CopyFrom(original);
            copy.ToMesh();
            copy.Refresh();

            if (original.TryGetComponent(out MeshRenderer originalRenderer) && child.TryGetComponent(out MeshRenderer copyRenderer))
                copyRenderer.sharedMaterials = originalRenderer.sharedMaterials;

            AdoptSource(copy);
        }

        public void CreateSourceFrom(ArraySourceData data) {
            ProBuilderMesh mesh = new GameObject("Array Source").AddComponent<ProBuilderMesh>();
            mesh.Clear();
            mesh.ToMesh();
            data.ApplyTo(mesh);
            AdoptSource(mesh);
        }

        private void AdoptSource(ProBuilderMesh mesh) {
            mesh.gameObject.name = "Array Source";

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Create Array Source");
            Undo.SetTransformParent(mesh.transform, transform, false, "Create Array Source");
#else
            mesh.transform.SetParent(transform, false);
#endif

            mesh.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            mesh.transform.localScale = Vector3.one;
            source = mesh;
        }

        private void OnEnable() {
            if (TryGetComponent(out ProBuilderMesh mesh) && mesh.faceCount == 0)
                mesh.Clear();

            All.Add(this);
            ProBuilderArrayStats.Changed += OnStatsChanged;
            ProBuilderPillarStats.Changed += OnPillarStatsChanged;
            QueueRebuild();
        }

        private void OnDisable() {
            All.Remove(this);
            ProBuilderArrayStats.Changed -= OnStatsChanged;
            ProBuilderPillarStats.Changed -= OnPillarStatsChanged;
        }

        private void OnPillarStatsChanged(ProBuilderPillarStats changed) {
            if (changed && changed == activePillarStats)
                QueueRebuild();
        }

        private void OnValidate() => QueueRebuild();

        private void OnStatsChanged(ProBuilderArrayStats changed) {
            if (changed == stats)
                QueueRebuild();
        }

        public void QueueRebuild() => RebuildQueue.Queue(this, Rebuild);

        public Matrix4x4 PathStartFrame() =>
            PathUtility.StartFrame(PathUtility.Rounded(activePath, activePathRadii, activeClosed, activeRoundingSegments, out bool loop), loop);

        public void Rebuild() {
            TsitskiProBuilderSettings.AutoName(this, stats, "ProBuilder Array");

            if (!TryGetComponent(out ProBuilderMesh mesh))
                return;

            if (activePillarStats) {
                if (activeMergeIntoMesh)
                    RebuildMergedPillars(mesh);
                else
                    RebuildPillars(mesh);
                return;
            }

            GeneratedChildren.Resize(this, generatedPillars, 0, "Array Pillar");

            ArraySourceData data = SourceData(mesh);
            if (data is not { hasData: true })
                return;

            List<Vector3> positions = new();
            List<Vector2> uvs = new();
            List<Face> faces = new();

            foreach (Matrix4x4 placement in Placements())
                data.AppendTo(placement, positions, uvs, faces);

            if (faces.Count == 0) {
                mesh.Clear();
            }
            else {
                mesh.RebuildWithPositionsAndFaces(positions, faces);
                mesh.textures = uvs;
            }
            mesh.ToMesh();
            mesh.Refresh();

            if (TryGetComponent(out MeshRenderer meshRenderer)) {
                Material[] materials = data.materials.Where(m => m).ToArray();
                meshRenderer.sharedMaterials = materials is { Length: > 0 } ? materials : new[] { TsitskiProBuilderSettings.DefaultMaterial };
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(mesh);
#endif
            Rebuilt?.Invoke(mesh);
        }

        private static Quaternion PlacementRotation(Matrix4x4 placement) {
            Vector3 forward = placement.GetColumn(2), up = placement.GetColumn(1);
            return forward.sqrMagnitude > 1e-8f && up.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(forward, up) : Quaternion.identity;
        }

        private void RebuildMergedPillars(ProBuilderMesh mesh) {
            GeneratedChildren.Resize(this, generatedPillars, 0, "Array Pillar");

            List<Vector3> positions = new();
            List<Face> faces = new();

            GameObject temporary = new("Array Pillar") { hideFlags = HideFlags.HideAndDontSave };
            temporary.SetActive(false);
            try {
                ProBuilderPillar pillar = temporary.AddComponent<ProBuilderPillar>();
                Vector3 defaultStart = pillar.cornerStart, defaultEnd = pillar.cornerEnd;
                pillar.stats = activePillarStats;
                pillar.height = activePillarHeight;
                pillar.cornerSide = cornerSide;

                List<Vector3> piecePositions = new();
                List<Face> pieceFaces = new();
                foreach ((Matrix4x4 placement, PlacementKind kind) in PlacementSlots()) {
                    temporary.transform.SetPositionAndRotation(transform.TransformPoint(placement.GetColumn(3)),
                        transform.rotation * PlacementRotation(placement));
                    pillar.cornerStart = defaultStart;
                    pillar.cornerEnd = defaultEnd;
                    if (pillar.activeAutoSnapToPath)
                        pillar.SnapToPath(pillar.snapDistance, transform, activePath, activeClosed, kind == PlacementKind.Corner);

                    piecePositions.Clear();
                    pieceFaces.Clear();
                    pillar.BuildGeometry(piecePositions, pieceFaces);

                    Matrix4x4 toArray = Matrix4x4.TRS(transform.InverseTransformPoint(temporary.transform.position),
                        Quaternion.Inverse(transform.rotation) * temporary.transform.rotation, Vector3.one);
                    int start = positions.Count;
                    foreach (Vector3 p in piecePositions)
                        positions.Add(toArray.MultiplyPoint3x4(p));
                    foreach (Face face in pieceFaces)
                        faces.Add(new Face(face.indexes.Select(i => i + start).ToArray()) {
                            smoothingGroup = face.smoothingGroup,
                            submeshIndex = face.submeshIndex,
                            manualUV = face.manualUV,
                            uv = face.uv
                        });
                }
            }
            finally {
                DestroyImmediate(temporary);
            }

            if (faces.Count == 0)
                mesh.Clear();
            else
                mesh.RebuildWithPositionsAndFaces(positions, faces);
            mesh.ToMesh();
            mesh.Refresh();

            if (TryGetComponent(out MeshRenderer meshRenderer) && !meshRenderer.sharedMaterial)
                meshRenderer.sharedMaterial = TsitskiProBuilderSettings.DefaultMaterial;

#if UNITY_EDITOR
            EditorUtility.SetDirty(mesh);
#endif
            Rebuilt?.Invoke(mesh);
        }

        private void RebuildPillars(ProBuilderMesh mesh) {
            if (mesh.faceCount > 0) {
                mesh.Clear();
                mesh.ToMesh();
                mesh.Refresh();
            }

            List<(Matrix4x4 placement, PlacementKind kind)> placements = PlacementSlots();
            GeneratedChildren.Resize(this, generatedPillars, placements.Count, "Array Pillar");

            ProBuilderPillarStats pillarStats = activePillarStats;
            float pillarHeight = activePillarHeight;
            for (int i = 0; i < placements.Count; i++) {
                (Matrix4x4 placement, PlacementKind kind) = placements[i];
                Quaternion rotation = PlacementRotation(placement);

                ProBuilderPillar pillar = generatedPillars[i];
                GeneratedChildren.Place(this, pillar, placement.GetColumn(3), rotation);
                pillar.stats = pillarStats;
                pillar.height = pillarHeight;
                pillar.cornerSide = cornerSide;
                if (pillar.activeAutoSnapToPath)
                    pillar.SnapToPath(pillar.snapDistance, transform, activePath, activeClosed, kind == PlacementKind.Corner);
                GeneratedChildren.Finish(pillar);
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(mesh);
#endif
            Rebuilt?.Invoke(mesh);
        }

        private ArraySourceData SourceData(ProBuilderMesh self) {
            if (sourceInStats)
                return stats.sourceMesh;

            ProBuilderMesh template = activeSource;
            return template && template != self && template.faceCount > 0 ? ArraySourceData.Capture(template) : null;
        }

        public List<Matrix4x4> Placements() => PlacementSlots().Select(slot => slot.placement).ToList();

        private List<(Matrix4x4 placement, PlacementKind kind)> PlacementSlots() {
            List<(Matrix4x4 placement, PlacementKind kind)> placements = new();
            List<Vector3> points = PathUtility.Rounded(activePath, activePathRadii, activeClosed, activeRoundingSegments, out bool loop);

            if (points.Count == 1) {
                placements.Add((Matrix4x4.Translate(points[0]), PlacementKind.Interior));
                return placements;
            }

            if (points.Count < 2)
                return placements;

            PathUtility.Segments(points, loop, out Vector3[] directions, out float[] lengths, out Vector3[] rights, out Vector3[] ups);
            float[] cumulative = new float[lengths.Length + 1];
            for (int s = 0; s < lengths.Length; s++)
                cumulative[s + 1] = cumulative[s] + lengths[s];

            float total = cumulative[^1];
            if (total <= 0f)
                return placements;

            bool align = activeAlignToPath;
            foreach (ArraySpacing.Placement slot in activeSpacingSettings.Layout(points, loop, MaxInstances)) {
                if (slot.skipped)
                    continue;

                float distance = slot.distance;
                float d = loop ? Mathf.Repeat(distance, total) : Mathf.Clamp(distance, 0f, total);
                int index = Array.BinarySearch(cumulative, d);
                int segment = Mathf.Clamp(index >= 0 ? index : ~index - 1, 0, lengths.Length - 1);

                Vector3 position = points[segment] + directions[segment] * Mathf.Clamp(d - cumulative[segment], 0f, lengths[segment]);
                Matrix4x4 placement = Matrix4x4.identity;
                if (align) {
                    placement.SetColumn(0, rights[segment]);
                    placement.SetColumn(1, ups[segment]);
                    placement.SetColumn(2, directions[segment]);
                }
                placement.SetColumn(3, new Vector4(position.x, position.y, position.z, 1f));
                placements.Add((placement, slot.kind));
            }

            return placements;
        }

    }
}
