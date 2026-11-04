using System.Collections.Generic;
using UnityEngine;

namespace Tsitski.ProBuilderTools {
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class ProBuilderBakedCuts : MonoBehaviour {
        [Tooltip(Tooltips.BakedCutters)] public List<ProBuilderCutter> cutters = new();
    }
}
