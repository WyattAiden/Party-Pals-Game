using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    [AddComponentMenu("tsitski ProBuilder Tools/ProBuilder Polygon")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProBuilderMesh))]
    public class ProBuilderPolygon : MonoBehaviour, IPathOwner {
        private const float Epsilon = 1e-4f;
        private const int RoundingSegments = 8;

        [Tooltip(Tooltips.PolygonStats)] public ProBuilderPolygonStats stats;
        [Tooltip(Tooltips.PolygonThickness)] [Min(0.01f)] public float thickness = 0.25f;
        [Tooltip(Tooltips.PolygonPath)] public List<Vector3> path = DefaultPath();
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();
        [Tooltip(Tooltips.PolygonHoles)] public List<PolygonHole> holes = new();

        [NonSerialized] public int skippedHoles;

        public static readonly HashSet<ProBuilderPolygon> All = new();
        public static event Action<ProBuilderMesh> Rebuilt;

        public static List<Vector3> DefaultPath() => new() { Vector3.zero, new(4f, 0f, 0f), new(4f, 0f, 4f), new(0f, 0f, 4f) };

        public bool pathInStats => stats && stats.savePolygon;
        public float activeThickness => Mathf.Max(0.01f, stats ? stats.thickness : thickness);

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

        public List<PolygonHole> activeHoles {
            get => (pathInStats ? stats.holes : holes) ?? new List<PolygonHole>();
            set {
                if (pathInStats)
                    stats.holes = value;
                else
                    holes = value;
            }
        }

        List<Vector3> IPathOwner.localPath {
            get => path;
            set {
                Vector3 delta = path is { Count: > 0 } && value is { Count: > 0 } ? value[0] - path[0] : Vector3.zero;
                path = value;
                foreach (PolygonHole hole in holes.Where(h => h?.path != null))
                    hole.path = hole.path.Select(p => p + delta).ToList();
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
            ProBuilderPolygonStats.Changed += OnStatsChanged;
            QueueRebuild();
        }

        private void OnDisable() {
            All.Remove(this);
            ProBuilderPolygonStats.Changed -= OnStatsChanged;
        }

        private void OnValidate() => QueueRebuild();

        private void OnStatsChanged(ProBuilderPolygonStats changed) {
            if (changed == stats)
                QueueRebuild();
        }

        public void QueueRebuild() => RebuildQueue.Queue(this, Rebuild);

        public void Rebuild() {
            TsitskiProBuilderSettings.AutoName(this, stats, "ProBuilder Polygon");

            if (!TryGetComponent(out ProBuilderMesh mesh))
                return;

            List<Vector2> outer = Loop(activePath, activePathRadii);
            float y = activePath is { Count: > 0 } ? activePath.Average(p => p.y) : 0f;
            skippedHoles = 0;

            List<Vector3> positions = new();
            List<Face> faces = new();
            if (outer.Count >= 3) {
                List<List<D2>> cutters = HoleLoops().Where(h => h.Count >= 3).Select(h => h.Select(D2.From).ToList()).ToList();
                foreach ((List<D2> outline, List<List<D2>> holes) in PolygonBoolean.Subtract(outer.Select(D2.From).ToList(), cutters)) {
                    IList<IList<Vector3>> holePoints = holes.Select(h => (IList<Vector3>)ToPoints(h, y)).ToList();
                    ActionResult result = mesh.CreateShapeFromPolygon(ToPoints(outline, y), activeThickness, false, holePoints.Count > 0 ? holePoints : null);
                    if (result.status != ActionResult.Status.Success) {
                        skippedHoles++;
                        continue;
                    }

                    int offset = positions.Count;
                    positions.AddRange(mesh.positions);
                    faces.AddRange(mesh.faces.Select(f => new Face(f.indexes.Select(i => i + offset).ToArray()) {
                        smoothingGroup = f.smoothingGroup, submeshIndex = f.submeshIndex
                    }));
                }
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

        public static List<Vector2> Loop(List<Vector3> points, List<float> radii) {
            List<Vector3> rounded = PathUtility.Rounded(points, radii, true, RoundingSegments, out bool loop);
            if (!loop)
                return new List<Vector2>();

            List<Vector2> outline = ProBuilderFloorPlan.Clean(rounded.Select(p => new Vector2(p.x, p.z)).ToList());
            return outline.Count >= 3 && Mathf.Abs(CutterOutline.SignedArea(outline)) > Epsilon * Epsilon ? outline : new List<Vector2>();
        }

        public List<List<Vector2>> HoleLoops() =>
            activeHoles.Select(h => h == null ? new List<Vector2>() : Loop(h.path, h.pathRadii)).ToList();

        private static List<Vector3> ToPoints(List<D2> loop, float y) => loop.Select(p => new Vector3((float)p.x, y, (float)p.y)).ToList();
    }
}
