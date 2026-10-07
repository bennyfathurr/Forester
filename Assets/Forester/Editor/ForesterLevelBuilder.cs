using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Forester.Domain;
using Forester.Presentation;
using Forester.Composition;
namespace Forester.Editor {
    public static class ForesterLevelBuilder {
        public const string ScenePath="Assets/Forester/Scenes/VillageForestEdge.unity";
        const string Base="Assets/Forester/";
        static T Asset<T>(string path,Action<T> init)where T:ScriptableObject {
            var a=AssetDatabase.LoadAssetAtPath<T>(path);
            if(a)return a;
            a=ScriptableObject.CreateInstance<T>();
            init(a);
            AssetDatabase.CreateAsset(a,path);
            return a;
        }
        public static Material Mat(string name,Color color,string shaderName="Universal Render Pipeline/Lit") {
            string path=Base+"Data/Visuals/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m)return m;
            m=new Material(Shader.Find(shaderName)) {
                name=name,color=color
            };
            AssetDatabase.CreateAsset(m,path);
            return m;
        }
        static GameObject Cube(string name,Transform parent,Vector3 pos,Vector3 scale,Material mat) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=name;
            go.transform.SetParent(parent);
            go.transform.position=pos;
            go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=mat;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        public static GameObject Wrapper(string visualId,string modelName,bool character) {
            string path=Base+"Prefabs/"+visualId+".prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(existing)return existing;
            string pack=character?"kenney_mini-characters":"kenney_tower-defense-kit";
            string source=$"Assets/3D/{pack}/Models/FBX format/{modelName}.fbx";
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if(!model)throw new ArgumentException("Missing original model "+source);
            var root=new GameObject(visualId);
            var view=root.AddComponent<EntityView>();
            var visual=new GameObject("Original model");
            visual.transform.SetParent(root.transform,false);
            view.visualRoot=visual.transform;
            var child=(GameObject)PrefabUtility.InstantiatePrefab(model);
            child.transform.SetParent(visual.transform,false);
            var bounds=new Bounds();
            bool first=true;
            foreach(var r in child.GetComponentsInChildren<Renderer>()) {
                if(first) {
                    bounds=r.bounds;
                    first=false;
                }
                else bounds.Encapsulate(r.bounds);
            }
            float size=Mathf.Max(.01f,Mathf.Max(bounds.size.y,Mathf.Max(bounds.size.x,bounds.size.z)));
            float scale=1.4f/size;
            visual.transform.localScale=Vector3.one*scale;
            child.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
            foreach(var col in root.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(col);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }
        [MenuItem("Forester/Build dedicated scene if missing")]public static void BuildIfMissing() {
            Build(false);
        }
        [MenuItem("Forester/Rebuild dedicated scene from authored data")]public static void Rebuild() {
            Build(true);
        }
        public static void Build(bool replace) {
            foreach(var dir in new[] {
                "Data/Cards","Data/Defenders","Data/Enemies","Data/Conditions","Data/Levels","Data/Providers","Data/Visuals","Prefabs","Scenes"
            })Directory.CreateDirectory(Base+dir);
            AssetDatabase.Refresh();
            var defaults=Defaults.Catalog();
            foreach(var c in defaults.Cards.Values)Asset<CardDefinitionSO>(Base+"Data/Cards/"+c.Id+".asset",a=>a.definition=c);
            foreach(var d in defaults.Defenders.Values)Asset<DefenderDefinitionSO>(Base+"Data/Defenders/"+d.Id+".asset",a=>a.definition=d);
            foreach(var e in defaults.Enemies.Values)Asset<EnemyDefinitionSO>(Base+"Data/Enemies/"+e.Id+".asset",a=>a.definition=e);
            Asset<ConditionDefinitionSO>(Base+"Data/Conditions/gust_front.asset",a=>a.definition=new ConditionDefinition());
            var cards=Asset<CardCatalogSO>(Base+"Data/Cards/CardCatalog.asset",a=> {
                a.cards=defaults.Cards.Keys.Select(id=>AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(Base+"Data/Cards/"+id+".asset")).ToArray();      a.defenders=defaults.Defenders.Keys.Select(id=>AssetDatabase.LoadAssetAtPath<DefenderDefinitionSO>(Base+"Data/Defenders/"+id+".asset")).ToArray();      a.enemies=defaults.Enemies.Keys.Select(id=>AssetDatabase.LoadAssetAtPath<EnemyDefinitionSO>(Base+"Data/Enemies/"+id+".asset")).ToArray();      a.conditions=new[] {
                    AssetDatabase.LoadAssetAtPath<ConditionDefinitionSO>(Base+"Data/Conditions/gust_front.asset")
                };
            });
            var level=Asset<LevelDefinitionSO>(Base+"Data/Levels/VillageForestEdge.asset",a=>a.From(Defaults.Level()));
            Asset<ProviderProfileSO>(Base+"Data/Providers/Apertus.asset",a=>a.planSchema=AssetDatabase.LoadAssetAtPath<TextAsset>(Base+"Data/Providers/threat_plan.schema.json"));
            var visuals=Asset<VisualCatalogSO>(Base+"Data/Visuals/VisualCatalog.asset",a=> {
                string[] ids= {
                    "water_team","watch_post","foam_station","warga","water_tank","emberling","wind_runner","brush_cluster","forest_goal","tree"
                };      string[] models= {
                    "character-female-a","weapon-turret","weapon-cannon","character-male-a","tower-square-build-f","enemy-ufo-a","enemy-ufo-b","enemy-ufo-c","wood-structure","detail-tree"
                };      var bindings=new List<VisualBinding>();      for(int i=0;      i<ids.Length;      i++) {
                    bool character=i==0||i==3;      string pack=character?"kenney_mini-characters":"kenney_tower-defense-kit";      bindings.Add(new VisualBinding {
                        id=ids[i],prefab=Wrapper(ids[i],models[i],character),portrait=AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/3D/{pack}/Previews/{models[i]}.png"),mappingNote="Original static model; gameplay proxy"
                    });
                }
                a.bindings=bindings.ToArray();
            });
            PathValidator.Validate(level.Copy(),cards.Copy());
            visuals.Validate(cards.Copy());
            AssetDatabase.SaveAssets();
            if(File.Exists(ScenePath)&&!replace) {
                Validate();
                return;
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameObject("Board").transform;
            var l=level.Copy();
            var green=Mat("ForestFloor",new Color(.22f,.40f,.25f));
            var trail=Mat("Trail",new Color(.58f,.40f,.25f));
            var outline=Mat("BuildCellOutline",new Color(.45f,.90f,.75f));
            var north=Mat("Entry",new Color(.97f,.40f,.24f));
            var goalMat=Mat("Goal",new Color(.2f,.8f,.98f));
            var origin=level.origin;
            Cube("Forest plateau",world,origin+new Vector3((l.Columns-1)*(float)l.CellSize/2,-.3f,(l.Rows-1)*(float)l.CellSize/2),new Vector3(l.Columns*(float)l.CellSize,.55f,l.Rows*(float)l.CellSize),green);
            var cliff=Mat("IslandSlate",new Color(.20f,.25f,.29f));
            var moss=Mat("MossVariation",new Color(.29f,.46f,.26f));
            var pad=Mat("BuildPad",new Color(.16f,.29f,.27f));
            var stone=Mat("PathStone",new Color(.69f,.57f,.39f));
            var random=new System.Random(417);
            // Deterministic perimeter columns give the board depth from every orbit angle.
            for(int x=-1;x<=l.Columns;x++)for(int z=-1;z<=l.Rows;z++) {
                if(x!=-1&&x!=l.Columns&&z!=-1&&z!=l.Rows)continue;
                float depth=2.5f+(float)random.NextDouble()*2;
                var edge=origin+new Vector3(x*(float)l.CellSize,-depth*.5f-.1f,z*(float)l.CellSize);
                Cube("Basalt island edge",world,edge,new Vector3(2.05f,depth,2.05f),cliff);
                Cube("Moss rim",world,edge+Vector3.up*(depth*.5f),new Vector3(2.03f,.18f,2.03f),moss);
            }
            Cube("Island foundation",world,origin+new Vector3((l.Columns-1)*(float)l.CellSize/2,-1.65f,(l.Rows-1)*(float)l.CellSize/2),new Vector3(l.Columns*(float)l.CellSize,3,l.Rows*(float)l.CellSize),cliff);
            var cells=new GameObject("GridCells (dedicated collider layer 30)").transform;
            cells.SetParent(world);
            for(int col=0;      col<l.Columns;      col++)for(int row=0;      row<l.Rows;      row++) {
                var c=new Cell(col,row);
                var pos=AssetViewRegistry.World(l.Position(c));
                var go=new GameObject(c.Id);
                go.transform.SetParent(cells);
                go.transform.position=pos;
                go.layer=30;
                go.AddComponent<GridCellView>().cell=c;
                var collider=go.AddComponent<BoxCollider>();
                collider.center=new Vector3(0,.01f,0);
                collider.size=new Vector3((float)l.CellSize,.1f,(float)l.CellSize);
                var kind=l.Kind(c);
                if(kind==CellKind.Scenic&&(col+row)%3==0)Cube("Meadow patch",world,pos+Vector3.down*.01f,new Vector3(1.96f,.025f,1.96f),moss);
                if(kind==CellKind.Path)Cube("Trail stepping stone",world,pos+new Vector3(.15f,.04f,-.1f),new Vector3(.68f,.055f,.5f),stone);
                if(kind==CellKind.Path||kind==CellKind.Spawn||kind==CellKind.Goal)Cube("Trail tile "+c.Id,world,pos+new Vector3(0,.005f,0),new Vector3((float)l.CellSize-.05f,.04f,(float)l.CellSize-.05f),kind==CellKind.Spawn?north:kind==CellKind.Goal?goalMat:trail);
                else if(kind==CellKind.Buildable) {
                    Cube("Inset build pad "+c.Id,world,pos+Vector3.up*.015f,new Vector3(1.72f,.055f,1.72f),pad);
                    float s=(float)l.CellSize*.44f;
                    foreach(var edge in new[] {
                        new Vector3(-s,.06f,0),new Vector3(s,.06f,0),new Vector3(0,.06f,-s),new Vector3(0,.06f,s)
                    })Cube("Build outline "+c.Id,go.transform,pos+edge,Mathf.Abs(edge.x)>0?new Vector3(.045f,.025f,s*2):new Vector3(s*2,.025f,.045f),outline);
                }
            }
            var decorations=new GameObject("Decorations").transform;
            decorations.SetParent(world);
            foreach(var c in Enumerable.Range(0,l.Columns).SelectMany(x=>Enumerable.Range(0,l.Rows).Select(z=>new Cell(x,z))))if(l.Kind(c)==CellKind.Scenic&&(c.Column*7+c.Row*13)%3==0&&!l.Cells.Any(x=>(x.Value==CellKind.Path||x.Value==CellKind.Spawn||x.Value==CellKind.Buildable)&&Math.Abs(x.Key.Column-c.Column)+Math.Abs(x.Key.Row-c.Row)<=1)) {
                var tree=(GameObject)PrefabUtility.InstantiatePrefab(visuals.Find("tree").prefab);
                tree.transform.SetParent(decorations);
                tree.transform.position=AssetViewRegistry.World(l.Position(c));
                tree.transform.localScale=Vector3.one*(1.25f+(float)random.NextDouble()*.8f);
                tree.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
            }
            // Entry beacons and the protected gate remain identifiable from the back of the island.
            foreach(var route in l.Routes) {
                var entry=AssetViewRegistry.World(l.Position(route.Waypoints[0]));
                foreach(float offset in new[]{-.75f,.75f}) {
                    Cube("Entry beacon base",world,entry+new Vector3(0,.3f,offset),new Vector3(.3f,.6f,.3f),cliff);
                    Cube("Entry beacon ember",world,entry+new Vector3(0,.72f,offset),new Vector3(.22f,.22f,.22f),north);
                }
            }
            var rockPrefab=Wrapper("scenery_rocks","detail-rocks",false);
            foreach(var cell in l.Cells.Keys) {
                if(l.Kind(cell)!=CellKind.Scenic||(cell.Column*13+cell.Row*5)%7!=0)continue;
                var rock=(GameObject)PrefabUtility.InstantiatePrefab(rockPrefab);
                rock.transform.SetParent(decorations);
                rock.transform.position=AssetViewRegistry.World(l.Position(cell));
                rock.transform.localScale=Vector3.one*.65f;
                rock.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
            }
            var goal=(GameObject)PrefabUtility.InstantiatePrefab(visuals.Find("forest_goal").prefab);
            goal.name="Protected forest gate";
            goal.transform.SetParent(world);
            goal.transform.position=AssetViewRegistry.World(l.Position(l.Routes[0].Waypoints.Last()));
            goal.transform.localScale=Vector3.one*1.3f;
            var camObject=new GameObject("MainCamera");
            var cam=camObject.AddComponent<Camera>();
            camObject.tag="MainCamera";
            camObject.AddComponent<AudioListener>();
            cam.backgroundColor=new Color(.035f,.08f,.105f);
            cam.clearFlags=CameraClearFlags.SolidColor;
            cam.rect=new Rect(0,0,1,1);
            camObject.AddComponent<BoardCamera>().Frame(level);
            var sun=new GameObject("Sun").AddComponent<Light>();
            sun.type=LightType.Directional;
            sun.intensity=1.5f;
            sun.shadows=LightShadows.Soft;
            sun.transform.rotation=Quaternion.Euler(55,-35,0);
            RenderSettings.ambientLight=new Color(.65f,.75f,.70f);
            var canvasObject=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=canvasObject.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,900);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var root=new GameObject("ForesterMatch - Composition");
            var composition=root.AddComponent<ForesterCompositionRoot>();
            var presenter=root.AddComponent<GamePresenter>();
            var hud=root.AddComponent<HudPresenter>();
            hud.canvas=canvas;
            presenter.hud=hud;
            presenter.placementPreviewMaterial=Mat("PlacementPreview",new Color(.1f,1,.6f),"Universal Render Pipeline/Unlit");
            presenter.boardCamera=cam;
            presenter.viewsRoot=new GameObject("Views - pooled defenders and threats").transform;
            AssetDatabase.SaveAssets();
            composition.level=AssetDatabase.LoadAssetAtPath<LevelDefinitionSO>(Base+"Data/Levels/VillageForestEdge.asset");
            composition.cards=AssetDatabase.LoadAssetAtPath<CardCatalogSO>(Base+"Data/Cards/CardCatalog.asset");
            composition.visuals=AssetDatabase.LoadAssetAtPath<VisualCatalogSO>(Base+"Data/Visuals/VisualCatalog.asset");
            composition.provider=AssetDatabase.LoadAssetAtPath<ProviderProfileSO>(Base+"Data/Providers/Apertus.asset");
            composition.presenter=presenter;
            presenter.level=composition.level;
            presenter.cards=composition.cards;
            presenter.visuals=composition.visuals;
            composition.Validate();
            EditorSceneManager.SaveScene(scene,ScenePath);
            Debug.Log("FORESTER_SCENE_BUILT");
        }
        [MenuItem("Forester/Validate dedicated scene")]public static void Validate() {
            EditorSceneManager.OpenScene(ScenePath);
            var root=UnityEngine.Object.FindAnyObjectByType<ForesterCompositionRoot>();
            if(!root)throw new ArgumentException("Missing Forester root");
            root.Validate();
            Debug.Log("FORESTER_VALIDATION_PASS");
        }
        public static void BuildEnhancedStandalone() {
            Rebuild();
            BuildStandalone();
        }
        public static void BuildStandalone() {
            Validate();
            Directory.CreateDirectory("Builds");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[] {
                    ScenePath
                },locationPathName="Builds/Forester.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None
            });
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Forester build failed");
            File.WriteAllText("Builds/forester_build_result.json","{\"result\":\"Succeeded\",\"errors\":"+report.summary.totalErrors+",\"warnings\":"+report.summary.totalWarnings+"}");
            Debug.Log("FORESTER_BUILD_PASS");
        }
    }
}
