using System.Linq;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(ProBuilderCutTarget))]
    [CanEditMultipleObjects]
    public class ProBuilderCutTargetEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            using (new EditorGUI.DisabledScope(true))
                DrawDefaultInspector();

            if (!targets.Cast<ProBuilderCutTarget>().All(target => target.TryGetComponent(out ProBuilderMesh _)))
                return;

            EditorGUILayout.Space();
            if (!GUILayout.Button("Bake to ProBuilder Mesh"))
                return;

            Undo.SetCurrentGroupName("Bake Cuts");
            int group = Undo.GetCurrentGroup();
            foreach (ProBuilderCutTarget target in targets.Cast<ProBuilderCutTarget>().ToArray()) {
                ProBuilderMesh baked = target.Bake();
                if (baked)
                    baked.Optimize();
            }
            Undo.CollapseUndoOperations(group);
            GUIUtility.ExitGUI();
        }
    }
}
