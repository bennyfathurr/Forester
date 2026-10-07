using System;
using UnityEngine;
namespace RATF.Presentation {
    [Serializable] public sealed class VisualBinding {
        public string id,status;
        public GameObject prefab;
        public Texture2D portrait;
        public AudioClip attackAudio;
        public string idleClip,moveClip,attackClip,retreatClip;
    }
    [CreateAssetMenu(menuName="RATF/Visual catalog")] public sealed class VisualCatalogSO:ScriptableObject {
        public VisualBinding[] bindings;
        public VisualBinding Find(string id) {
            return Array.Find(bindings,b=>b.id==id);
        }
        public void Validate() {
            if(bindings==null)throw new InvalidOperationException("Missing visual bindings");
            foreach(var id in new[] {
                "villager_emak","villager_bapak","villager_anak","security_robot","foam_cannon","bolt_turret","gate","refinery"
            }) {
                var b=Find(id);
                if(b==null||b.prefab==null||string.IsNullOrEmpty(b.status))throw new InvalidOperationException("Missing visual binding: "+id);
            }
        }
    }
}
