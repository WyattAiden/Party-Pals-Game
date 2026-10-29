using System;
using UnityEditor;

namespace Tsitski.Core {
    public static class TsitskiInspector {
        public static bool DrawProperties(SerializedObject serializedObject, Func<string, bool> hidden = null) {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            SerializedProperty property = serializedObject.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false) {
                if (property.name == "m_Script") {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);
                    continue;
                }

                if (hidden != null && hidden(property.name))
                    continue;

                EditorGUILayout.PropertyField(property, true);
            }

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            return changed;
        }
    }
}
