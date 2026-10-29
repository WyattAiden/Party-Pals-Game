using System;
using System.Collections.Generic;
using System.Linq;
using Tsitski.Core;
using UnityEngine;
using UnityEngine.ProBuilder;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    [AddComponentMenu("tsitski ProBuilder Tools/ProBuilder Roof")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProBuilderMesh))]
    public class ProBuilderRoof : MonoBehaviour {
        private const int NoSmoothing = 0;

        [Tooltip(Tooltips.RoofStats)] public ProBuilderRoofStats stats;
        [Tooltip(Tooltips.RoofProfile)] public List<Vector2> profile = DefaultProfile();
        [Tooltip(Tooltips.Radii)] public List<float> profileRadii = new();
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.RoofThickness)] [Min(0.01f)] public float thickness = 0.25f;
        [Tooltip(Tooltips.RoofPaths)] public List<RoofPath> paths = DefaultPaths();

        [NonSerialized] public List<List<Vector2>> footprint = new();
        [NonSerialized] public int failedRegions;
        [NonSerialized] public List<int> separatedPaths = new();

        public static readonly HashSet<ProBuilderRoof> All = new();
        public static event Action<ProBuilderMesh> Rebuilt;

        public static List<Vector2> DefaultProfile() => new() { Vector2.zero, Vector2.one };

        public static List<RoofPath> DefaultPaths() => new() {
            new RoofPath { width = 4f, height = 1.5f, path = new List<Vector3> { Vector3.zero, new(0f, 0f, 8f) } }
        };

        public bool pathsInStats => stats && stats.savePaths;
        public UnityEngine.Object profileOwner => stats ? stats : this;
        public UnityEngine.Object pathsOwner => pathsInStats ? stats : this;

        public List<Vector2> activeProfile {
            get => stats ? stats.profile : profile;
            set {
                if (stats)
                    stats.profile = value;
                else
                    profile = value;
            }
        }

        public List<float> activeProfileRadii {
            get => stats ? stats.profileRadii : profileRadii;
            set {
                if (stats)
                    stats.profileRadii = value;
                else
                    profileRadii = value;
            }
        }

        public int activeRoundingSegments => Mathf.Max(1, stats ? stats.roundingSegments : roundingSegments);
        public float activeThickness => Mathf.Max(0.01f, stats ? stats.thickness : thickness);

        public List<RoofPath> activePaths {
            get => (pathsInStats ? stats.paths : paths) ?? new List<RoofPath>();
            set {
                if (pathsInStats)
                    stats.paths = value;
                else
                    paths = value;
            }
        }

        public float baseHeight {
            get {
                List<Vector3> points = activePaths.Where(p => p?.path != null).SelectMany(p => p.path).ToList();
                return points.Count > 0 ? points.Average(p => p.y) : 0f;
            }
        }

        private void Reset() {
            TsitskiProBuilderSettings.ApplyDefaultStaticFlags(gameObject);
            if (TryGetComponent(out MeshCollider _))
                return;

#if UNITY_EDITOR
            Undo.AddComponent<MeshCollider>(gameObject);
#else
            gameObject.AddComponent<MeshCollider>();
#endif
        }

        private void OnEnable() {
            All.Add(this);
            ProBuilderRoofStats.Changed += OnStatsChanged;
            QueueRebuild();
        }

        private void OnDisable() {
            All.Remove(this);
            ProBuilderRoofStats.Changed -= OnStatsChanged;
        }

        private void OnValidate() => QueueRebuild();

        private void OnStatsChanged(ProBuilderRoofStats changed) {
            if (changed == stats)
                QueueRebuild();
        }

        public void QueueRebuild() => RebuildQueue.Queue(this, Rebuild);

        public void Rebuild() {
            TsitskiProBuilderSettings.AutoName(this, stats, "ProBuilder Roof");

            if (!TryGetComponent(out ProBuilderMesh mesh))
                return;

            List<Vector3> positions = new();
            List<Face> faces = new();
            Build(positions, faces);

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

        public List<Vector3> Centerline(RoofPath roofPath, out bool loop) =>
            PathUtility.Rounded(roofPath.path, roofPath.pathRadii, roofPath.closed, activeRoundingSegments, out loop);

        public List<Vector2> ProfileCurve() => ProfileCurve(activeProfile, activeProfileRadii, activeRoundingSegments);

        public static List<Vector2> ProfileCurve(List<Vector2> source, List<float> radii, int segments) {
            List<Vector2> rounded = source is { Count: >= 2 } ? PointRounding.Round(source, radii, false, segments) : DefaultProfile();
            return new RoofCurve(rounded).points.Select(p => p.ToVector2()).ToList();
        }

        private void Build(List<Vector3> positions, List<Face> faces) {
            List<RoofWing> wings = new();
            List<int> pathIndices = new();
            List<RoofPath> roofPaths = activePaths;
            for (int index = 0; index < roofPaths.Count; index++) {
                RoofPath roofPath = roofPaths[index];
                if (roofPath?.path is not { Count: >= 2 })
                    continue;

                List<Vector3> line = Centerline(roofPath, out bool loop);
                wings.Add(new RoofWing {
                    centerline = line.Select(p => new D2(p.x, p.z)).ToList(), closed = loop,
                    gableStart = roofPath.gableStart, gableEnd = roofPath.gableEnd,
                    halfWidth = Mathf.Max(0.01f, roofPath.width) * 0.5, height = Mathf.Max(0.01f, roofPath.height)
                });
                pathIndices.Add(index);
            }

            RoofCurve curve = new(PointRounding.Round(activeProfile is { Count: >= 2 } ? activeProfile : DefaultProfile(), activeProfileRadii, false,
                activeRoundingSegments));
            List<FootprintLoop> loops = RoofGeometry.Footprint(wings);
            footprint = loops.Select(l => l.points.Select(p => p.ToVector2()).ToList()).ToList();
            failedRegions = 0;
            separatedPaths.Clear();

            List<RoofPart> parts = new();
            foreach (List<FootprintLoop> region in RoofGeometry.Regions(loops)) {
                List<int> group = region.SelectMany(l => l.wings).Distinct().OrderBy(i => i).ToList();
                if (TryBuild(group, wings, curve, parts))
                    continue;

                int culprit = -1;
                if (group.Count > 1)
                    foreach (int id in group) {
                        List<RoofPart> attempt = new();
                        if (!TryBuild(group.Where(other => other != id).ToList(), wings, curve, attempt) ||
                            !TryBuild(new List<int> { id }, wings, curve, attempt))
                            continue;
                        parts.AddRange(attempt);
                        culprit = id;
                        break;
                    }

                if (culprit >= 0) {
                    separatedPaths.Add(pathIndices[culprit]);
                    continue;
                }

                foreach (int id in group)
                    if (!TryBuild(new List<int> { id }, wings, curve, parts))
                        failedRegions++;
                if (group.Count > 1)
                    separatedPaths.AddRange(group.Select(id => pathIndices[id]));
            }

            float y = baseHeight;
            float shell = activeThickness;
            foreach (RoofPart part in parts) {
                foreach (RoofFace face in part.faces) {
                    if (face.gable)
                        AddGable(face, y, shell, positions, faces);
                    else
                        AddSlope(face, part.wings[face.wing], curve, y, shell, positions, faces);
                }

                foreach (FootprintLoop loop in part.region)
                    for (int i = 0; i < loop.points.Count; i++)
                        if (!loop.gables[i])
                            AddFascia(loop.points[i], loop.points[(i + 1) % loop.points.Count], y, shell, positions, faces);
            }
        }

        private sealed class RoofPart {
            public List<RoofWing> wings;
            public List<FootprintLoop> region;
            public List<RoofFace> faces;
        }

        private static bool TryBuild(List<int> group, List<RoofWing> all, RoofCurve curve, List<RoofPart> parts) {
            if (group.Count == 0)
                return false;

            List<RoofWing> wings = group.Select(i => all[i]).ToList();
            List<RoofPart> built = new();
            foreach (List<FootprintLoop> region in RoofGeometry.Regions(RoofGeometry.Footprint(wings))) {
                List<RoofFace> roofFaces = RoofGeometry.Skeleton(region, wings, curve, out _, out bool complete);
                if (!complete || !Valid(region, roofFaces, wings, curve))
                    return false;
                built.Add(new RoofPart { wings = wings, region = region, faces = roofFaces });
            }

            parts.AddRange(built);
            return true;
        }

        private static double TriangulatedArea(RoofFace face) {
            List<int> triangles = RoofGeometry.Triangulate(face.polygon);
            double area = 0;
            for (int t = 0; t < triangles.Count; t += 3)
                area += System.Math.Abs(RoofGeometry.SignedArea(new[] { face.polygon[triangles[t]], face.polygon[triangles[t + 1]], face.polygon[triangles[t + 2]] }));
            return area;
        }

        private static bool Valid(List<FootprintLoop> region, List<RoofFace> roofFaces, List<RoofWing> wings, RoofCurve curve) {
            double area = region.Sum(l => RoofGeometry.SignedArea(l.points));
            double covered = roofFaces.Where(f => !f.gable).Sum(TriangulatedArea);
            if (System.Math.Abs(covered - area) > 1e-3 * System.Math.Max(1, area))
                return false;

            Dictionary<(long, long), double> heights = new();
            foreach (RoofFace face in roofFaces.Where(f => !f.gable)) {
                RoofWing wing = wings[face.wing];
                foreach (D2 p in face.polygon) {
                    double height = wing.height * curve.Height(face.Inset(p) / wing.halfWidth);
                    if (height < -1e-3)
                        return false;

                    (long, long) key = ((long)System.Math.Round(p.x * 1000), (long)System.Math.Round(p.y * 1000));
                    if (heights.TryGetValue(key, out double other) && System.Math.Abs(other - height) > 0.01)
                        return false;
                    heights[key] = height;
                }
            }

            return true;
        }

        private static void AddSlope(RoofFace face, RoofWing wing, RoofCurve curve, float y, float shell,
            List<Vector3> positions, List<Face> faces) {
            List<int> triangles = RoofGeometry.Triangulate(face.polygon);
            double[] breaks = curve.inner.Select(p => p.x * wing.halfWidth).ToArray();

            for (int band = 0; band <= breaks.Length; band++) {
                double low = band == 0 ? double.NegativeInfinity : breaks[band - 1];
                double high = band == breaks.Length ? double.PositiveInfinity : breaks[band];
                List<int> top = new(), bottom = new();

                for (int t = 0; t < triangles.Count; t += 3) {
                    List<D2> piece = new() { face.polygon[triangles[t]], face.polygon[triangles[t + 1]], face.polygon[triangles[t + 2]] };
                    piece = Clip(Clip(piece, face, low, true), face, high, false);
                    if (piece.Count < 3)
                        continue;

                    int topStart = positions.Count;
                    foreach (D2 p in piece)
                        positions.Add(new Vector3((float)p.x, y + (float)(wing.height * curve.Height(face.Inset(p) / wing.halfWidth)), (float)p.y));
                    int bottomStart = positions.Count;
                    for (int i = 0; i < piece.Count; i++)
                        positions.Add(positions[topStart + i] + Vector3.down * shell);

                    for (int i = 1; i + 1 < piece.Count; i++) {
                        AddTriangle(positions, top, topStart, topStart + i, topStart + i + 1, Vector3.up);
                        AddTriangle(positions, bottom, bottomStart, bottomStart + i, bottomStart + i + 1, Vector3.down);
                    }
                }

                if (top.Count > 0)
                    faces.Add(new Face(top) { smoothingGroup = NoSmoothing });
                if (bottom.Count > 0)
                    faces.Add(new Face(bottom) { smoothingGroup = NoSmoothing });
            }
        }

        private static void AddGable(RoofFace face, float y, float shell, List<Vector3> positions, List<Face> faces) {
            if (face.polygon.Count < 2)
                return;

            D2 along = (face.edgeEnd - face.edgeStart).Normalized;
            List<D2> top = face.polygon.Select((p, i) => new D2(along.Dot(p - face.edgeStart), face.heights[i])).ToList();
            List<D2> band = new(top);
            for (int i = top.Count - 1; i >= 0; i--)
                band.Add(new D2(top[i].x, top[i].y - shell));
            band = RoofGeometry.Simplify(band);
            if (band.Count < 3)
                return;

            Vector3 alongWorld = new((float)along.x, 0f, (float)along.y);
            Vector3 outward = -new Vector3((float)face.normal.x, 0f, (float)face.normal.y);
            Vector3 origin = new((float)face.edgeStart.x, y, (float)face.edgeStart.y);

            int start = positions.Count;
            positions.AddRange(band.Select(p => origin + alongWorld * (float)p.x + Vector3.up * (float)p.y));

            List<int> triangles = RoofGeometry.Triangulate(band);
            List<int> indices = new();
            for (int t = 0; t < triangles.Count; t += 3)
                AddTriangle(positions, indices, start + triangles[t], start + triangles[t + 1], start + triangles[t + 2], outward);
            if (indices.Count > 0)
                faces.Add(new Face(indices) { smoothingGroup = NoSmoothing });
        }

        private static List<D2> Clip(List<D2> polygon, RoofFace face, double limit, bool keepAbove) {
            if (double.IsInfinity(limit))
                return polygon;

            List<D2> result = new();
            for (int i = 0; i < polygon.Count; i++) {
                D2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                double da = face.Inset(a) - limit, db = face.Inset(b) - limit;
                if (!keepAbove) {
                    da = -da;
                    db = -db;
                }

                if (da >= 0)
                    result.Add(a);
                if (da >= 0 != db >= 0)
                    result.Add(a + (b - a) * (da / (da - db)));
            }

            return result;
        }

        private static void AddFascia(D2 a, D2 b, float y, float shell, List<Vector3> positions, List<Face> faces) {
            D2 outward = (b - a).Normalized.Left * -1;
            int start = positions.Count;
            positions.Add(new Vector3((float)a.x, y, (float)a.y));
            positions.Add(new Vector3((float)b.x, y, (float)b.y));
            positions.Add(new Vector3((float)b.x, y - shell, (float)b.y));
            positions.Add(new Vector3((float)a.x, y - shell, (float)a.y));

            Vector3 facing = new((float)outward.x, 0f, (float)outward.y);
            List<int> indices = new();
            AddTriangle(positions, indices, start, start + 1, start + 2, facing);
            AddTriangle(positions, indices, start, start + 2, start + 3, facing);
            if (indices.Count > 0)
                faces.Add(new Face(indices) { smoothingGroup = NoSmoothing });
        }

        private static void AddTriangle(List<Vector3> positions, List<int> indices, int a, int b, int c, Vector3 facing) {
            Vector3 normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            if (normal.sqrMagnitude < 1e-14f)
                return;

            if (Vector3.Dot(normal, facing) < 0f)
                (b, c) = (c, b);
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }
    }
}
