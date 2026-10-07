using System.Collections.Generic;
using UnityEngine;
namespace Forester.Presentation {
    // Bounded cosmetic pool: never drives damage, cooldowns or placement.
    public sealed class BoardFeedback : MonoBehaviour {
        sealed class Pulse { public LineRenderer line; public float age, duration, radius; public Color color; }
        readonly List<Pulse> pool=new List<Pulse>();
        Material material;
        sealed class Spark { public Transform transform; public Vector3 velocity; public float age, life, size; }
        readonly List<Spark> sparks=new List<Spark>();
        MaterialPropertyBlock tint;
        AudioSource speaker;
        AudioClip chime;
        float nextSound;
        public bool Paused;
        public Material Material => material;
        public void Initialize(Material source) {
            tint=new MaterialPropertyBlock();
            material=new Material(source);
            material.color=Color.white;
            speaker=gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake=false;
            speaker.volume=.12f;
            chime=AudioClip.Create("Generated containment chime",4410,1,44100,false);
            var samples=new float[4410];
            for(int i=0;i<samples.Length;i++) {
                float t=i/44100f;
                samples[i]=Mathf.Sin(2*Mathf.PI*(660*t+500*t*t))*Mathf.Exp(-t*48)*(1-Mathf.Exp(-t*300));
            }
            chime.SetData(samples,0);
        }
        public void Emit(Vector3 position,Color color,float radius=1,float duration=.45f) {
            if(!material) return;
            var pulse=pool.Find(p=>!p.line.gameObject.activeSelf);
            if(pulse==null && pool.Count<64) {
                var go=new GameObject("Pooled feedback ring");
                go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();
                line.sharedMaterial=material;
                line.loop=true;
                line.useWorldSpace=false;
                line.positionCount=32;
                pulse=new Pulse{line=line};
                pool.Add(pulse);
            }
            if(pulse==null) return;
            pulse.age=0; pulse.duration=duration; pulse.radius=radius; pulse.color=color;
            pulse.line.transform.position=position+Vector3.up*.12f;
            pulse.line.gameObject.SetActive(true);
            Draw(pulse);
            Burst(position,color,radius);
            if(Time.unscaledTime>=nextSound) {
                speaker.pitch=radius>1?1f:1.45f;
                speaker.PlayOneShot(chime);
                nextSound=Time.unscaledTime+.085f;
            }
        }
        void Draw(Pulse p) {
            float t=p.age/p.duration;
            tint.SetColor("_BaseColor",p.color);
            p.line.SetPropertyBlock(tint);
            p.line.widthMultiplier=.12f*(1-t);
            float radius=p.radius*(.2f+.8f*t);
            for(int i=0;i<32;i++) { float a=i*Mathf.PI/16; p.line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius)); }
        }
        void Burst(Vector3 position,Color color,float radius) {
            for(int i=0;i<6;i++) {
                var spark=sparks.Find(x=>!x.transform.gameObject.activeSelf);
                if(spark==null&&sparks.Count<96) {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name="Pooled containment droplet";
                    Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(transform,false);
                    go.GetComponent<Renderer>().sharedMaterial=material;
                    spark=new Spark{transform=go.transform};
                    sparks.Add(spark);
                }
                if(spark==null)return;
                float angle=i*Mathf.PI/3+Time.time;
                spark.age=0; spark.life=.4f; spark.size=radius>1?.18f:.12f;
                spark.velocity=new Vector3(Mathf.Cos(angle)*2,2.8f,Mathf.Sin(angle)*2);
                spark.transform.position=position+Vector3.up*.55f;
                spark.transform.localScale=Vector3.one*spark.size;
                tint.SetColor("_BaseColor",color);
                spark.transform.GetComponent<Renderer>().SetPropertyBlock(tint);
                spark.transform.gameObject.SetActive(true);
            }
        }
        void Update() {
            if(Paused)return;
            foreach(var spark in sparks)if(spark.transform.gameObject.activeSelf) {
                spark.age+=Time.deltaTime;
                if(spark.age>=spark.life){spark.transform.gameObject.SetActive(false);continue;}
                spark.velocity+=Vector3.down*7*Time.deltaTime;
                spark.transform.position+=spark.velocity*Time.deltaTime;
                spark.transform.Rotate(new Vector3(110,180,50)*Time.deltaTime);
                spark.transform.localScale=Vector3.one*spark.size*(1-spark.age/spark.life);
            }
            foreach(var p in pool) if(p.line.gameObject.activeSelf) {
                p.age+=Time.deltaTime;
                if(p.age>=p.duration) p.line.gameObject.SetActive(false);
                else Draw(p);
            }
        }
        public void Clear() {
            foreach(var p in pool)p.line.gameObject.SetActive(false);
            foreach(var s in sparks)s.transform.gameObject.SetActive(false);
            if(speaker)speaker.Stop();
            Paused=false;
        }
        void OnDestroy() { if(material) Destroy(material); if(chime)Destroy(chime); }
    }
}
