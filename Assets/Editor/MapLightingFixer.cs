using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Generates lighting data (ambient probe + skybox reflections) for every scene in Build Settings.
// Without this data, scenes loaded at runtime in Unity 6 render dark/flat.
public static class MapLightingFixer
{
    const string SettingsPath = "Assets/Scenes/MapLighting.lighting";

    [MenuItem("Tools/Party Pals/Fix Map Lighting")]
    public static void FixAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var previousSetup = EditorSceneManager.GetSceneManagerSetup();
        var settings = GetOrCreateSettings();
        var scenes = EditorBuildSettings.scenes;
        int done = 0;

        try
        {
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled) continue;
                string path = scenes[i].path;
                EditorUtility.DisplayProgressBar("Fixing map lighting", path, (float)i / scenes.Length);

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Lightmapping.lightingSettings = settings;
                if (Lightmapping.Bake())
                {
                    EditorSceneManager.SaveScene(scene);
                    done++;
                }
                else
                {
                    Debug.LogWarning($"[MapLightingFixer] Lighting bake failed for {path}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (previousSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }

        Debug.Log($"[MapLightingFixer] Generated lighting for {done} scene(s).");
    }

    static LightingSettings GetOrCreateSettings()
    {
        var s = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (s != null) return s;

        s = new LightingSettings
        {
            // Your lights are realtime, so only environment lighting needs generating. Fast bake.
            bakedGI = false,
            realtimeGI = false
        };
        AssetDatabase.CreateAsset(s, SettingsPath);
        AssetDatabase.SaveAssets();
        return s;
    }
}
