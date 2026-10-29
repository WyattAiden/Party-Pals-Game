using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tsitski.ProBuilderTools {
    [Serializable]
    public class PolygonHole {
        [Tooltip(Tooltips.PolygonHolePath)] public List<Vector3> path = new();
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();

        public PolygonHole Copy() => new() { path = new List<Vector3>(path), pathRadii = new List<float>(pathRadii) };
    }

    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Polygon Stats", fileName = "Polygon Stats")]
    public class ProBuilderPolygonStats : ScriptableObject {
        [Tooltip(Tooltips.PolygonThickness)] [Min(0.01f)] public float thickness = 0.25f;
        [Tooltip(Tooltips.SavePolygon)] public bool savePolygon;
        [Tooltip(Tooltips.PolygonPath)] public List<Vector3> path = ProBuilderPolygon.DefaultPath();
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();
        [Tooltip(Tooltips.PolygonHoles)] public List<PolygonHole> holes = new();

        public static event Action<ProBuilderPolygonStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnValidate() => Changed?.Invoke(this);
    }
}
