using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Tsitski.ProBuilderTools {
    [Serializable]
    public class FloorPlanOutlineSettings {
        [Tooltip(Tooltips.OutlineTrimStats)] public ProBuilderTrimStats trimStats;
        [Tooltip(Tooltips.OutlineArrayStats)] [FormerlySerializedAs("stats")] public ProBuilderArrayStats arrayStats;
        [Tooltip(Tooltips.OutlineWallSide)] public WallSide wallSide = WallSide.Outside;
        [Tooltip(Tooltips.OutlineHeight)] public float height;
        [Tooltip(Tooltips.HeightFromTop)] public bool heightFromTop;

        public FloorPlanOutlineSettings Copy() => (FloorPlanOutlineSettings)MemberwiseClone();
    }
}
