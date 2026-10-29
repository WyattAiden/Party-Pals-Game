using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {
    public static class PointClipboard {
        private const string Prefix2D = "tsitski-points-2d:";
        private const string Prefix3D = "tsitski-points-3d:";

        public static bool has2D => (EditorGUIUtility.systemCopyBuffer ?? "").StartsWith(Prefix2D);
        public static bool has3D => (EditorGUIUtility.systemCopyBuffer ?? "").StartsWith(Prefix3D);

        public static void Copy(IEnumerable<Vector2> points) =>
            EditorGUIUtility.systemCopyBuffer = Prefix2D + string.Join(";", points.Select(p => Format(p.x, p.y)));

        public static void Copy(IEnumerable<Vector3> points) =>
            EditorGUIUtility.systemCopyBuffer = Prefix3D + string.Join(";", points.Select(p => Format(p.x, p.y, p.z)));

        public static List<Vector2> Paste2D() =>
            Parse(Prefix2D, 2).Select(v => new Vector2(v[0], v[1])).ToList();

        public static List<Vector3> Paste3D() =>
            Parse(Prefix3D, 3).Select(v => new Vector3(v[0], v[1], v[2])).ToList();

        public static bool IsCopy(Event e) => e.commandName == "Copy";

        public static bool IsPaste(Event e) => e.commandName == "Paste";

        private static string Format(params float[] values) =>
            string.Join(",", values.Select(v => v.ToString("R", CultureInfo.InvariantCulture)));

        private static IEnumerable<float[]> Parse(string prefix, int components) {
            string buffer = EditorGUIUtility.systemCopyBuffer ?? "";
            if (!buffer.StartsWith(prefix))
                yield break;

            foreach (string entry in buffer[prefix.Length..].Split(';')) {
                string[] parts = entry.Split(',');
                if (parts.Length != components)
                    continue;

                float[] values = new float[components];
                bool valid = true;
                for (int i = 0; i < components && valid; i++)
                    valid = float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]);

                if (valid)
                    yield return values;
            }
        }
    }
}
