using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RATF.Domain;
namespace RATF.Presentation {
    public sealed class AssetViewRegistry:IDisposable {
        sealed class Retiring {
            public UnitView View;
            public string Definition;
            public float Remaining=.5f;
            public bool Factory;
        }
        readonly VisualCatalogSO catalog;
        readonly Transform parent;
        readonly Camera camera;
        readonly Dictionary<long,UnitView> active=new Dictionary<long,UnitView>();
        readonly Dictionary<string,Stack<UnitView>> pools=new Dictionary<string,Stack<UnitView>>();
        readonly List<Retiring> retiring=new List<Retiring>();
        public int ActiveCount=>active.Count;
        public AssetViewRegistry(VisualCatalogSO catalog,Transform parent,Camera camera) {
            this.catalog=catalog;
            this.parent=parent;
            this.camera=camera;
            catalog.Validate();
        }
        void Release(UnitView view,string definition) {
            view.Release();
            if(!pools.TryGetValue(definition,out var stack))pools[definition]=stack=new Stack<UnitView>();
            stack.Push(view);
        }
        public void Sync(SimulationWorld world,float alpha,bool reducedMotion) {
            foreach(var evt in world.Events)if(evt.Kind=="attack"&&active.TryGetValue(evt.Id,out var view))view.Attack();
            foreach(var id in active.Keys.Where(id=>!world.State.Entities.Any(e=>e.Id==id)).ToArray()) {
                var v=active[id];
                string def=v.DefinitionId;
                retiring.Add(new Retiring {
                    View=v,Definition=def,Factory=world.Units[def].Factory
                });
                active.Remove(id);
            }
            foreach(var r in retiring.ToArray()) {
                r.Remaining-=Time.unscaledDeltaTime;
                r.View.Retreat(r.Remaining/.5f,r.Factory,reducedMotion);
                if(r.Remaining<=0) {
                    Release(r.View,r.Definition);
                    retiring.Remove(r);
                }
            }
            foreach(var e in world.State.Entities) {
                if(!active.TryGetValue(e.Id,out var v)) {
                    if(pools.TryGetValue(e.DefinitionId,out var stack)&&stack.Count>0)v=stack.Pop();
                    else {
                        var go=UnityEngine.Object.Instantiate(catalog.Find(e.DefinitionId).prefab,parent);
                        v=go.GetComponent<UnitView>();
                        if(v==null)throw new InvalidOperationException("Wrapper requires UnitView");
                    }
                    v.Bind(e,world.Units[e.DefinitionId]);
                    active.Add(e.Id,v);
                }
                v.Render(e,world.Units[e.DefinitionId],world.Level.LaneY[e.Lane-1],alpha,world.State.Tick,camera,reducedMotion);
            }
        }
        public void Dispose() {
            foreach(var v in active.Values)if(v!=null)UnityEngine.Object.Destroy(v.gameObject);
            foreach(var stack in pools.Values)foreach(var v in stack)if(v!=null)UnityEngine.Object.Destroy(v.gameObject);
            foreach(var r in retiring)if(r.View!=null)UnityEngine.Object.Destroy(r.View.gameObject);
            active.Clear();
            pools.Clear();
            retiring.Clear();
        }
    }
}
