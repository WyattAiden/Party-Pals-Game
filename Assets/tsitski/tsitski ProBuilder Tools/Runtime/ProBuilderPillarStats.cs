using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsitski.ProBuilderTools {
    [CreateAssetMenu(menuName = "tsitski ProBuilder Tools/Pillar Stats", fileName = "Pillar Stats")]
    public class ProBuilderPillarStats : ScriptableObject {
        [Tooltip(Tooltips.PillarShape)] public ProBuilderPillar.Shape shape;
        [Tooltip(Tooltips.AutoSnapToPath)] public bool autoSnapToPath = true;
        [Tooltip(Tooltips.SnapToFlats)] public bool snapToFlats;
        [Tooltip(Tooltips.GenerateDownwards)] public bool generateDownwards;
        [Tooltip(Tooltips.CornerLength)] [Min(ProBuilderPillar.MinCornerLength)] public float cornerLength = 0.5f;
        [Tooltip(Tooltips.PillarRoundBevel)] [Min(0)] public int roundBevel;
        [Tooltip(Tooltips.BottomProfile)] public PillarProfile bottomProfile = PillarProfile.DefaultBottom();
        [Tooltip(Tooltips.BottomHeight)] [Min(0f)] public float bottomHeight = ProBuilderPillar.DefaultBottomHeight;
        [Tooltip(Tooltips.CenterProfile)] public PillarProfile centerProfile = PillarProfile.DefaultCenter();
        [Tooltip(Tooltips.StretchCenter)] public bool stretchCenter;
        [Tooltip(Tooltips.CenterHeight)] [Min(ProBuilderPillar.MinCenterHeight)] public float centerHeight = ProBuilderPillar.DefaultCenterHeight;
        [Tooltip(Tooltips.TopProfile)] public PillarProfile topProfile = PillarProfile.DefaultTop();
        [Tooltip(Tooltips.TopHeight)] [Min(0f)] public float topHeight = ProBuilderPillar.DefaultTopHeight;
        [Tooltip(Tooltips.PillarSides)] [Min(ProBuilderPillar.MinSides)] public int sides = 12;
        [Tooltip(Tooltips.RoundingSegments)] [Min(1)] public int roundingSegments = 8;
        [Tooltip(Tooltips.SmoothingAngle)] [Range(0f, 180f)] public float smoothingAngle = 40f;

        [SerializeField] [HideInInspector] [FormerlySerializedAs("profile")] private List<Vector2> legacyProfile = new();
        [SerializeField] [HideInInspector] [FormerlySerializedAs("profileRadii")] private List<float> legacyProfileRadii = new();

        public static event Action<ProBuilderPillarStats> Changed;

        public void NotifyChanged() => Changed?.Invoke(this);

        private void OnEnable() => MigrateLegacyProfile();

        private void OnValidate() {
            MigrateLegacyProfile();
            bottomHeight = Mathf.Max(0f, bottomHeight);
            topHeight = Mathf.Max(0f, topHeight);
            centerHeight = Mathf.Max(ProBuilderPillar.MinCenterHeight, centerHeight);
            Changed?.Invoke(this);
        }

        private void MigrateLegacyProfile() {
            if (legacyProfile is not { Count: >= 2 })
                return;

            if (PillarProfile.Split(legacyProfile, legacyProfileRadii, bottomHeight, topHeight,
                    out bottomProfile, out centerProfile, out topProfile, out bottomHeight, out float center, out topHeight))
                centerHeight = Mathf.Max(ProBuilderPillar.MinCenterHeight, center);

            legacyProfile = new List<Vector2>();
            legacyProfileRadii = new List<float>();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}
