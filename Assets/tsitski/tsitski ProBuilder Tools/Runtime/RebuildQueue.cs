using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    public static class RebuildQueue {
        private static readonly HashSet<Object> Queued = new();
        private static readonly Dictionary<Object, Action> Deferred = new();

        public static bool HandleHeld { get; private set; }
        public static bool Deferring => HandleHeld && TsitskiProBuilderSettings.RebuildAfterDragging;
        public static event Action Released;

        public static void Queue(Object owner, Action rebuild) {
#if UNITY_EDITOR
            if (!Queued.Add(owner))
                return;

            EditorApplication.delayCall += () => {
                Queued.Remove(owner);
                if (owner)
                    Run(owner, rebuild);
            };
#endif
        }

        public static void Run(Object owner, Action rebuild) {
            if (Deferring) {
                Deferred[owner] = rebuild;
                return;
            }

            Deferred.Remove(owner);
            rebuild();
        }

        public static void SetHandleHeld(bool held) {
            if (held) {
                HandleHeld = true;
                return;
            }

            if (!HandleHeld)
                return;

            HandleHeld = false;
            List<KeyValuePair<Object, Action>> deferred = new(Deferred);
            Deferred.Clear();
            foreach ((Object owner, Action rebuild) in deferred)
                if (owner)
                    rebuild();
            Released?.Invoke();
        }
    }
}
