using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Tsitski.ProBuilderTools {
    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Floor Plan Stats", fileName = "Floor Plan Stats")]
    public class ProBuilderFloorPlanStats : ScriptableObject {
        [Tooltip(Tooltips.WallHeight)] [Min(0.01f)] public float height = 3f;
        [Tooltip(Tooltips.WallThickness)] [Min(0.01f)] public float thickness = 0.2f;
        [Tooltip(Tooltips.Alignment)] public WallAlignment alignment;
        [Tooltip(Tooltips.WallsAroundHoles)] public bool wallsAroundHoles;
        [Tooltip(Tooltips.SquareCorners)] public bool squareCorners;
        [Tooltip(Tooltips.LoopCutStep)] [Min(0f)] public float loopCutStep;
        [Tooltip(Tooltips.VerticalLoopCutStep)] [Min(0f)] public float verticalLoopCutStep;
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.FloorPlanOutlineSettings)] [FormerlySerializedAs("arrays")]
        public FloorPlanOutlineSettings[] floorPlanOutlineSettings = Array.Empty<FloorPlanOutlineSettings>();

        public static event Action<ProBuilderFloorPlanStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnValidate() => Changed?.Invoke(this);
    }
}
