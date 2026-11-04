using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    public static class RealBlendSetup {
        private const string Define = "TSITSKI_REALBLEND";
        private const string StorageTypeName = "RealBlend.VertexPaintStorage";

        private static Type _storageType;

        static RealBlendSetup() {
            _storageType = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(t => t.FullName == StorageTypeName);
            EditorApplication.delayCall += UpdateDefine;
        }

        private static void UpdateDefine() {
            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            List<string> defines = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

            bool installed = _storageType != null;
            if (defines.Contains(Define) == installed)
                return;

            if (installed)
                defines.Add(Define);
            else
                defines.Remove(Define);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
        }

#if TSITSKI_REALBLEND
        [InitializeOnLoadMethod]
        private static void Subscribe() => ProBuilderCutTarget.ResultRebuilt += OnResultRebuilt;

        private static MethodInfo setBaseTopology => _storageType?.GetMethod("SetBaseTopology", new[] { typeof(Mesh) });

        [MenuItem("Tools/tsitski ProBuilder Tools/Setup Real Blend")]
        private static void Setup() {
            if (setBaseTopology == null) {
                Debug.LogWarning("Real Blend's VertexPaintStorage wasn't found.");
                return;
            }

            Undo.SetCurrentGroupName("Setup Real Blend");
            int group = Undo.GetCurrentGroup();
            int added = 0;

            foreach (ProBuilderMesh mesh in Selection.gameObjects
                         .SelectMany(root => root.GetComponentsInChildren<ProBuilderMesh>(true)).Distinct()) {
                if (!mesh.TryGetComponent(out MeshFilter _) || mesh.GetComponent(_storageType))
                    continue;

                Undo.RegisterCompleteObjectUndo(mesh, "Setup Real Blend");
                Component storage = Undo.AddComponent(mesh.gameObject, _storageType);
                Fill(mesh, storage, true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(storage);
                added++;
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log($"Real Blend set up on {added} ProBuilder mesh{(added == 1 ? "" : "es")}.");
        }

        private static void OnResultRebuilt(ProBuilderMesh result) {
            if (result && _storageType != null && result.GetComponent(_storageType) is Component storage)
                Fill(result, storage, result.colors == null || result.colors.Count != result.vertexCount);
        }

        private static void Fill(ProBuilderMesh mesh, Component storage, bool clearColors) {
            if (clearColors) {
                mesh.colors = Enumerable.Repeat(Color.clear, mesh.vertexCount).ToArray();
                mesh.ToMesh();
                mesh.Refresh();
            }

            setBaseTopology?.Invoke(storage, new object[] { mesh.GetComponent<MeshFilter>().sharedMesh });
            EditorUtility.SetDirty(storage);
        }

        [MenuItem("Tools/tsitski ProBuilder Tools/Setup Real Blend", true)]
        private static bool CanSetup() => _storageType != null && Selection.gameObjects.Length > 0;
#endif
    }
}
