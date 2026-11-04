using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Tsitski.ProBuilderTools {
    [CustomEditor(typeof(ProBuilderTrimStats))]
    public class ProBuilderTrimStatsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            if (!serializedObject.isEditingMultipleObjects) {
                ProBuilderTrimStats stats = (ProBuilderTrimStats)target;
                ProfilePreview.Draw(ProBuilderTrim.BuildProfile(stats.profile, stats.profileRadii, stats.profileMirror, stats.roundingSegments),
                    true, stats.profile, edited => ProBuilderTrimEditor.ApplySnap(stats, edited));
            }
        }
    }
}
