using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Forester.Domain;
namespace Forester.Presentation {
    public sealed class AssetViewRegistry:IDisposable {
        readonly VisualCatalogSO catalog;
        readonly Transform root;
        readonly BoardFeedback feedback;
        readonly Camera cameraView;
        readonly Dictionary<long,(double hp,Vector3 position)> previousThreats=new Dictionary<long,(double,Vector3)>();
        readonly Dictionary<string,Stack<GameObject>> pool=new Dictionary<string,Stack<GameObject>>();
        readonly Dictionary<string,GameObject> live=new Dictionary<string,GameObject>();
        readonly Dictionary<string,string> keys=new Dictionary<string,string>();
        readonly List<GameObject> all=new List<GameObject>();
        public AssetViewRegistry(VisualCatalogSO c,Transform parent,BoardFeedback effects=null,Camera camera=null) {
            feedback=effects;
            cameraView=camera;
            catalog=c;
            root=parent;
        }
        public static Vector3 World(Point p,float y=0)=>new Vector3((float)p.X,y,(float)p.Z);
        GameObject Get(string entity,string visual) {
            if(live.TryGetValue(entity,out var go))return go;
            if(!pool.TryGetValue(visual,out var q))pool[visual]=q=new Stack<GameObject>();
            go=q.Count>0?q.Pop():UnityEngine.Object.Instantiate(catalog.Find(visual).prefab,root);
            if(!all.Contains(go))all.Add(go);
            go.name=entity+" · "+visual;
            go.SetActive(true);
            live[entity]=go;
            keys[entity]=visual;
            return go;
        }
        public void Fire(long id,Vector3 target) {
            if(live.TryGetValue("D"+id,out var go))go.GetComponent<EntityView>()?.Fire(target);
        }
        public void Sync(SimulationWorld world,double alpha) {
            var wanted=new HashSet<string>();
            foreach(var d in world.Board.Defenders.Values) {
                string id="D"+d.Id;
                wanted.Add(id);
                Get(id,world.Board.Catalog.Defenders[d.DefinitionId].VisualId).transform.position=World(world.Board.Level.Position(d.Cell));
            }
            foreach(var t in world.Threats.Values) {
                string id="T"+t.Id;
                wanted.Add(id);
                var go=Get(id,world.Board.Catalog.Enemies[t.DefinitionId].VisualId);
                go.transform.position=World(new Point(t.PreviousPosition.X+(t.Position.X-t.PreviousPosition.X)*alpha,t.PreviousPosition.Z+(t.Position.Z-t.PreviousPosition.Z)*alpha));
                var view=go.GetComponent<EntityView>();
                if(previousThreats.TryGetValue(t.Id,out var old)&&t.HP<old.hp)view?.Hit();
                if(feedback&&cameraView)view?.Health((float)(t.HP/world.Board.Catalog.Enemies[t.DefinitionId].HP),feedback.Material,cameraView);
                previousThreats[t.Id]=(t.HP,go.transform.position);
                var direction=World(t.Position)-World(t.PreviousPosition);
                if(direction.sqrMagnitude>0.00001f)go.transform.rotation=Quaternion.LookRotation(direction);
            }
            foreach(var id in previousThreats.Keys.Where(id=>!world.Threats.ContainsKey(id)).ToArray()) {
                if(feedback)feedback.Emit(previousThreats[id].position,new Color(.5f,1,.8f),1.1f,.45f);
                previousThreats.Remove(id);
            }
            foreach(var id in live.Keys.Where(k=>!wanted.Contains(k)).ToArray()) {
                var go=live[id];
                go.GetComponent<EntityView>()?.ResetView();
                go.SetActive(false);
                pool[keys[id]].Push(go);
                live.Remove(id);
                keys.Remove(id);
            }
        }
        public void Dispose() {
            foreach(var go in all)if(go)UnityEngine.Object.Destroy(go);
            live.Clear();
            pool.Clear();
            all.Clear();
        }
    }
}
