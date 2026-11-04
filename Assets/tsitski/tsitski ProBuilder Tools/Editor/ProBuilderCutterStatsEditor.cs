using UnityEditor;
using UnityEngine;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(ProBuilderCutterStats))]
    [CanEditMultipleObjects]
    public class ProBuilderCutterStatsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();
            ProBuilderCutterEditor.DrawProperties(serializedObject, nameof(ProBuilderCutterStats.center));
            serializedObject.ApplyModifiedProperties();

            SerializedProperty shape = serializedObject.FindProperty(nameof(ProBuilderCutterStats.shape));
            bool explosion = !shape.hasMultipleDifferentValues && shape.intValue == (int)ProBuilderCutterShape.Explosion;

            if (explosion && GUILayout.Button("Randomize Explosion")) {
                foreach (Object t in targets) {
                    ProBuilderCutterStats stats = (ProBuilderCutterStats)t;
                    Undo.RecordObject(stats, "Randomize Explosion");
                    stats.explosionSeed = Random.Range(int.MinValue, int.MaxValue);
                    EditorUtility.SetDirty(stats);
                    stats.NotifyChanged();
                }
            }

            if (serializedObject.isEditingMultipleObjects)
                return;

            ProBuilderCutterStats target = (ProBuilderCutterStats)this.target;
            ProBuilderCutterEditor.DrawProfile(CutterShapeSettings.From(target), target.pathPoints, target.pathRadii);
        }
    }
}
