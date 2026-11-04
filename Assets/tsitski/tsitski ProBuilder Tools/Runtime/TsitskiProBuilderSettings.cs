using System.IO;
using UnityEngine;
using UnityEngine.ProBuilder;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    public class TsitskiProBuilderSettings : ScriptableObject {
        private const string AssetName = "tsitski ProBuilder Tools Settings.asset";

        [Tooltip(Tooltips.DefaultMeshMaterial)] public Material defaultMeshMaterial;
        [Tooltip(Tooltips.RebuildAfterDragging)] public bool rebuildAfterDragging = true;
        [Tooltip(Tooltips.AutoNameGenerators)] public bool autoNameGenerators;
        [Tooltip(Tooltips.GizmosOnlyOnSelected)] public bool gizmosOnlyOnSelected;
        [Tooltip(Tooltips.ContributeGIByDefault)] public bool contributeGIByDefault = true;

        private static TsitskiProBuilderSettings _instance;

        public static Material DefaultMaterial {
            get {
                TsitskiProBuilderSettings settings = Instance;
                return settings && settings.defaultMeshMaterial ? settings.defaultMeshMaterial : BuiltinMaterials.defaultMaterial;
            }
        }

        public static bool RebuildAfterDragging {
            get {
                TsitskiProBuilderSettings settings = Instance;
                return !settings || settings.rebuildAfterDragging;
            }
        }

        public static bool GizmosOnlyOnSelected {
            get {
                TsitskiProBuilderSettings settings = Instance;
                return settings && settings.gizmosOnlyOnSelected;
            }
        }

        public static bool ContributeGIByDefault {
            get {
                TsitskiProBuilderSettings settings = Instance;
                return !settings || settings.contributeGIByDefault;
            }
        }

        public static void ApplyDefaultStaticFlags(GameObject gameObject) {
#if UNITY_EDITOR
            if (!gameObject || !ContributeGIByDefault)
                return;

            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(gameObject);
            if ((flags & StaticEditorFlags.ContributeGI) != 0)
                return;

            Undo.RecordObject(gameObject, "Contribute GI");
            GameObjectUtility.SetStaticEditorFlags(gameObject, flags | StaticEditorFlags.ContributeGI);
#endif
        }

        public static void AutoName(Component generator, Object stats, string defaultName) {
            TsitskiProBuilderSettings settings = Instance;
            if (!generator || !settings || !settings.autoNameGenerators)
                return;

            string generatorName = stats ? stats.name : defaultName;
            if (generator.gameObject.name == generatorName)
                return;

            generator.gameObject.name = generatorName;
#if UNITY_EDITOR
            EditorUtility.SetDirty(generator.gameObject);
#endif
        }

        public static TsitskiProBuilderSettings Instance {
            get {
#if UNITY_EDITOR
                if (!_instance)
                    _instance = LoadOrCreate();
#endif
                return _instance;
            }
        }

#if UNITY_EDITOR
        private static TsitskiProBuilderSettings LoadOrCreate() {
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(TsitskiProBuilderSettings)}")) {
                TsitskiProBuilderSettings found = AssetDatabase.LoadAssetAtPath<TsitskiProBuilderSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (found)
                    return found;
            }

            TsitskiProBuilderSettings created = CreateInstance<TsitskiProBuilderSettings>();
            created.defaultMeshMaterial = BuiltinMaterials.defaultMaterial;

            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(created));
            string folder = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath))?.Replace('\\', '/');
            AssetDatabase.CreateAsset(created, $"{(string.IsNullOrEmpty(folder) ? "Assets" : folder)}/{AssetName}");
            AssetDatabase.SaveAssets();
            return created;
        }
#endif
    }
}
