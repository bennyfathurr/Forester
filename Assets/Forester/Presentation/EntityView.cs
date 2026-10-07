using UnityEngine;
namespace Forester.Presentation {
    public sealed class EntityView : MonoBehaviour {
        public Transform visualRoot;
        Vector3 restScale;
        float born, recoil, flash;
        Renderer[] surfaces;
        MaterialPropertyBlock tint;
        LineRenderer health;
        void SetFlash(float value) {
            if(surfaces==null)return;
            tint.SetColor("_BaseColor",new Color(value,value,value,1));
            foreach(var surface in surfaces)surface.SetPropertyBlock(value>1?tint:null);
        }
        public void Hit() { flash=.12f; SetFlash(2.4f); }
        public void Health(float ratio,Material material,Camera cameraView) {
            if(!health) {
                var go=new GameObject("Threat health");
                go.transform.SetParent(transform,false);
                health=go.AddComponent<LineRenderer>();
                health.useWorldSpace=false;
                health.sharedMaterial=material;
                health.positionCount=2;
                health.widthMultiplier=.09f;
                health.numCapVertices=3;
            }
            var right=transform.InverseTransformDirection(cameraView.transform.right)*.65f;
            health.SetPosition(0,Vector3.up*1.65f-right);
            health.SetPosition(1,Vector3.up*1.65f-right+right*2*Mathf.Clamp01(ratio));
            tint.SetColor("_BaseColor",Color.Lerp(new Color(1,.35f,.2f),new Color(.4f,1,.7f),ratio));
            health.SetPropertyBlock(tint);
        }
        void Awake() {
            if(visualRoot)restScale=visualRoot.localScale;
            surfaces=GetComponentsInChildren<Renderer>();
            tint=new MaterialPropertyBlock();
        }
        void OnEnable() { born=Time.time; recoil=0; flash=0; SetFlash(1); }
        public void Fire(Vector3 target) {
            var direction=target-transform.position;
            direction.y=0;
            if(direction.sqrMagnitude>.001f) transform.rotation=Quaternion.LookRotation(direction);
            recoil=1;
        }
        void LateUpdate() {
            if(!visualRoot) return;
            if(flash>0) { flash-=Time.deltaTime; if(flash<=0)SetFlash(1); }
            float age=Time.time-born;
            float pop=1+Mathf.Sin(Mathf.Min(age/.35f,1)*Mathf.PI)*.22f;
            recoil=Mathf.MoveTowards(recoil,0,Time.deltaTime*7);
            visualRoot.localScale=Vector3.Scale(restScale,new Vector3(pop,2-pop,pop));
            visualRoot.localPosition=new Vector3(0,0,-recoil*.15f);
        }
        public void ResetView() {
            if(visualRoot) { visualRoot.localScale=restScale; visualRoot.localPosition=Vector3.zero; }
            gameObject.SetActive(false);
            transform.localScale=Vector3.one;
            transform.rotation=Quaternion.identity;
        }
    }
}
