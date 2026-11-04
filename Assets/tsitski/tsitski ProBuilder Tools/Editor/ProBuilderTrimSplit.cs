using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    internal static class ProBuilderTrimSplit {
        private const string MenuPath = "Tools/tsitski ProBuilder Tools/Modelling/Split Into New Trim";
        private const string UndoName = "Split Into New Trim";

        [MenuItem(MenuPath)]
        private static void Split() {
            if (!ProBuilderTrimEditor.TryGetSelectedPathPoints(out ProBuilderTrim trim, out HashSet<int> selected)) {
                Debug.Log("[tsitski ProBuilder Tools] Split Into New Trim needs path points selected on a trim.");
                return;
            }

            if (trim.pathInStats) {
                Debug.LogWarning($"[tsitski ProBuilder Tools] Can't split '{trim.name}': its path is stored in Trim Stats '{trim.stats.name}' and shared with other trims.", trim);
                return;
            }

            List<Vector3> path = new(trim.path);
            List<float> radii = PointRounding.Synced(trim.pathRadii, path.Count);
            List<(List<int> indices, bool chosen)> pieces = Pieces(path.Count, trim.closed && path.Count >= 3, selected);

            if (!pieces.Any(p => p.chosen)) {
                Debug.Log("[tsitski ProBuilder Tools] Split Into New Trim needs at least two neighbouring path points selected.");
                return;
            }

            if (pieces.All(p => p.chosen)) {
                Debug.Log("[tsitski ProBuilder Tools] Split Into New Trim needs part of the path left unselected.");
                return;
            }

            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();

            List<int> keep = pieces.First(p => !p.chosen).indices;
            List<Object> split = new();

            foreach ((List<int> indices, bool chosen) in pieces) {
                if (indices == keep)
                    continue;

                ProBuilderTrim created = CreateTrim(trim, indices.Select(i => path[i]).ToList(), PieceRadii(indices, radii));
                if (chosen)
                    split.Add(created.gameObject);
            }

            Undo.RecordObject(trim, UndoName);
            trim.path = keep.Select(i => path[i]).ToList();
            trim.pathRadii = PieceRadii(keep, radii);
            trim.closed = false;
            trim.Rebuild();
            EditorUtility.SetDirty(trim);

            ProBuilderTrimEditor.ClearSelectedPathPoints();
            Selection.objects = split.ToArray();
            Undo.CollapseUndoOperations(group);
        }

        [MenuItem(MenuPath, true)]
        private static bool SplitValidate() => ProBuilderTrimEditor.TryGetSelectedPathPoints(out _, out HashSet<int> selected) && selected.Count >= 2;

        private static List<(List<int> indices, bool chosen)> Pieces(int count, bool closed, HashSet<int> selected) {
            List<(List<int>, bool)> pieces = new();
            int segments = closed ? count : count - 1;
            if (segments <= 0)
                return pieces;

            bool Chosen(int segment) => selected.Contains(segment % count) && selected.Contains((segment + 1) % count);

            int start = 0;
            if (closed) {
                start = -1;
                for (int k = 0; k < segments; k++)
                    if (Chosen(k) != Chosen((k - 1 + segments) % segments)) {
                        start = k;
                        break;
                    }

                if (start < 0) {
                    pieces.Add((Enumerable.Range(0, count).Append(0).ToList(), Chosen(0)));
                    return pieces;
                }
            }

            List<int> current = null;
            bool currentChosen = false;

            for (int n = 0; n < segments; n++) {
                int k = (start + n) % segments;
                bool chosen = Chosen(k);

                if (current == null || chosen != currentChosen) {
                    current = new List<int> { k % count };
                    currentChosen = chosen;
                    pieces.Add((current, chosen));
                }

                current.Add((k + 1) % count);
            }

            return pieces;
        }

        private static List<float> PieceRadii(List<int> indices, List<float> radii) =>
            indices.Select((i, k) => k == 0 || k == indices.Count - 1 ? 0f : radii[i]).ToList();

        private static ProBuilderTrim CreateTrim(ProBuilderTrim original, List<Vector3> path, List<float> radii) {
            GameObject source = original.gameObject;
            GameObject child = new($"{source.name} Split");
            Undo.RegisterCreatedObjectUndo(child, UndoName);

            Transform transform = child.transform;
            transform.SetParent(source.transform.parent, false);
            transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
            transform.localScale = source.transform.localScale;
            transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

            child.layer = source.layer;
            child.tag = source.tag;
            GameObjectUtility.SetStaticEditorFlags(child, GameObjectUtility.GetStaticEditorFlags(source));

            child.AddComponent<ProBuilderMesh>();
            if (source.TryGetComponent(out MeshRenderer sourceRenderer) && child.TryGetComponent(out MeshRenderer renderer)) {
                EditorUtility.CopySerialized(sourceRenderer, renderer);
                renderer.enabled = true;
            }

            ProBuilderTrim trim = child.AddComponent<ProBuilderTrim>();
            EditorUtility.CopySerialized(original, trim);
            trim.path = path;
            trim.pathRadii = radii;
            trim.closed = false;

            if (source.TryGetComponent(out MeshCollider sourceCollider)) {
                if (!child.TryGetComponent(out MeshCollider collider))
                    collider = child.AddComponent<MeshCollider>();
                collider.sharedMaterial = sourceCollider.sharedMaterial;
                collider.convex = sourceCollider.convex;
                collider.isTrigger = sourceCollider.isTrigger;
                collider.enabled = true;
            }

            trim.Rebuild();
            return trim;
        }
    }
}
