using System;
using System.Linq;
using UnityEngine;
using Forester.Domain;
namespace Forester.Presentation {
    [Serializable] public sealed class VisualBinding {
        public string id;
        public GameObject prefab;
        public Texture2D portrait;
        public AudioClip audio;
        public AnimationClip idle,fire,contained;
        public string mappingNote;
    }
    [CreateAssetMenu(menuName="Forester/Visual Catalog")] public sealed class VisualCatalogSO:ScriptableObject {
        public VisualBinding[] bindings;
        public VisualBinding Find(string id)=>Array.Find(bindings,b=>b.id==id);
        public void Validate(Catalog c) {
            var ids=new System.Collections.Generic.HashSet<string>();
            foreach(var b in bindings)if(string.IsNullOrWhiteSpace(b.id)||!ids.Add(b.id)||b.prefab==null||b.prefab.GetComponent<EntityView>()==null)throw new ArgumentException("Missing/duplicate visual: "+b.id);
            foreach(var d in c.Defenders.Values)if(Find(d.VisualId)==null)throw new ArgumentException("Missing defender visual "+d.VisualId);
            foreach(var e in c.Enemies.Values)if(Find(e.VisualId)==null)throw new ArgumentException("Missing threat visual "+e.VisualId);
        }
    }
}
