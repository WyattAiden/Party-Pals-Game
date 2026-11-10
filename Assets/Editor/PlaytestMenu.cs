using System.IO;
using UnityEditor;

public static class PlaytestMenu
{
    const string ToggleItem = "Party Pals/Playtest Logging";

    // Checkbox menu item. Works in and out of Play Mode; the choice is saved.
    [MenuItem(ToggleItem, false, 0)]
    static void ToggleLogging() => PlaytestLogger.Enabled = !PlaytestLogger.Enabled;

    [MenuItem(ToggleItem, true)]
    static bool ToggleLoggingValidate()
    {
        Menu.SetChecked(ToggleItem, PlaytestLogger.Enabled);
        return true;
    }

    [MenuItem("Party Pals/Open Playtest Logs Folder")]
    static void OpenFolder()
    {
        string path = PlaytestLogger.RootFolder;
        Directory.CreateDirectory(path);
        EditorUtility.OpenWithDefaultApp(path);
    }
}
