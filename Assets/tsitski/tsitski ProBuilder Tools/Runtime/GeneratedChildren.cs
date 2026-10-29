using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    internal interface IGenerated {
        MonoBehaviour generator { get; set; }
    }

    internal static class GeneratedChildren {
        public static bool Resize<T>(MonoBehaviour owner, List<T> items, int wanted, string name) where T : MonoBehaviour, IGenerated {
            bool changed = items.RemoveAll(item => !item) > 0;
            foreach (Transform child in owner.transform)
                if (child.TryGetComponent(out T stray) && stray.generator == owner && !items.Contains(stray)) {
                    items.Add(stray);
                    changed = true;
                }

            while (items.Count > wanted) {
                T extra = items[^1];
                items.RemoveAt(items.Count - 1);
                if (Application.isPlaying)
                    Object.Destroy(extra.gameObject);
                else
                    Object.DestroyImmediate(extra.gameObject);
                changed = true;
            }

            while (items.Count < wanted) {
                GameObject created = new(name) { layer = owner.gameObject.layer };
                created.transform.SetParent(owner.transform, false);
                TsitskiProBuilderSettings.ApplyDefaultStaticFlags(created);

                ProBuilderArray.suppressReset = true;
                T item;
                try {
                    item = created.AddComponent<T>();
                }
                finally {
                    ProBuilderArray.suppressReset = false;
                }

                item.generator = owner;
                if (!created.TryGetComponent(out MeshCollider _))
                    created.AddComponent<MeshCollider>();
                items.Add(item);
                changed = true;
            }

#if UNITY_EDITOR
            if (changed)
                EditorUtility.SetDirty(owner);
#endif
            return changed;
        }

        public static void Place(MonoBehaviour owner, Component item, Vector3 position, Quaternion rotation) {
            Transform itemTransform = item.transform;
            if (itemTransform.parent != owner.transform)
                itemTransform.SetParent(owner.transform, false);
            itemTransform.localPosition = position;
            itemTransform.localRotation = rotation;
            itemTransform.localScale = Vector3.one;
        }

        public static void Finish(ProBuilderPillar pillar) {
            pillar.Rebuild();
            MarkDirty(pillar);
        }

        public static void MarkDirty(Component item) {
#if UNITY_EDITOR
            EditorUtility.SetDirty(item);
            EditorUtility.SetDirty(item.transform);
#endif
        }
    }
}
