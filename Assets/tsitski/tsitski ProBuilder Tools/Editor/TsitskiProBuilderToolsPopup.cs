using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    public class TsitskiProBuilderToolsPopup : EditorWindow {
        const string MenuRoot = "Tools/tsitski ProBuilder Tools/";
        const float MinWidth = 220f;
        const float ButtonHeight = 24f;
        const float Padding = 4f;

        class Tool {
            public string path;
            public string label;
            public string group;
            public string shortcut;
            public MethodInfo validate;
        }

        List<Tool> _tools = new();
        SceneView _origin;
        bool _closing;

        static Vector2 s_LastMouseScreenPosition;

        static TsitskiProBuilderToolsPopup() {
            SceneView.duringSceneGui += OnSceneGui;
        }

        static void OnSceneGui(SceneView sceneView) {
            Event e = Event.current;
            if (e.isMouse || e.type == EventType.Repaint)
                s_LastMouseScreenPosition = GUIUtility.GUIToScreenPoint(e.mousePosition);
        }

        [Shortcut("tsitski ProBuilder Tools/Tools Popup", typeof(SceneView), KeyCode.X, ShortcutModifiers.Alt)]
        static void OpenFromShortcut() => Open(s_LastMouseScreenPosition);

        static void Open(Vector2 screenPosition) {
            foreach (TsitskiProBuilderToolsPopup existing in Resources.FindObjectsOfTypeAll<TsitskiProBuilderToolsPopup>())
                existing.SafeClose();

            List<Tool> tools = FindTools();
            if (tools.Count == 0)
                return;

            TsitskiProBuilderToolsPopup popup = CreateInstance<TsitskiProBuilderToolsPopup>();
            popup._tools = tools;
            popup._origin = SceneView.lastActiveSceneView;

            int headers = tools.Select(t => t.group).Distinct().Count(g => g.Length > 0);
            bool hasLoose = tools.Any(t => t.group.Length == 0);
            float height = Padding * 2 + EditorGUIUtility.singleLineHeight + tools.Count * (ButtonHeight + 2f)
                           + headers * (EditorGUIUtility.singleLineHeight + 2f) + (hasLoose && headers > 0 ? 6f : 0f);
            float width = MinWidth;
            foreach (Tool tool in tools)
                width = Mathf.Max(width, EditorStyles.label.CalcSize(new GUIContent(ButtonText(tool))).x + Padding * 2 + 32f);

            popup.ShowAsDropDown(new Rect(screenPosition, Vector2.zero), new Vector2(width, height));
        }

        static List<Tool> FindTools() {
            List<Tool> tools = new();
            Dictionary<string, MethodInfo> validators = new();

            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<MenuItem>()) {
                foreach (MenuItem item in method.GetCustomAttributes<MenuItem>()) {
                    if (!item.menuItem.StartsWith(MenuRoot))
                        continue;

                    (string path, string shortcut) = SplitShortcut(item.menuItem);

                    if (item.validate)
                        validators[path] = method;
                    else {
                        string relative = path.Substring(MenuRoot.Length);
                        int slash = relative.LastIndexOf('/');
                        tools.Add(new Tool {
                            path = path,
                            group = slash < 0 ? "" : relative.Substring(0, slash),
                            label = slash < 0 ? relative : relative.Substring(slash + 1),
                            shortcut = shortcut
                        });
                    }
                }
            }

            foreach (Tool tool in tools)
                validators.TryGetValue(tool.path, out tool.validate);

            return tools.OrderBy(t => t.group.Length == 0).ThenBy(t => t.group).ThenBy(t => t.label).ToList();
        }

        static (string path, string shortcut) SplitShortcut(string menuItem) {
            int space = menuItem.LastIndexOf(' ');
            if (space < 0)
                return (menuItem, "");

            string last = menuItem.Substring(space + 1);
            if (last.Length < 2 || "%#&_".IndexOf(last[0]) < 0)
                return (menuItem, "");

            string shortcut = last
                .Replace("%", "Ctrl+").Replace("#", "Shift+").Replace("&", "Alt+").TrimStart('_')
                .Replace("DEL", "Delete");
            return (menuItem.Substring(0, space), shortcut);
        }

        static bool IsEnabled(Tool tool) {
            if (tool.validate == null)
                return true;

            try {
                return tool.validate.Invoke(null, null) is true;
            }
            catch {
                return false;
            }
        }

        void OnGUI() {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) {
                SafeClose(true);
                return;
            }

            GUILayout.BeginArea(new Rect(Padding, Padding, position.width - Padding * 2, position.height - Padding * 2));
            GUILayout.Label("tsitski ProBuilder Tools", EditorStyles.boldLabel);

            string group = null;
            foreach (Tool tool in _tools) {
                if (tool.group != group) {
                    if (tool.group.Length > 0)
                        GUILayout.Label(tool.group, EditorStyles.miniBoldLabel);
                    else if (group != null)
                        GUILayout.Space(6f);
                    group = tool.group;
                }

                using (new EditorGUI.DisabledScope(!IsEnabled(tool))) {
                    if (GUILayout.Button(ButtonText(tool), GUILayout.Height(ButtonHeight))) {
                        string path = tool.path;
                        SafeClose(true);
                        EditorApplication.delayCall += () => EditorApplication.ExecuteMenuItem(path);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            FitHeight(this, GUILayoutUtility.GetLastRect().yMax + Padding * 2f);
            GUILayout.EndArea();
        }

        internal static void FitHeight(EditorWindow window, float height) {
            if (Event.current.type != EventType.Repaint || Mathf.Abs(window.position.height - height) < 1f)
                return;

            Rect rect = window.position;
            rect.height = height;
            window.minSize = new Vector2(rect.width, height);
            window.maxSize = new Vector2(rect.width, height);
            window.position = rect;
        }

        static string ButtonText(Tool tool) =>
            string.IsNullOrEmpty(tool.shortcut) ? tool.label : $"{tool.label}   ({tool.shortcut})";

        void SafeClose(bool returnFocus = false) {
            if (_closing)
                return;

            _closing = true;
            SceneView origin = _origin;
            Close();

            if (!returnFocus)
                return;

            EditorApplication.delayCall += () => {
                if (!origin)
                    return;

                origin.Focus();
                foreach (KeyCode key in new[] { KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.X })
                    origin.SendEvent(new Event { type = EventType.KeyUp, keyCode = key });
                origin.Repaint();
            };
        }
    }
}
