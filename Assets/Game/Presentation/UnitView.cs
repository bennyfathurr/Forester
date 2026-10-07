using UnityEngine;
using RATF.Domain;
namespace RATF.Presentation {
    public sealed class UnitView:MonoBehaviour {
        public Transform VisualRoot;
        public TextMesh Label;
        public Renderer TeamMarker;
        public long Id;
        public string DefinitionId;
        float pulse;
        float baseScale=1;
        Material ownedMarker;
        StructureView structure;
        public void Bind(EntityState e,UnitDefinition d) {
            Id=e.Id;
            DefinitionId=e.DefinitionId;
            if(d.Machine&&structure==null) {
                structure=gameObject.AddComponent<StructureView>();
                structure.Initialize(TeamMarker);
            }
            gameObject.SetActive(true);
            pulse=0;
            if(VisualRoot!=null) {
                baseScale=VisualRoot.localScale.x;
                VisualRoot.localRotation=Quaternion.Euler(0,d.Factory?-90:90,0);
            }
            if(TeamMarker!=null) {
                if(ownedMarker==null)ownedMarker=TeamMarker.material;
                ownedMarker.color=d.Factory?new Color(.1f,.75f,.95f):new Color(1,.43f,.12f);
            }
        }
        public void Render(EntityState e,UnitDefinition d,float laneY,float alpha,int tick,Camera camera,bool reducedMotion) {
            transform.position=new Vector3(Mathf.Lerp(e.PreviousX,e.X,alpha),0,laneY+(d.Machine?1:((e.Id%3)-1)*.16f));
            if(Label!=null) {
                Label.text=d.Support?"SUPPORT":(tick<e.ActiveTick?"BUILD ":"")+e.Hp+" HP"+(tick<e.SlowUntil?" • FOAM":"")+(tick<e.RallyUntil?" • RALLY":"");
                Label.transform.rotation=camera.transform.rotation;
            }
            if(structure!=null)structure.Construction(tick<e.ActiveTick,Mathf.Max(0,e.ActiveTick-tick),reducedMotion);
            pulse=Mathf.MoveTowards(pulse,0,Time.deltaTime*5);
            if(VisualRoot!=null&&!reducedMotion)VisualRoot.localScale=Vector3.one*baseScale*(1+pulse*.08f);
        }
        public void Retreat(float remaining,bool factory,bool reducedMotion) {
            if(Label!=null)Label.text=factory?"BREAKDOWN":"RETREAT";
            if(!reducedMotion) {
                if(factory&&VisualRoot!=null)VisualRoot.localScale=Vector3.one*baseScale*Mathf.Max(.01f,remaining);
                else transform.position+=Vector3.left*Time.unscaledDeltaTime*2;
            }
        }
        public void Attack() {
            pulse=1;
        }
        void OnDestroy() {
            if(ownedMarker!=null)Destroy(ownedMarker);
        }
        public void Release() {
            Id=0;
            DefinitionId=null;
            pulse=0;
            if(VisualRoot!=null)VisualRoot.localScale=Vector3.one*baseScale;
            if(Label!=null)Label.text="";
            gameObject.SetActive(false);
        }
    }
}
