using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Forester.Domain;
using Forester.Presentation;
namespace Forester.Editor {
    public sealed class LevelAuthoringWindow:EditorWindow {
        LevelDefinitionSO level;
        CardCatalogSO catalog;
        CellKind paint=CellKind.Buildable;
        bool painting;
        string result="Select an authored level. Inspector edits ordered waypoints and wave allowlists.";
        [MenuItem("Forester/Level authoring painter")]static void Open()=>GetWindow<LevelAuthoringWindow>("Forester Level");
        void OnEnable()=>SceneView.duringSceneGui+=Draw;
        void OnDisable()=>SceneView.duringSceneGui-=Draw;
        void OnGUI() {
            level=(LevelDefinitionSO)EditorGUILayout.ObjectField("Level",level,typeof(LevelDefinitionSO),false);
            catalog=(CardCatalogSO)EditorGUILayout.ObjectField("Card catalog",catalog,typeof(CardCatalogSO),false);
            paint=(CellKind)EditorGUILayout.EnumPopup("Cell tag",paint);
            painting=EditorGUILayout.Toggle("Paint with left click",painting);
            if(GUILayout.Button("Select level for waypoint/data editing")&&level)Selection.activeObject=level;
            if(GUILayout.Button("Paint intermediate route / endpoint tags")&&level) {
                Undo.RecordObject(level,"Repaint paths");
                RepaintPaths(level);
                EditorUtility.SetDirty(level);
                SceneView.RepaintAll();
            }
            if(GUILayout.Button("Validate before play")&&level)try {
                PathValidator.Validate(level.Copy(),catalog?catalog.Copy():null);
                result="Valid complete routes, endpoints, build masks and footprints.";
                AssetDatabase.SaveAssets();
            }
            catch(Exception e) {
                result=e.Message;
            }
            if(GUILayout.Button("Save and rebuild dedicated scene")&&level) {
                try {
                    PathValidator.Validate(level.Copy(),catalog?catalog.Copy():null);
                    AssetDatabase.SaveAssets();
                    ForesterLevelBuilder.Rebuild();
                    result="Scene rebuilt. The builder uses Data/Levels/VillageForestEdge.asset.";
                }
                catch(Exception e) {
                    result=e.Message;
                }
            }
            EditorGUILayout.HelpBox(result,MessageType.Info);
        }
        public static void RepaintPaths(LevelDefinitionSO a) {
            var l=a.Copy();
            foreach(var cell in l.Cells.Where(x=>x.Value==CellKind.Path||x.Value==CellKind.Spawn||x.Value==CellKind.Goal).Select(x=>x.Key).ToArray())l.Cells.Remove(cell);
            foreach(var route in l.Routes) {
                foreach(var c in PathValidator.Cells(route)) {
                    if(l.Kind(c)==CellKind.Buildable)throw new ArgumentException("Buildable cell overlaps new route: "+c.Id);
                    l.Cells[c]=CellKind.Path;
                }
                l.Cells[route.Waypoints[0]]=CellKind.Spawn;
                l.Cells[route.Waypoints.Last()]=CellKind.Goal;
            }
            a.From(l);
        }
        void Draw(SceneView view) {
            if(!level)return;
            for(int c=0;      c<level.columns;      c++)for(int r=0;      r<level.rows;      r++) {
                var cell=new Cell(c,r);
                var tag=Array.Find(level.cells,x=>x.cell.Equals(cell));
                Handles.color=tag==null||tag.kind==CellKind.Scenic?new Color(.4f,.5f,.4f,.3f):tag.kind==CellKind.Buildable?Color.cyan:tag.kind==CellKind.Goal?Color.blue:Color.yellow;
                var pos=level.origin+new Vector3(c*level.cellSize,.08f,r*level.cellSize);
                Handles.DrawWireCube(pos,new Vector3(level.cellSize*.92f,.01f,level.cellSize*.92f));
            }
            foreach(var route in level.routes) {
                Handles.color=route.Id=="NORTH"?Color.yellow:Color.magenta;
                for(int i=0;      i<route.Waypoints.Length;      i++) {
                    var cell=route.Waypoints[i];
                    var pos=level.origin+new Vector3(cell.Column*level.cellSize,.12f,cell.Row*level.cellSize);
                    Handles.Label(pos,route.Id+"/"+i);
                    if(!painting) {
                        EditorGUI.BeginChangeCheck();
                        var next=Handles.PositionHandle(pos,Quaternion.identity);
                        if(EditorGUI.EndChangeCheck()) {
                            Undo.RecordObject(level,"Move route waypoint");
                            route.Waypoints[i]=new Cell(Mathf.RoundToInt((next.x-level.origin.x)/level.cellSize),Mathf.RoundToInt((next.z-level.origin.z)/level.cellSize));
                            EditorUtility.SetDirty(level);
                        }
                    }
                    if(i>0) {
                        var prev=route.Waypoints[i-1];
                        Handles.DrawLine(level.origin+new Vector3(prev.Column*level.cellSize,.12f,prev.Row*level.cellSize),pos);
                    }
                }
            }
            var e=Event.current;
            if(painting) {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                if(e.type==EventType.MouseDown&&e.button==0&&!e.alt) {
                    var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    if(new Plane(Vector3.up,level.origin).Raycast(ray,out var distance)) {
                        var point=ray.GetPoint(distance);
                        var cell=new Cell(Mathf.RoundToInt((point.x-level.origin.x)/level.cellSize),Mathf.RoundToInt((point.z-level.origin.z)/level.cellSize));
                        if(cell.Column>=0&&cell.Row>=0&&cell.Column<level.columns&&cell.Row<level.rows) {
                            Undo.RecordObject(level,"Paint cell");
                            var list=level.cells.ToList();
                            list.RemoveAll(x=>x.cell.Equals(cell));
                            list.Add(new CellTag {
                                cell=cell,kind=paint
                            });
                            level.cells=list.ToArray();
                            EditorUtility.SetDirty(level);
                            e.Use();
                            SceneView.RepaintAll();
                        }
                    }
                }
            }
        }
    }
}
