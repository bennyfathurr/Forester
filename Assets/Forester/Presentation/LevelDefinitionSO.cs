using System;
using System.Linq;
using UnityEngine;
using Forester.Domain;
namespace Forester.Presentation {
    [Serializable] public sealed class CellTag {
        public Cell cell;
        public CellKind kind;
    }
    [CreateAssetMenu(menuName="Forester/Level")] public sealed class LevelDefinitionSO:ScriptableObject {
        public int columns=16,rows=12,integrity=20,startingPP=120,capPP=200;
        public float cellSize=2;
        public Vector3 origin;
        public CellTag[] cells;
        public RouteDefinition[] routes;
        public WaveRule[] waves;
        public int[] awards= {
            55,65
        };
        public PhasePermissions permissions=new PhasePermissions();
        public bool orthographic,allowPanZoom;
        public float pitch=50,yaw=45,fieldOfView=40;
        public LevelDefinition Copy() {
            var l=new LevelDefinition {
                Columns=columns,Rows=rows,Integrity=integrity,StartingPP=startingPP,CapPP=capPP,CellSize=cellSize,Origin=new Point(origin.x,origin.z),Awards=(int[])awards.Clone(),Permissions=new PhasePermissions {
                    Place=(Phase[])permissions.Place.Clone(),Move=(Phase[])permissions.Move.Clone(),Sell=(Phase[])permissions.Sell.Clone()
                }
            };
            foreach(var c in cells)l.Cells.Add(c.cell,c.kind);
            foreach(var r in routes)l.Routes.Add(new RouteDefinition {
                Id=r.Id,SpawnId=r.SpawnId,GoalId=r.GoalId,Waypoints=(Cell[])r.Waypoints.Clone()
            });
            l.Waves=Array.ConvertAll(waves,w=>new WaveRule {
                Budget=w.Budget,MaxCount=w.MaxCount,Window=w.Window,Routes=(string[])w.Routes.Clone(),Enemies=(string[])w.Enemies.Clone(),Conditions=(string[])w.Conditions.Clone()
            });
            return l;
        }
        public void From(LevelDefinition l) {
            columns=l.Columns;
            rows=l.Rows;
            integrity=l.Integrity;
            startingPP=l.StartingPP;
            capPP=l.CapPP;
            cellSize=(float)l.CellSize;
            cells=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(l.Cells,x=>new CellTag {
                cell=x.Key,kind=x.Value
            }));
            routes=l.Routes.ToArray();
            waves=l.Waves;
            awards=l.Awards;
            permissions=l.Permissions;
        }
    }
}
