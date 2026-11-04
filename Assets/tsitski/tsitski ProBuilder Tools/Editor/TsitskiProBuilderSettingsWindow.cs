using UnityEditor;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    public class TsitskiProBuilderSettingsWindow : EditorWindow {
        private UnityEditor.Editor _editor;

        [MenuItem("Tools/tsitski ProBuilder Tools/Settings")]
        private static void Open() {
            TsitskiProBuilderSettingsWindow window = GetWindow<TsitskiProBuilderSettingsWindow>(true, "tsitski ProBuilder Tools Settings");
            window.minSize = new Vector2(320f, 120f);
            window.Show();
        }

        private void OnDisable() {
            if (_editor)
                DestroyImmediate(_editor);
        }

        private void OnGUI() {
            TsitskiProBuilderSettings settings = TsitskiProBuilderSettings.Instance;
            if (!settings)
                return;

            UnityEditor.Editor.CreateCachedEditor(settings, null, ref _editor);
            _editor.serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_editor.serializedObject.FindProperty(nameof(TsitskiProBuilderSettings.defaultMeshMaterial)));
            EditorGUILayout.PropertyField(_editor.serializedObject.FindProperty(nameof(TsitskiProBuilderSettings.rebuildAfterDragging)));
            EditorGUILayout.PropertyField(_editor.serializedObject.FindProperty(nameof(TsitskiProBuilderSettings.autoNameGenerators)));
            EditorGUILayout.PropertyField(_editor.serializedObject.FindProperty(nameof(TsitskiProBuilderSettings.gizmosOnlyOnSelected)));
            EditorGUILayout.PropertyField(_editor.serializedObject.FindProperty(nameof(TsitskiProBuilderSettings.contributeGIByDefault)));

            bool wasAutoNaming = settings.autoNameGenerators;
            if (!_editor.serializedObject.ApplyModifiedProperties())
                return;

            AssetDatabase.SaveAssetIfDirty(settings);
            if (!wasAutoNaming && settings.autoNameGenerators)
                NameAllGenerators();
        }

        private static void NameAllGenerators() {
            foreach (ProBuilderCutter cutter in UnityCompat.FindAll<ProBuilderCutter>())
                TsitskiProBuilderSettings.AutoName(cutter, cutter.stats, "ProBuilder Cutter");
            foreach (ProBuilderTrim trim in UnityCompat.FindAll<ProBuilderTrim>())
                TsitskiProBuilderSettings.AutoName(trim, trim.stats, "ProBuilder Trim");
            foreach (ProBuilderArray array in UnityCompat.FindAll<ProBuilderArray>())
                TsitskiProBuilderSettings.AutoName(array, array.stats, "ProBuilder Array");
            foreach (ProBuilderStairs stairs in UnityCompat.FindAll<ProBuilderStairs>())
                TsitskiProBuilderSettings.AutoName(stairs, stairs.stats, "ProBuilder Stairs");
            foreach (ProBuilderPillar pillar in UnityCompat.FindAll<ProBuilderPillar>())
                TsitskiProBuilderSettings.AutoName(pillar, pillar.stats, "ProBuilder Pillar");
            foreach (ProBuilderFloorPlan plan in UnityCompat.FindAll<ProBuilderFloorPlan>())
                TsitskiProBuilderSettings.AutoName(plan, null, "ProBuilder Floor Plan");
            foreach (ProBuilderRoof roof in UnityCompat.FindAll<ProBuilderRoof>())
                TsitskiProBuilderSettings.AutoName(roof, roof.stats, "ProBuilder Roof");
        }
    }
}
