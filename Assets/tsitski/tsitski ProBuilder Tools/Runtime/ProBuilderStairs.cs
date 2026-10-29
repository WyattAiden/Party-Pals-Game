using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;

using Tsitski.Core;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    [AddComponentMenu("tsitski ProBuilder Tools/ProBuilder Stairs")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProBuilderMesh))]
    public class ProBuilderStairs : MonoBehaviour, IPathOwner {
        public const float MinSize = 0.01f;
        private const float Epsilon = 1e-4f;
        private const float MaxLandingCorner = 3f;
        private const float MaxWindingTurn = 175f;
        private const float ArcStep = 10f;
        private const float MinTrimRadius = 0.001f;
        private const float CutterTopClearance = 0.01f;
        private const int NoSmoothing = 0;
        private const int UndersideSmoothing = 1;
        private const string CutterName = "Stairs Cutter";
        private const string LeftTrimName = "Stairs Trim Left";
        private const string RightTrimName = "Stairs Trim Right";

        [Tooltip(Tooltips.StairsStats)] public ProBuilderStairsStats stats;
        [Tooltip(Tooltips.StairsPath)] public List<Vector3> path = new() { Vector3.zero, new(0f, 1.5f, 2.5f) };
        [Tooltip(Tooltips.StairsRadii)] public List<float> pathRadii = new();
        [Tooltip(Tooltips.StairsWidth)] [Min(MinSize)] public float width = 1f;
        [Tooltip(Tooltips.StepHeight)] [Min(MinSize)] public float stepHeight = 0.15f;
        [Tooltip(Tooltips.Solid)] public bool solid = true;
        [Tooltip(Tooltips.StairsThickness)] [Min(MinSize)] public float thickness = 0.15f;
        [Tooltip(Tooltips.GenerateCutter)] public bool generateCutter;
        [Tooltip(Tooltips.CutterHeadroom)] [Min(MinSize)] public float cutterHeadroom = 2.1f;
        [Tooltip(Tooltips.LeftTrim)] public ProBuilderTrimStats leftTrim;
        [Tooltip(Tooltips.RightTrim)] public ProBuilderTrimStats rightTrim;
        [Tooltip(Tooltips.SmoothCollider)] public bool smoothCollider;

        [SerializeField, HideInInspector] private ProBuilderCutter generatedCutter;
        [SerializeField, HideInInspector] private ProBuilderTrim generatedLeftTrim;
        [SerializeField, HideInInspector] private ProBuilderTrim generatedRightTrim;
        [SerializeField, HideInInspector] private Mesh smoothColliderMesh;

        private static readonly Dictionary<Mesh, ProBuilderStairs> ColliderOwners = new();
        [NonSerialized] private bool _buildingSmooth;

        public static event Action<ProBuilderMesh> Rebuilt;

#if UNITY_EDITOR
#endif

        List<Vector3> IPathOwner.localPath { get => path; set => path = value; }
        bool IPathOwner.pathInStats => false;

        public float activeWidth => Mathf.Max(MinSize, stats ? stats.width : width);
        public float activeStepHeight => Mathf.Max(MinSize, stats ? stats.stepHeight : stepHeight);
        public bool activeSolid => stats ? stats.solid : solid;
        public float activeThickness => Mathf.Max(MinSize, stats ? stats.thickness : thickness);
        public bool activeGenerateCutter => stats ? stats.generateCutter : generateCutter;
        public float activeCutterHeadroom => Mathf.Max(MinSize, stats ? stats.cutterHeadroom : cutterHeadroom);
        public ProBuilderTrimStats activeLeftTrim => stats ? stats.leftTrim : leftTrim;
        public ProBuilderTrimStats activeRightTrim => stats ? stats.rightTrim : rightTrim;
        public bool activeSmoothCollider => stats ? stats.smoothCollider : smoothCollider;

        public int stepCount {
            get {
                Prepare(out List<Vector3> points, out _, out List<(int start, int end)> runs);
                return runs.Sum(r => Steps(Mathf.Abs(points[r.end].y - points[r.start].y)));
            }
        }

        private struct Station {
            public Vector3 center, left, right;

            public static Station Lerp(Station a, Station b, float t) => new() {
                center = Vector3.Lerp(a.center, b.center, t),
                left = Vector3.Lerp(a.left, b.left, t),
                right = Vector3.Lerp(a.right, b.right, t)
            };

            public static Station Straight(Vector3 center, Vector3 direction, float halfWidth) {
                Vector3 offset = Vector3.Cross(Vector3.up, direction) * halfWidth;
                return new Station { center = center, left = center - offset, right = center + offset };
            }
        }

        private int Steps(float rise) => rise < Epsilon ? 0 : Mathf.Max(1, Mathf.RoundToInt(rise / activeStepHeight));

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
            if (TryGetComponent(out ProBuilderMesh mesh) && mesh.faceCount == 0)
                mesh.Clear();

            ProBuilderStairsStats.Changed += OnStatsChanged;
            QueueRebuild();
        }

        private void OnDisable() => ProBuilderStairsStats.Changed -= OnStatsChanged;

        private void OnValidate() => QueueRebuild();

        private void OnStatsChanged(ProBuilderStairsStats changed) {
            if (changed == stats)
                QueueRebuild();
        }

        public void QueueRebuild() => RebuildQueue.Queue(this, Rebuild);

        public void Rebuild() {
            TsitskiProBuilderSettings.AutoName(this, stats, "ProBuilder Stairs");

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
            CutLightmapUnwrap.ApplyToProBuilderMesh(mesh);

            if (TryGetComponent(out MeshRenderer meshRenderer) && !meshRenderer.sharedMaterial)
                meshRenderer.sharedMaterial = TsitskiProBuilderSettings.DefaultMaterial;

            SyncSmoothCollider();

#if UNITY_EDITOR
            EditorUtility.SetDirty(mesh);
#endif
            Rebuilt?.Invoke(mesh);
        }

        private void SyncSmoothCollider() {
            if (!TryGetComponent(out MeshCollider meshCollider))
                return;

            if (!activeSmoothCollider) {
                if (smoothColliderMesh) {
                    if (!ColliderOwners.TryGetValue(smoothColliderMesh, out ProBuilderStairs previous) || !previous || previous == this) {
                        ColliderOwners.Remove(smoothColliderMesh);
                        DestroyImmediate(smoothColliderMesh);
                    }
                    smoothColliderMesh = null;
                }

                Mesh renderMesh = TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (meshCollider.sharedMesh != renderMesh) {
                    meshCollider.sharedMesh = null;
                    meshCollider.sharedMesh = renderMesh;
                }
                return;
            }

            List<Vector3> positions = new();
            List<Face> faces = new();
            _buildingSmooth = true;
            try {
                Build(positions, faces);
            }
            finally {
                _buildingSmooth = false;
            }

            if (smoothColliderMesh && ColliderOwners.TryGetValue(smoothColliderMesh, out ProBuilderStairs owner) && owner && owner != this)
                smoothColliderMesh = null;
            if (!smoothColliderMesh)
                smoothColliderMesh = new Mesh();

            ColliderOwners[smoothColliderMesh] = this;
            smoothColliderMesh.name = $"{name} Smooth Collider";
            smoothColliderMesh.Clear();
            smoothColliderMesh.SetVertices(positions);
            smoothColliderMesh.SetTriangles(faces.SelectMany(f => f.indexes).ToList(), 0);
            smoothColliderMesh.RecalculateBounds();

            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = smoothColliderMesh;
        }

        private void SyncCutter(List<Vector3> points, List<float> radii, List<(int start, int end)> runs) {
            if (!activeGenerateCutter || points.Count < 2) {
                if (generatedCutter)
                    DestroyImmediate(generatedCutter.gameObject);
                return;
            }

            if (!generatedCutter) {
                GameObject child = new(CutterName);
                child.transform.SetParent(transform, false);
                generatedCutter = child.AddComponent<ProBuilderCutter>();
            }

            Transform t = generatedCutter.transform;
            if (t.localPosition != Vector3.zero)
                t.localPosition = Vector3.zero;
            if (t.localRotation != Quaternion.identity)
                t.localRotation = Quaternion.identity;
            if (t.localScale != Vector3.one)
                t.localScale = Vector3.one;

            float halfWidth = activeWidth * 0.5f;
            float topY = points.Max(p => p.y) + CutterTopClearance;
            float bottomY = Mathf.Max(topY - activeCutterHeadroom, points.Min(p => p.y) + 0.1f);
            float height = Mathf.Max(MinSize, topY - bottomY), centerY = (topY + bottomY) * 0.5f;

            List<Vector3> heights = new(points);
            foreach ((int start, int end) in runs) {
                float total = 0f;
                for (int i = start; i < end; i++)
                    total += Vector3.Distance(FlatPoint(points[i]), FlatPoint(points[i + 1]));

                float along = 0f;
                for (int i = start + 1; i < end; i++) {
                    along += Vector3.Distance(FlatPoint(points[i - 1]), FlatPoint(points[i]));
                    heights[i] = new Vector3(points[i].x, Mathf.Lerp(points[start].y, points[end].y, total > Epsilon ? along / total : 0f), points[i].z);
                }
            }

            List<float> sourceRadii = radii.Select(r => r > 0f ? r + halfWidth : 0f).ToList();
            bool reversed = heights[0].y > heights[^1].y;
            if (reversed) {
                heights.Reverse();
                sourceRadii.Reverse();
            }

            float cutoff = topY - 0.1f - activeCutterHeadroom;
            List<Vector3> kept = new() { heights[^1] };
            List<float> cutterRadii = new() { sourceRadii[^1] };
            bool trimmed = false;

            for (int i = heights.Count - 2; i >= 0; i--) {
                if (heights[i].y >= cutoff) {
                    kept.Add(heights[i]);
                    cutterRadii.Add(sourceRadii[i]);
                    continue;
                }

                Vector3 above = heights[i + 1], below = heights[i];
                kept.Add(Vector3.Lerp(above, below, (above.y - cutoff) / Mathf.Max(Epsilon, above.y - below.y)));
                cutterRadii.Add(0f);
                trimmed = true;
                break;
            }

            kept.Reverse();
            cutterRadii.Reverse();
            if (reversed) {
                kept.Reverse();
                cutterRadii.Reverse();
            }

            List<Vector3> cutterPoints = kept.Select(p => new Vector3(p.x, centerY, p.z)).ToList();
            int startIndex = reversed ? cutterPoints.Count - 1 : 0, nextIndex = reversed ? cutterPoints.Count - 2 : 1;

            if (!trimmed && cutterPoints.Count >= 2) {
                float firstLength = Vector3.Distance(cutterPoints[startIndex], cutterPoints[nextIndex]);
                if (firstLength > Epsilon)
                    cutterPoints[startIndex] = Vector3.Lerp(cutterPoints[startIndex], cutterPoints[nextIndex], Mathf.Min(0.5f, activeThickness / firstLength));
            }

            if (generatedCutter.shape == ProBuilderCutterShape.Path &&
                Mathf.Approximately(generatedCutter.pathWidth, activeWidth) && Mathf.Approximately(generatedCutter.pathHeight, height) &&
                generatedCutter.pathPoints != null && generatedCutter.pathPoints.SequenceEqual(cutterPoints) &&
                generatedCutter.pathRadii != null && generatedCutter.pathRadii.SequenceEqual(cutterRadii) &&
                (generatedCutter.pathRotations == null || generatedCutter.pathRotations.Count == 0))
                return;

            generatedCutter.shape = ProBuilderCutterShape.Path;
            generatedCutter.pathPoints = cutterPoints;
            generatedCutter.pathRadii = cutterRadii;
            generatedCutter.pathRotations = new List<Quaternion>();
            generatedCutter.pathWidth = activeWidth;
            generatedCutter.pathHeight = height;
            generatedCutter.NotifyChanged();
        }

        private ProBuilderTrim SyncTrim(ProBuilderTrim trim, ProBuilderTrimStats trimStats, string trimName, List<Vector3> points,
            List<float> radii, List<(int start, int end)> runs, float side, float halfWidth) {
            List<Vector3> sidePath = new();
            List<float> sideRadii = new();
            if (points.Count >= 2)
                SidePath(points, radii, runs, side, halfWidth, sidePath, sideRadii);
            if (!trimStats || sidePath.Count < 2) {
                if (trim)
                    DestroyImmediate(trim.gameObject);
                return null;
            }

            if (side < 0f) {
                sidePath.Reverse();
                sideRadii.Reverse();
            }

            if (!trim) {
                GameObject child = new(trimName);
                child.transform.SetParent(transform, false);
                trim = child.AddComponent<ProBuilderTrim>();
                if (!child.TryGetComponent(out MeshCollider _))
                    child.AddComponent<MeshCollider>();
            }

            Transform t = trim.transform;
            if (t.localPosition != Vector3.zero)
                t.localPosition = Vector3.zero;
            if (t.localRotation != Quaternion.identity)
                t.localRotation = Quaternion.identity;
            if (t.localScale != Vector3.one)
                t.localScale = Vector3.one;

            if (trim.stats == trimStats && !trim.closed && trim.path != null && trim.path.SequenceEqual(sidePath) &&
                trim.pathRadii != null && trim.pathRadii.SequenceEqual(sideRadii))
                return trim;

            trim.stats = trimStats;
            trim.path = sidePath;
            trim.pathRadii = sideRadii;
            trim.closed = false;
            trim.Rebuild();
            return trim;
        }

        private static void SidePath(List<Vector3> points, List<float> radii, List<(int start, int end)> runs, float side, float halfWidth,
            List<Vector3> result, List<float> resultRadii) {
            int last = points.Count - 1;

            void Add(Vector3 p, float radius) {
                if (result.Count > 0 && (result[^1] - p).sqrMagnitude <= Epsilon * Epsilon) {
                    resultRadii[^1] = Mathf.Max(resultRadii[^1], radius);
                    return;
                }

                result.Add(p);
                resultRadii.Add(radius);
            }

            foreach ((int a, int b) in runs) {
                bool startLanding = a > 0, endLanding = b < last;

                if (startLanding) {
                    Vector3 center = points[a];
                    Vector3 into = Flat(points[a] - points[a - 1]), outOf = Flat(points[a + 1] - points[a]);
                    if (into == Vector3.zero)
                        into = outOf;
                    if (outOf == Vector3.zero)
                        outOf = into;

                    if (into != Vector3.zero) {
                        Vector3 arrive = center - into * halfWidth + Vector3.Cross(Vector3.up, into) * (side * halfWidth);
                        Vector3 leave = center + outOf * halfWidth + Vector3.Cross(Vector3.up, outOf) * (side * halfWidth);
                        float outerSide = Vector3.Cross(into, outOf).y > 0f ? -1f : 1f;

                        Add(arrive, 0f);
                        if (Mathf.Approximately(side, outerSide) && Intersect(arrive, into, leave, outOf, out Vector3 corner) &&
                            (corner - center).sqrMagnitude <= MaxLandingCorner * MaxLandingCorner * halfWidth * halfWidth)
                            Add(new Vector3(corner.x, center.y, corner.z), 0f);
                        Add(leave, 0f);
                    }
                }

                Vector3 firstDirection = Flat(points[a + 1] - points[a]), lastDirection = Flat(points[b] - points[b - 1]);
                if (firstDirection == Vector3.zero || lastDirection == Vector3.zero)
                    continue;

                int count = b - a + 1;
                float[] tangents = CornerTangents(points, radii, a, b, startLanding, endLanding, halfWidth, out float[] turns);
                float[] arcs = new float[count];
                for (int i = 1; i < count - 1; i++)
                    if (tangents[i] > Epsilon)
                        arcs[i] = tangents[i] / Mathf.Tan(turns[i] * 0.5f * Mathf.Deg2Rad) * turns[i] * Mathf.Deg2Rad;

                float[] reach = new float[count];
                float startOffset = startLanding ? halfWidth : 0f, raw = 0f, saved = 0f;
                for (int i = 1; i < count; i++) {
                    raw += Vector3.Distance(FlatPoint(points[a + i - 1]), FlatPoint(points[a + i]));
                    if (i == count - 1)
                        break;

                    reach[i] = raw - startOffset - saved - tangents[i] + arcs[i] * 0.5f;
                    saved += 2f * tangents[i] - arcs[i];
                }

                float total = raw - startOffset - (endLanding ? halfWidth : 0f) - saved;
                float Height(float along) => Mathf.Lerp(points[a].y, points[b].y, total > Epsilon ? Mathf.Clamp01(along / total) : 0f);

                Vector3 Offset(Vector3 p, Vector3 direction) => FlatPoint(p) + Vector3.Cross(Vector3.up, direction) * (side * halfWidth);

                Vector3 start = Offset(points[a], firstDirection) + firstDirection * (startLanding ? halfWidth : 0f);
                Add(new Vector3(start.x, points[a].y, start.z), 0f);

                for (int i = 1; i < count - 1; i++) {
                    int p = a + i;
                    Vector3 into = Flat(points[p] - points[p - 1]), outOf = Flat(points[p + 1] - points[p]);
                    Vector3 rightIn = Vector3.Cross(Vector3.up, into), rightOut = Vector3.Cross(Vector3.up, outOf);
                    float denominator = 1f + Vector3.Dot(rightIn, rightOut);
                    if (denominator < 0.1f)
                        continue;

                    Vector3 mitre = FlatPoint(points[p]) + (rightIn + rightOut) * (side * halfWidth / denominator);
                    float radius = 0f;
                    if (tangents[i] > Epsilon) {
                        float centerRadius = tangents[i] / Mathf.Tan(turns[i] * 0.5f * Mathf.Deg2Rad);
                        float turnSide = Vector3.Cross(into, outOf).y > 0f ? 1f : -1f;

                        if (!Mathf.Approximately(side, turnSide)) {
                            float edgeRadius = centerRadius + halfWidth;
                            float edgeTangent = edgeRadius * Mathf.Tan(turns[i] * 0.5f * Mathf.Deg2Rad);
                            Vector3 arcStart = mitre - into * edgeTangent, arcEnd = mitre + outOf * edgeTangent;

                            Add(new Vector3(arcStart.x, Height(reach[i] - arcs[i] * 0.5f), arcStart.z), 0f);
                            Add(new Vector3(mitre.x, Height(reach[i]), mitre.z), edgeRadius);
                            Add(new Vector3(arcEnd.x, Height(reach[i] + arcs[i] * 0.5f), arcEnd.z), 0f);
                            continue;
                        }

                        radius = Mathf.Max(centerRadius - halfWidth, MinTrimRadius);
                    }

                    Add(new Vector3(mitre.x, Height(reach[i]), mitre.z), radius);
                }

                Vector3 end = Offset(points[b], lastDirection) - lastDirection * (endLanding ? halfWidth : 0f);
                Add(new Vector3(end.x, points[b].y, end.z), 0f);
            }

            for (int i = 1; i < result.Count - 1; i++) {
                if (resultRadii[i] <= MinTrimRadius)
                    continue;

                Vector3 into = result[i] - result[i - 1], outOf = result[i + 1] - result[i];
                Vector3 flatIn = new(into.x, 0f, into.z), flatOut = new(outOf.x, 0f, outOf.z);
                if (flatIn.sqrMagnitude < Epsilon || flatOut.sqrMagnitude < Epsilon)
                    continue;

                float planTurn = Vector3.Angle(flatIn, flatOut), turn = Vector3.Angle(into, outOf);
                if (planTurn < 0.1f || turn < 0.1f)
                    continue;

                Vector3 bisector = (outOf.normalized - into.normalized).normalized;
                float flatBisector = new Vector2(bisector.x, bisector.z).magnitude;
                float planBulge = 1f / Mathf.Cos(planTurn * 0.5f * Mathf.Deg2Rad) - 1f;
                float bulge = 1f / Mathf.Cos(turn * 0.5f * Mathf.Deg2Rad) - 1f;
                if (flatBisector < 0.1f || bulge < Epsilon)
                    continue;

                resultRadii[i] *= planBulge / (bulge * flatBisector);
            }
        }

        private void Prepare(out List<Vector3> points, out List<float> radii, out List<(int start, int end)> runs) {
            points = new List<Vector3>();
            radii = new List<float>();
            runs = new List<(int start, int end)>();

            if (path != null) {
                for (int i = 0; i < path.Count; i++) {
                    float radius = PointRounding.Radius(pathRadii, i);
                    if (points.Count > 0 && (points[^1] - path[i]).sqrMagnitude <= Epsilon * Epsilon) {
                        radii[^1] = Mathf.Max(radii[^1], radius);
                        continue;
                    }

                    points.Add(path[i]);
                    radii.Add(radius);
                }
            }

            int last = points.Count - 1, start = 0;
            for (int i = 1; i <= last; i++) {
                if (i < last && Winds(points, radii, i))
                    continue;

                runs.Add((start, i));
                start = i;
            }
        }

        private static bool Winds(List<Vector3> points, List<float> radii, int i) {
            Vector3 into = Flat(points[i] - points[i - 1]), outOf = Flat(points[i + 1] - points[i]);
            return radii[i] > 0f && into != Vector3.zero && outOf != Vector3.zero && Vector3.Angle(into, outOf) < MaxWindingTurn;
        }

        private void Build(List<Vector3> positions, List<Face> faces) {
            Prepare(out List<Vector3> points, out List<float> radii, out List<(int start, int end)> runs);
            if (!_buildingSmooth) {
                SyncCutter(points, radii, runs);
                float trimHalfWidth = activeWidth * 0.5f;
                generatedLeftTrim = SyncTrim(generatedLeftTrim, activeLeftTrim, LeftTrimName, points, radii, runs, -1f, trimHalfWidth);
                generatedRightTrim = SyncTrim(generatedRightTrim, activeRightTrim, RightTrimName, points, radii, runs, 1f, trimHalfWidth);
            }
            if (points.Count < 2)
                return;

            float halfWidth = activeWidth * 0.5f;
            float baseY = points.Min(p => p.y);
            int last = points.Count - 1;

            foreach ((int start, int end) in runs)
                Run(positions, faces, points, radii, start, end, start > 0, end < last, baseY, halfWidth);

            foreach ((int start, _) in runs.Skip(1))
                Landing(positions, faces, points[start], Flat(points[start] - points[start - 1]), Flat(points[start + 1] - points[start]), baseY,
                    halfWidth);
        }

        private void Run(List<Vector3> positions, List<Face> faces, List<Vector3> points, List<float> radii, int a, int b,
            bool startLanding, bool endLanding, float baseY, float halfWidth) {
            List<Station> line = Centerline(points, radii, a, b, startLanding, endLanding, halfWidth);
            if (line == null)
                return;

            Vector3 start = line[0].center + Vector3.up * points[a].y;
            Vector3 end = line[^1].center + Vector3.up * points[b].y;

            if (line.Count == 2) {
                if (Vector3.Dot(end - start, Flat(points[a + 1] - points[a])) < Epsilon)
                    return;

                if (start.y <= end.y)
                    Flight(positions, faces, start, end, startLanding, baseY, halfWidth);
                else
                    Flight(positions, faces, end, start, endLanding, baseY, halfWidth);
                return;
            }

            if (start.y <= end.y) {
                Winding(positions, faces, line, start.y, end.y, startLanding, baseY);
                return;
            }

            line.Reverse();
            for (int i = 0; i < line.Count; i++)
                line[i] = new Station { center = line[i].center, left = line[i].right, right = line[i].left };
            Winding(positions, faces, line, end.y, start.y, endLanding, baseY);
        }

        private static float[] CornerTangents(List<Vector3> points, List<float> radii, int a, int b, bool startLanding, bool endLanding,
            float halfWidth, out float[] turns) {
            int count = b - a + 1;
            turns = new float[count];
            float[] tangents = new float[count];
            for (int i = 1; i < count - 1; i++) {
                int p = a + i;
                turns[i] = Vector3.Angle(Flat(points[p] - points[p - 1]), Flat(points[p + 1] - points[p]));
                if (turns[i] > 0.1f)
                    tangents[i] = (halfWidth + radii[p]) * Mathf.Tan(turns[i] * 0.5f * Mathf.Deg2Rad);
            }

            float[] scales = new float[count - 1];
            for (int i = 0; i < count - 1; i++) {
                float available = Vector3.Distance(FlatPoint(points[a + i]), FlatPoint(points[a + i + 1]))
                                  - (i == 0 && startLanding ? halfWidth : 0f) - (i == count - 2 && endLanding ? halfWidth : 0f);
                float demand = tangents[i] + tangents[i + 1];
                scales[i] = demand > available && demand > 0f ? Mathf.Max(0f, available) / demand : 1f;
            }

            float[] scaled = new float[count];
            for (int i = 1; i < count - 1; i++)
                scaled[i] = tangents[i] * Mathf.Min(scales[i - 1], scales[i]);
            return scaled;
        }

        private static List<Station> Centerline(List<Vector3> points, List<float> radii, int a, int b, bool startLanding, bool endLanding,
            float halfWidth) {
            Vector3 firstDirection = Flat(points[a + 1] - points[a]), lastDirection = Flat(points[b] - points[b - 1]);
            if (firstDirection == Vector3.zero || lastDirection == Vector3.zero)
                return null;

            int count = b - a + 1;
            float[] tangents = CornerTangents(points, radii, a, b, startLanding, endLanding, halfWidth, out float[] turns);

            List<Station> line = new() {
                Station.Straight(FlatPoint(points[a]) + firstDirection * (startLanding ? halfWidth : 0f), firstDirection, halfWidth)
            };

            for (int i = 1; i < count - 1; i++) {
                float tangent = tangents[i];
                if (tangent < Epsilon)
                    continue;

                int p = a + i;
                Vector3 into = Flat(points[p] - points[p - 1]), outOf = Flat(points[p + 1] - points[p]);
                float radius = tangent / Mathf.Tan(turns[i] * 0.5f * Mathf.Deg2Rad);
                float side = Vector3.Cross(into, outOf).y > 0f ? 1f : -1f;
                Vector3 arcStart = FlatPoint(points[p]) - into * tangent;
                Vector3 center = arcStart + Vector3.Cross(Vector3.up, into) * (side * radius);
                Vector3 spokeStart = arcStart - center;
                float inner = Mathf.Max(0f, radius - halfWidth) / radius, outer = (radius + halfWidth) / radius;
                int segments = Mathf.Max(1, Mathf.CeilToInt(turns[i] / ArcStep));

                for (int k = 0; k <= segments; k++) {
                    Vector3 spoke = Quaternion.AngleAxis(side * turns[i] * k / segments, Vector3.up) * spokeStart;
                    line.Add(new Station {
                        center = center + spoke,
                        left = center + spoke * (side > 0f ? outer : inner),
                        right = center + spoke * (side > 0f ? inner : outer)
                    });
                }
            }

            line.Add(Station.Straight(FlatPoint(points[b]) - lastDirection * (endLanding ? halfWidth : 0f), lastDirection, halfWidth));
            return line;
        }

        private void Winding(List<Vector3> positions, List<Face> faces, List<Station> line, float lowY, float highY, bool lowIsLanding, float baseY) {
            float[] distance = new float[line.Count];
            for (int j = 1; j < line.Count; j++)
                distance[j] = distance[j - 1] + Vector3.Distance(line[j - 1].center, line[j].center);

            float length = distance[^1];
            if (length < Epsilon)
                return;

            float rise = highY - lowY;
            int steps = Steps(rise);
            float depth = length / Mathf.Max(1, steps), height = steps > 0 ? rise / steps : 0f;
            float bottomY = BottomY(lowY, rise, lowIsLanding, baseY);
            float slopeOffset = activeThickness * Mathf.Sqrt(1f + height * height / (depth * depth));

            List<Station> stations = new();
            List<float> along = new();
            int next = 1;
            for (int j = 0; j < line.Count - 1; j++) {
                stations.Add(line[j]);
                along.Add(distance[j]);

                for (; next < steps && next * depth < distance[j + 1]; next++) {
                    float s = next * depth;
                    if (s <= distance[j])
                        continue;

                    stations.Add(Station.Lerp(line[j], line[j + 1], (s - distance[j]) / (distance[j + 1] - distance[j])));
                    along.Add(s);
                }
            }

            stations.Add(line[^1]);
            along.Add(length);

            float Bottom(float s) => lowY + (activeSolid ? bottomY : Mathf.Max(bottomY, Mathf.Min(s * height / depth - slopeOffset, rise - activeThickness)));
            Vector3 At(Vector3 p, float y) => new(p.x, y, p.z);

            bool smooth = _buildingSmooth;
            float StationTop(int j) => lowY + along[j] * rise / length;

            float[] tops = new float[stations.Count - 1];
            for (int j = 0; j < tops.Length; j++) {
                int step = Mathf.Clamp(Mathf.FloorToInt((along[j] + along[j + 1]) * 0.5f / depth), 0, Mathf.Max(0, steps - 1));
                tops[j] = lowY + (steps > 0 ? (step + 1) * height : 0f);

                Station a = stations[j], b = stations[j + 1];
                float top = tops[j], ya = Bottom(along[j]), yb = Bottom(along[j + 1]);
                float topA = smooth ? StationTop(j) : top, topB = smooth ? StationTop(j + 1) : top;
                Vector3 middle = (a.center + b.center) * 0.5f;

                AddStrip(positions, faces, Vector3.up, At(a.left, topA), At(a.right, topA), At(b.right, topB), At(b.left, topB));
                AddStrip(positions, faces, Vector3.down, At(a.left, ya), At(a.right, ya), At(b.right, yb), At(b.left, yb),
                    Mathf.Abs(ya - yb) > Epsilon ? UndersideSmoothing : NoSmoothing);
                List<float> extras = new();
                if (j > 0 && !smooth)
                    extras.Add(tops[j - 1]);
                else if (j == 0 && lowIsLanding)
                    extras.Add(lowY);
                extras = extras.Where(y => y > ya + Epsilon && y < topA - Epsilon).OrderByDescending(y => y).ToList();

                Vector3[] Side(Vector3 from, Vector3 to) => new[] { At(to, topB), At(from, topA) }
                    .Concat(extras.Select(y => At(from, y)))
                    .Concat(new[] { At(from, ya), At(to, yb) })
                    .ToArray();

                AddFace(positions, faces, (a.left + b.left) * 0.5f - middle, Side(a.left, b.left));
                AddFace(positions, faces, (a.right + b.right) * 0.5f - middle, Side(a.right, b.right));
            }

            for (int j = 0; j < stations.Count; j++) {
                float before = j > 0 ? smooth ? StationTop(j) : tops[j - 1] : Bottom(along[j]);
                float after = j < tops.Length ? smooth ? StationTop(j) : tops[j] : Bottom(along[j]);
                if (Mathf.Abs(after - before) < Epsilon)
                    continue;

                Station s = stations[j];
                Vector3 forward = Vector3.Cross(s.right - s.left, Vector3.up);
                AddFace(positions, faces, forward * Mathf.Sign(before - after),
                    At(s.left, before), At(s.right, before), At(s.right, after), At(s.left, after));
            }
        }

        private static Vector3 Flat(Vector3 v) {
            v.y = 0f;
            return v.sqrMagnitude > Epsilon * Epsilon ? v.normalized : Vector3.zero;
        }

        private static Vector3 FlatPoint(Vector3 v) => new(v.x, 0f, v.z);

        private float BottomY(float lowY, float rise, bool lowIsLanding, float baseY) => activeSolid
            ? Mathf.Min(baseY - lowY, rise < Epsilon ? -activeThickness : 0f)
            : lowIsLanding || rise < Epsilon
                ? -activeThickness
                : 0f;

        private void Flight(List<Vector3> positions, List<Face> faces, Vector3 low, Vector3 high, bool lowIsLanding, float baseY, float halfWidth) {
            Vector3 run = high - low;
            float rise = run.y;
            run.y = 0f;
            float runLength = run.magnitude;

            Vector3 forward = run / runLength;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float bottomY = BottomY(low.y, rise, lowIsLanding, baseY);

            int steps = Steps(rise);
            List<Vector2> profile = Profile(steps, runLength, rise, bottomY);
            List<List<Vector2>> sides = new();

            if (_buildingSmooth && steps > 0) {
                List<Vector2> ramp = new() { profile[0] };
                if (bottomY < -Epsilon)
                    ramp.Add(Vector2.zero);
                ramp.Add(new Vector2(runLength, rise));
                ramp.AddRange(profile.Skip(1 + steps * 2));
                profile = ramp;
                sides.Add(new List<Vector2>(ramp));
            }
            else if (steps == 0) {
                sides.Add(profile);
            }
            else {
                float depth = runLength / steps, height = rise / steps;
                List<Vector2> slab = new() { Vector2.zero };
                for (int i = 0; i < steps; i++) {
                    Vector2 back = new((i + 1) * depth, (i + 1) * height);
                    sides.Add(new List<Vector2> { new(i * depth, i * height), new(i * depth, (i + 1) * height), back });
                    slab.Add(back);
                }

                slab.AddRange(profile.Skip(1 + steps * 2));
                if (bottomY < -Epsilon) {
                    slab.Add(profile[0]);
                    profile.Insert(1, Vector2.zero);
                }
                sides.Add(slab);
            }

            if (CutterOutline.SignedArea(profile) < 0f)
                profile.Reverse();

            Vector3 Point(Vector2 p, float side) => low + forward * p.x + Vector3.up * p.y + right * side;

            for (int k = 0; k < profile.Count; k++) {
                Vector2 a = profile[k], b = profile[(k + 1) % profile.Count];
                Vector3 facing = forward * (b.y - a.y) - Vector3.up * (b.x - a.x);
                AddFace(positions, faces, facing, Point(a, -halfWidth), Point(b, -halfWidth), Point(b, halfWidth), Point(a, halfWidth));
            }

            foreach (List<Vector2> piece in sides) {
                Vector3[] left = new Vector3[piece.Count], rightSide = new Vector3[piece.Count];
                for (int i = 0; i < piece.Count; i++) {
                    left[i] = Point(piece[i], -halfWidth);
                    rightSide[i] = Point(piece[i], halfWidth);
                }

                AddFace(positions, faces, -right, left);
                AddFace(positions, faces, right, rightSide);
            }
        }

        public List<List<Vector2>> ProfileLines() {
            Prepare(out List<Vector3> points, out _, out _);
            List<List<Vector2>> lines = new();
            if (points.Count < 2)
                return lines;

            float baseY = points.Min(p => p.y), x = 0f;
            int last = points.Count - 1;

            for (int i = 0; i < last; i++) {
                Vector3 a = points[i], b = points[i + 1];
                float runLength = Vector3.Distance(FlatPoint(a), FlatPoint(b));
                if (runLength < Epsilon)
                    continue;

                bool ascending = b.y >= a.y;
                float rise = Mathf.Abs(b.y - a.y), lowY = Mathf.Min(a.y, b.y);
                bool lowIsLanding = ascending ? i > 0 : i + 1 < last;
                float startX = x;

                lines.Add(Profile(Steps(rise), runLength, rise, BottomY(lowY, rise, lowIsLanding, baseY))
                    .Select(p => new Vector2(ascending ? startX + p.x : startX + runLength - p.x, lowY + p.y))
                    .ToList());
                x += runLength;
            }

            return lines;
        }

        public static List<Vector2> SampleProfile(ProBuilderStairsStats stairsStats, int steps, float treadDepth) {
            float stepHeight = Mathf.Max(MinSize, stairsStats.stepHeight);
            return Profile(steps, steps * treadDepth, steps * stepHeight, 0f, Mathf.Max(MinSize, stairsStats.thickness), stairsStats.solid);
        }

        private List<Vector2> Profile(int steps, float runLength, float rise, float bottomY) =>
            Profile(steps, runLength, rise, bottomY, activeThickness, activeSolid);

        private static List<Vector2> Profile(int steps, float runLength, float rise, float bottomY, float thickness, bool solid) {
            List<Vector2> profile = new() { new Vector2(0f, bottomY) };

            if (steps == 0) {
                profile.Add(new Vector2(0f, rise));
                profile.Add(new Vector2(runLength, rise));
                profile.Add(new Vector2(runLength, bottomY));
                return profile;
            }

            float depth = runLength / steps, height = rise / steps;
            for (int i = 0; i < steps; i++) {
                profile.Add(new Vector2(i * depth, (i + 1) * height));
                profile.Add(new Vector2((i + 1) * depth, (i + 1) * height));
            }

            float slopeOffset = thickness * Mathf.Sqrt(1f + height * height / (depth * depth));
            float underStart = (bottomY + slopeOffset) * depth / height;

            if (solid || underStart >= runLength - Epsilon || rise - slopeOffset <= bottomY + Epsilon) {
                profile.Add(new Vector2(runLength, bottomY));
                return profile;
            }

            profile.Add(new Vector2(runLength, rise - thickness));
            if (underStart > Epsilon)
                profile.Add(new Vector2(underStart, bottomY));
            return profile;
        }

        private void Landing(List<Vector3> positions, List<Face> faces, Vector3 center, Vector3 into, Vector3 outOf, float baseY, float halfWidth) {
            if (into == Vector3.zero)
                into = outOf;
            if (outOf == Vector3.zero)
                outOf = into;
            if (into == Vector3.zero)
                return;

            Vector3 rightIn = Vector3.Cross(Vector3.up, into), rightOut = Vector3.Cross(Vector3.up, outOf);
            List<Vector3> corners = new();

            foreach (float side in new[] { -1f, 1f }) {
                Vector3 arrive = center - into * halfWidth + rightIn * (side * halfWidth);
                Vector3 leave = center + outOf * halfWidth + rightOut * (side * halfWidth);
                corners.Add(arrive);
                corners.Add(leave);

                if (Intersect(arrive, into, leave, outOf, out Vector3 corner) &&
                    (corner - center).sqrMagnitude <= MaxLandingCorner * MaxLandingCorner * halfWidth * halfWidth)
                    corners.Add(corner);
            }

            List<Vector2> outline = ConvexHull(corners.Select(c => new Vector2(c.x, c.z)).ToList());
            if (outline.Count < 3)
                return;

            float topY = center.y;
            float bottomY = activeSolid && baseY < topY - Epsilon ? baseY : topY - activeThickness;
            Vector3 Point(Vector2 p, float y) => new(p.x, y, p.y);

            AddFace(positions, faces, Vector3.up, outline.Select(p => Point(p, topY)).ToArray());
            AddFace(positions, faces, Vector3.down, outline.Select(p => Point(p, bottomY)).ToArray());

            for (int i = 0; i < outline.Count; i++) {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                Vector3 facing = new(b.y - a.y, 0f, a.x - b.x);
                AddFace(positions, faces, facing, Point(a, bottomY), Point(b, bottomY), Point(b, topY), Point(a, topY));
            }
        }

        private static bool Intersect(Vector3 p, Vector3 d, Vector3 q, Vector3 e, out Vector3 point) {
            point = p;
            float denominator = d.x * e.z - d.z * e.x;
            if (Mathf.Abs(denominator) < 1e-3f)
                return false;

            Vector3 offset = q - p;
            point = p + d * ((offset.x * e.z - offset.z * e.x) / denominator);
            return true;
        }

        private static List<Vector2> ConvexHull(List<Vector2> points) {
            points = points.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            if (points.Count < 3)
                return points;

            float Turn(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

            List<Vector2> hull = new();
            for (int pass = 0; pass < 2; pass++) {
                int floor = hull.Count;
                foreach (Vector2 p in pass == 0 ? points : Enumerable.Reverse(points)) {
                    while (hull.Count >= floor + 2 && Turn(hull[^2], hull[^1], p) <= Epsilon * Epsilon)
                        hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }

                hull.RemoveAt(hull.Count - 1);
            }

            return hull;
        }

        private static void AddStrip(List<Vector3> positions, List<Face> faces, Vector3 facing, Vector3 aLeft, Vector3 aRight, Vector3 bRight,
            Vector3 bLeft, int smoothing = NoSmoothing) {
            if ((aLeft - aRight).sqrMagnitude < 1e-10f || (bLeft - bRight).sqrMagnitude < 1e-10f ||
                (aLeft - bLeft).sqrMagnitude < 1e-10f || (aRight - bRight).sqrMagnitude < 1e-10f) {
                AddFace(positions, faces, facing, smoothing, aLeft, aRight, bRight, bLeft);
                return;
            }

            Vector3 normal = Vector3.Cross(aRight - aLeft, bRight - aLeft) + Vector3.Cross(bRight - aLeft, bLeft - aLeft);
            if (normal.sqrMagnitude < 1e-12f)
                return;

            int start = positions.Count;
            if (Vector3.Dot(normal, facing) >= 0f)
                positions.AddRange(new[] { aLeft, aRight, bRight, bLeft });
            else
                positions.AddRange(new[] { aLeft, bLeft, bRight, aRight });

            faces.Add(new Face(new[] { start, start + 1, start + 2, start, start + 2, start + 3 }) { smoothingGroup = smoothing });
        }

        private static void AddFace(List<Vector3> positions, List<Face> faces, Vector3 facing, params Vector3[] corners) =>
            AddFace(positions, faces, facing, NoSmoothing, corners);

        private static void AddFace(List<Vector3> positions, List<Face> faces, Vector3 facing, int smoothing, params Vector3[] corners) {
            List<Vector3> unique = new();
            foreach (Vector3 corner in corners)
                if (unique.Count == 0 || (unique[^1] - corner).sqrMagnitude > 1e-10f)
                    unique.Add(corner);
            if (unique.Count > 1 && (unique[0] - unique[^1]).sqrMagnitude <= 1e-10f)
                unique.RemoveAt(unique.Count - 1);
            if (unique.Count < 3)
                return;
            corners = unique.ToArray();

            Vector3 normal = Vector3.zero;
            for (int i = 2; i < corners.Length; i++)
                normal += Vector3.Cross(corners[i - 1] - corners[0], corners[i] - corners[0]);
            if (normal.sqrMagnitude < 1e-12f)
                return;

            if (Vector3.Dot(normal, facing) < 0f)
                Array.Reverse(corners, 1, corners.Length - 1);

            corners = FanFromBestCorner(corners);

            int start = positions.Count;
            positions.AddRange(corners);

            int[] indices = new int[(corners.Length - 2) * 3];
            for (int i = 2; i < corners.Length; i++) {
                indices[(i - 2) * 3] = start;
                indices[(i - 2) * 3 + 1] = start + i - 1;
                indices[(i - 2) * 3 + 2] = start + i;
            }

            faces.Add(new Face(indices) { smoothingGroup = smoothing });
        }

        private static Vector3[] FanFromBestCorner(Vector3[] corners) {
            int count = corners.Length;
            if (count <= 3)
                return corners;

            int apex = 0;
            float best = -1f;
            for (int i = 0; i < count; i++) {
                float score = float.MaxValue;
                for (int k = 0; k < count; k++) {
                    int next = (k + 1) % count;
                    if (k == i || next == i)
                        continue;

                    Vector3 edge = corners[next] - corners[k];
                    float length = edge.magnitude;
                    if (length < 1e-6f)
                        continue;

                    score = Mathf.Min(score, Vector3.Cross(edge / length, corners[i] - corners[k]).magnitude);
                }

                if (score > best) {
                    best = score;
                    apex = i;
                }
            }

            Vector3[] rotated = new Vector3[count];
            for (int i = 0; i < count; i++)
                rotated[i] = corners[(apex + i) % count];
            return rotated;
        }
    }
}
