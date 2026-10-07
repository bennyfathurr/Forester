using UnityEngine;
namespace RATF.Presentation {
    public sealed class GateView:MonoBehaviour {
        public int lane;
        public GameObject visual;
        public TextMesh label;
        public void Render(int hp,int max,Camera camera) {
            if(visual!=null)visual.SetActive(hp>0);
            if(label!=null) {
                label.text=hp<=0?"L"+lane+" • OPEN":"L"+lane+" • GATE"+(hp<max?"\n"+hp+" / "+max:"");
                label.color=hp<=0?Color.green:Color.white;
                label.transform.rotation=camera.transform.rotation;
            }
        }
    }
}
