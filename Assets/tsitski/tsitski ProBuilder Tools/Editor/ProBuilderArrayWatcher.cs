using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    internal static class ProBuilderArrayWatcher {
        static ProBuilderArrayWatcher() {
            ProBuilderEditor.afterMeshModification += SourcesChanged;
            EditorMeshUtility.meshOptimized += (mesh, _) => SourcesChanged(new[] { mesh });
            Selection.selectionChanged += UpdateVisibility;
            EditorApplication.hierarchyChanged += UpdateVisibility;
            EditorApplication.update += Update;
        }

        private static void SourcesChanged(IEnumerable<ProBuilderMesh> meshes) {
            HashSet<ProBuilderMesh> changed = new(meshes.Where(m => m));
            foreach (ProBuilderArray array in ProBuilderArray.All.ToArray()) {
                if (!array)
                    continue;

                if (StoresLocalSource(array) && changed.Contains(array.source))
                    StoreSource(array);
                else if (changed.Contains(array.activeSource))
                    array.QueueRebuild();
            }
        }

        private static bool StoresLocalSource(ProBuilderArray array) =>
            array.stats && array.stats.storeSource && array.source && array.source.gameObject.scene.IsValid();

        internal static void StoreSource(ProBuilderArray array) {
            Undo.RecordObject(array.stats, "Store Array Source");
            array.stats.sourceMesh = ArraySourceData.Capture(array.source);
            UnityEditor.EditorUtility.SetDirty(array.stats);
            array.stats.NotifyChanged();
        }

        private static void Update() {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            foreach (ProBuilderArray array in ProBuilderArray.All.ToArray()) {
                if (!array)
                    continue;

                bool storing = StoresLocalSource(array);
                ProBuilderMesh source = storing ? array.source : array.activeSource;
                if (!source || !source.gameObject.scene.IsValid() || !source.transform.hasChanged)
                    continue;

                source.transform.hasChanged = false;
                if (storing)
                    StoreSource(array);
                else
                    array.QueueRebuild();
            }
        }

        private static void UpdateVisibility() {
            foreach (ProBuilderArray array in ProBuilderArray.All.ToArray()) {
                ProBuilderMesh source = array ? array.source : null;
                if (!source || !source.gameObject.scene.IsValid() || !source.TryGetComponent(out MeshRenderer renderer))
                    continue;

                bool visible = Selection.gameObjects.Contains(source.gameObject);
                if (renderer.enabled != visible)
                    renderer.enabled = visible;
            }
        }
    }
}
