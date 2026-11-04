using System;
using UnityEngine;

namespace Tsitski.ProBuilderTools {
    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Stairs Stats", fileName = "Stairs Stats")]
    public class ProBuilderStairsStats : ScriptableObject {
        [Tooltip(Tooltips.StairsWidth)] [Min(ProBuilderStairs.MinSize)] public float width = 1.2f;
        [Tooltip(Tooltips.StepHeight)] [Min(ProBuilderStairs.MinSize)] public float stepHeight = 0.18f;
        [Tooltip(Tooltips.Solid)] public bool solid = true;
        [Tooltip(Tooltips.StairsThickness)] [Min(ProBuilderStairs.MinSize)] public float thickness = 0.15f;
        [Tooltip(Tooltips.GenerateCutter)] public bool generateCutter;
        [Tooltip(Tooltips.CutterHeadroom)] [Min(ProBuilderStairs.MinSize)] public float cutterHeadroom = 2.1f;
        [Tooltip(Tooltips.LeftTrim)] public ProBuilderTrimStats leftTrim;
        [Tooltip(Tooltips.RightTrim)] public ProBuilderTrimStats rightTrim;
        [Tooltip(Tooltips.SmoothCollider)] public bool smoothCollider;

        public static event Action<ProBuilderStairsStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnValidate() => Changed?.Invoke(this);
    }
}
