using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tsitski.ProBuilderTools {
    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Trim Stats", fileName = "Trim Stats")]
    public class ProBuilderTrimStats : ScriptableObject {
        [Tooltip(Tooltips.Profile)] public List<Vector2> profile = ProBuilderTrim.DefaultProfile();
        [Tooltip(Tooltips.Radii)] public List<float> profileRadii = new();
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.ProfileMirror)] public ProfileMirror profileMirror;
        [Tooltip(Tooltips.StorePath)] public bool storePath;
        [Tooltip(Tooltips.TrimPath)] public List<Vector3> path = new() { Vector3.zero };
        [Tooltip(Tooltips.Radii)] public List<float> pathRadii = new();
        [Tooltip(Tooltips.Closed)] public bool closed;
        [Tooltip(Tooltips.LoopCutStep)] [Min(0f)] public float loopCutStep;
        [Tooltip(Tooltips.RoundBevel)] [Min(0)] public int roundBevel;
        [Tooltip(Tooltips.FlattenAngledEnds)] public bool flattenAngledEnds;

        public static event Action<ProBuilderTrimStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnValidate() => Changed?.Invoke(this);
    }
}
