using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Serialization;

namespace Tsitski.ProBuilderTools {
    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Array Stats", fileName = "Array Stats")]
    public class ProBuilderArrayStats : ScriptableObject {
        [Tooltip(Tooltips.ArrayStatsSource)] public ProBuilderMesh source;
        [Tooltip(Tooltips.StoreSource)] public bool storeSource;
        public ArraySourceData sourceMesh = new();
        [Tooltip(Tooltips.ArrayPillarStats)] public ProBuilderPillarStats pillarStats;
        [Tooltip(Tooltips.ArrayPillarHeight)] [Min(0.01f)] public float pillarHeight = 3f;
        [Tooltip(Tooltips.MergeIntoMesh)] public bool mergeIntoMesh;
        [Tooltip(Tooltips.Spacing)] [Min(ProBuilderArray.MinSpacing)] public float spacing = 1f;
        [Tooltip(Tooltips.Offset)] [Min(0f)] public float offset;
        [Tooltip(Tooltips.FitSpacing)] [FormerlySerializedAs("spacePerSegment")] [FormerlySerializedAs("fitToPath")] public bool fitSpacing;
        [Tooltip(Tooltips.SplitAtCorners)] public bool splitAtCorners = true;
        [Tooltip(Tooltips.DivisionLength)] [Min(0f)] public float divisionLength;
        [Tooltip(Tooltips.SkipCorners)] [FormerlySerializedAs("avoidCorners")] public bool skipCorners;
        [Tooltip(Tooltips.SkipDivisions)] public bool skipDivisions = true;
        [Tooltip(Tooltips.AlignToPath)] public bool alignToPath = true;
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.StorePath)] public bool storePath;
        [Tooltip(Tooltips.ArrayPath)] public List<Vector3> path = new() { Vector3.zero };
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();
        [Tooltip(Tooltips.Closed)] public bool closed;

        public static event Action<ProBuilderArrayStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnValidate() => Changed?.Invoke(this);
    }
}
