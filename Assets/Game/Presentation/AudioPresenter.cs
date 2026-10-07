using UnityEngine;
using RATF.Domain;
namespace RATF.Presentation {
    public sealed class AudioPresenter:MonoBehaviour {
        AudioSource source;
        AudioClip cue;
        public float volume=.35f;
        void Awake() {
            source=gameObject.AddComponent<AudioSource>();
            var data=new float[1600];
            for(int i=0;    i<data.Length;    i++)data[i]=Mathf.Sin(i*2*Mathf.PI*440/16000)*(1-i/(float)data.Length)*.12f;
            cue=AudioClip.Create("Generated interface cue (no supplied audio)",data.Length,1,16000,false);
            cue.SetData(data,0);
        }
        public void Events(System.Collections.Generic.List<WorldEvent> events) {
            source.volume=volume;
            foreach(var e in events)if(e.Kind=="spawn"||e.Kind=="gate_open"||e.Kind=="rally")source.PlayOneShot(cue);
        }
        void OnDestroy() {
            if(cue!=null)Destroy(cue);
        }
    }
}
