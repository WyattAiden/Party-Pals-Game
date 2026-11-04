using System.Collections.Generic;
using UnityEngine;

namespace Tsitski.ProBuilderTools {
    public interface IPathOwner {
        List<Vector3> localPath { get; set; }
        bool pathInStats { get; }
        void Rebuild();
    }
}
