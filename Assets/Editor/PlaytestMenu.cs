using System.IO;
using UnityEditor;

public static class PlaytestMenu
{
    [MenuItem("Party Pals/Open Playtest Logs Folder")]
    static void OpenFolder()
    {
        string path = PlaytestLogger.RootFolder;
        Directory.CreateDirectory(path);
        EditorUtility.OpenWithDefaultApp(path);
    }
}
