using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tsitski.ProBuilderTools {
    [Serializable]
    public class RoofPath {
        [Tooltip(Tooltips.RoofPathWidth)] [Min(0.01f)] public float width = 4f;
        [Tooltip(Tooltips.RoofPathHeight)] [Min(0.01f)] public float height = 1.5f;
        [Tooltip(Tooltips.Closed)] public bool closed;
        [Tooltip(Tooltips.RoofGableStart)] public bool gableStart;
        [Tooltip(Tooltips.RoofGableEnd)] public bool gableEnd;
        [Tooltip(Tooltips.RoofPathPoints)] public List<Vector3> path = new();
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();

        public RoofPath Copy() => new() {
            width = width, height = height, closed = closed, gableStart = gableStart, gableEnd = gableEnd,
            path = new List<Vector3>(path), pathRadii = new List<float>(pathRadii)
        };
    }

    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Roof Stats", fileName = "Roof Stats")]
    public class ProBuilderRoofStats : ScriptableObject {
        [Tooltip(Tooltips.RoofProfile)] public List<Vector2> profile = ProBuilderRoof.DefaultProfile();
        [Tooltip(Tooltips.Radii)] public List<float> profileRadii = new();
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.RoofThickness)] [Min(0.01f)] public float thickness = 0.2f;
        [Tooltip(Tooltips.SaveRoofPaths)] public bool savePaths;
        [Tooltip(Tooltips.RoofPaths)] public List<RoofPath> paths = ProBuilderRoof.DefaultPaths();

        public static event Action<ProBuilderRoofStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnValidate() => Changed?.Invoke(this);
    }
}
