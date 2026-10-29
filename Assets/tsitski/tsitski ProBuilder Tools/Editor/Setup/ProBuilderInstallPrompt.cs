using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Tsitski.ProBuilderTools {
    [InitializeOnLoad]
    internal static class ProBuilderInstallPrompt {
        private const string PromptedKey = "Tsitski.ProBuilderTools.ProBuilderPrompted";
        private static AddRequest _request;

        static ProBuilderInstallPrompt() {
            if (Application.isBatchMode || SessionState.GetBool(PromptedKey, false))
                return;

            SessionState.SetBool(PromptedKey, true);
            EditorApplication.delayCall += Prompt;
        }

        private static void Prompt() {
            if (EditorUtility.DisplayDialog("tsitski ProBuilder Tools",
                    "tsitski ProBuilder Tools needs ProBuilder 5.0.3 or newer. Install the ProBuilder package now?",
                    "Install", "Later"))
                Install();
        }

        [MenuItem("Tools/tsitski ProBuilder Tools/Install ProBuilder")]
        private static void Install() {
            if (_request != null && !_request.IsCompleted)
                return;

            _request = Client.Add("com.unity.probuilder");
            EditorApplication.update += WaitForInstall;
        }

        private static void WaitForInstall() {
            if (!_request.IsCompleted)
                return;

            EditorApplication.update -= WaitForInstall;
            if (_request.Status == StatusCode.Failure)
                Debug.LogError($"tsitski ProBuilder Tools: installing ProBuilder failed: {_request.Error.message}");
        }
    }
}
