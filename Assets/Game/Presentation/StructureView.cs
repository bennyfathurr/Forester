using UnityEngine;
namespace RATF.Presentation {
    public sealed class StructureView:MonoBehaviour {
        Renderer ring;
        Material material;
        public void Initialize(Renderer marker) {
            ring=marker;
            if(ring!=null)material=ring.material;
        }
        public void Construction(bool building,float remaining,bool reducedMotion) {
            if(ring==null)return;
            if(material!=null)material.color=building?new Color(.12f,.75f,.95f):new Color(.2f,.4f,.55f);
            ring.transform.localScale=new Vector3(.9f*(building&&!reducedMotion?1+.1f*Mathf.Sin(Time.unscaledTime*5):1),.02f,.9f);
        }
        void OnDestroy() {
            if(material!=null)Destroy(material);
        }
    }
}
