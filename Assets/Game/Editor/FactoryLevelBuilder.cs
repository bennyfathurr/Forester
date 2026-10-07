using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using RATF.Domain;
using RATF.Presentation;
using RATF.Composition;
namespace RATF.Editor {
    public static class FactoryLevelBuilder {
        public const string ScenePath="Assets/Game/Scenes/FactoryOutskirts.unity";
        [MenuItem("Rage Against Factory/Build dedicated scene")]  public static void BuildInteractive() {
            if(File.Exists(ScenePath)&&!EditorUtility.DisplayDialog("Replace generated scene?","Only the dedicated FactoryOutskirts scene will be replaced. Wrapper/data assets are preserved.","Replace","Cancel"))return;
            Build(true);
        }
        public static void BuildIfMissing() {
            Build(false);
        }
        static T Asset<T>(string path) where T:ScriptableObject {
            var a=AssetDatabase.LoadAssetAtPath<T>(path);
            if(a!=null)return a;
            a=ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a,path);
            return a;
        }
        static Material Material(string name,Color color) {
            string path="Assets/Game/Data/Visuals/"+name+".mat";
            var a=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(a!=null)return a;
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            a=new Material(shader) {
                name=name,color=color
            };
            AssetDatabase.CreateAsset(a,path);
            return a;
        }
        static GameObject Cube(string name,Transform parent,Vector3 p,Vector3 scale,Material material) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=name;
            go.transform.SetParent(parent);
            go.transform.position=p;
            go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static TextMesh Label(string text,Transform parent,Vector3 p,float size= .2f) {
            var go=new GameObject(text);
            go.transform.SetParent(parent);
            go.transform.position=p;
            var label=go.AddComponent<TextMesh>();
            label.text=text;
            label.characterSize=size;
            label.fontSize=36;
            label.anchor=TextAnchor.MiddleCenter;
            label.color=Color.white;
            go.transform.rotation=Quaternion.Euler(52,0,0);
            return label;
        }
        static readonly string[] ids= {
            "villager_emak","villager_bapak","villager_anak","security_robot","foam_cannon","bolt_turret","gate","refinery"
        };
        static readonly string[] names= {
            "character-female-a","character-male-a","character-male-b","enemy-ufo-a","weapon-cannon","weapon-turret","wood-structure","tower-square-build-f"
        };
        public static void Build(bool replace) {
            if(File.Exists(ScenePath)&&!replace) {
                Validate();
                return;
            }
            foreach(var dir in new[] {
                "Data/Units","Data/Structures","Data/Levels","Data/AI","Data/Visuals","Prefabs/Views","Scenes"
            })Directory.CreateDirectory("Assets/Game/"+dir);
            AssetDatabase.Refresh();
            var catalog=Asset<VisualCatalogSO>("Assets/Game/Data/Visuals/VisualCatalog.asset");
            if(catalog.bindings==null||catalog.bindings.Length==0) {
                var bindings=new List<VisualBinding>();
                for(int i=0;    i<ids.Length;    i++) {
                    string pack=i<3?"kenney_mini-characters":"kenney_tower-defense-kit";
                    string source=$"Assets/3D/{pack}/Models/FBX format/{names[i]}.fbx";
                    var model=AssetDatabase.LoadAssetAtPath<GameObject>(source);
                    if(model==null)throw new InvalidOperationException("Missing source FBX: "+source);
                    string prefabPath=$"Assets/Game/Prefabs/Views/{ids[i]}.prefab";
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if(prefab==null) {
                        var wrapper=new GameObject(ids[i]);
                        var view=wrapper.AddComponent<UnitView>();
                        var visual=new GameObject("VisualRoot");
                        visual.transform.SetParent(wrapper.transform,false);
                        var child=(GameObject)PrefabUtility.InstantiatePrefab(model);
                        child.transform.SetParent(visual.transform,false);
                        var renderers=child.GetComponentsInChildren<Renderer>();
                        var bounds=new Bounds();
                        bool first=true;
                        foreach(var r in renderers) {
                            if(first) {
                                bounds=r.bounds;
                                first=false;
                            }
                            else bounds.Encapsulate(r.bounds);
                        }
                        float size=Mathf.Max(.01f,bounds.size.y);
                        float desired=i==2?.65f:i<4?1:i<6?.9f:2.1f;
                        float factor=desired/size;
                        visual.transform.localScale=Vector3.one*factor;
                        child.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
                        view.VisualRoot=visual.transform;
                        var marker=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        marker.name="TeamMarker";
                        marker.transform.SetParent(wrapper.transform,false);
                        marker.transform.localScale=new Vector3(.65f,.02f,.65f);
                        marker.transform.localPosition=new Vector3(0,.035f,0);
                        UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
                        marker.GetComponent<Renderer>().sharedMaterial=Material(i<3?"VillageMarker":"FactoryMarker",i<3?new Color(1,.43f,.12f):new Color(.1f,.7f,.95f));
                        view.TeamMarker=marker.GetComponent<Renderer>();
                        view.Label=Label("",wrapper.transform,new Vector3(0,desired+.35f,0),.1f);
                        prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,prefabPath);
                        UnityEngine.Object.DestroyImmediate(wrapper);
                    }
                    var portrait=AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/3D/{pack}/Previews/{names[i]}.png");
                    bindings.Add(new VisualBinding {
                        id=ids[i],prefab=prefab,portrait=portrait,status=i==2?"Scaled adult model supporter; static":i==3?"UFO security drone stand-in; static":i==7?"Tower refinery stand-in; static":"Mapped original FBX; static"
                    });
                }
                catalog.bindings=bindings.ToArray();
                EditorUtility.SetDirty(catalog);
            }
            var match=Asset<MatchDefinitionSO>("Assets/Game/Data/Levels/Match.asset");
            var level=Asset<LevelDefinitionSO>("Assets/Game/Data/Levels/FactoryOutskirts.asset");
            var profile=Asset<ProviderProfileSO>("Assets/Game/Data/AI/Provider.asset");
            var units=new List<UnitDefinitionSO>();
            foreach(var d in MatchDefinition.Units().Values) {
                string path="Assets/Game/Data/Units/"+d.Id+".asset";
                bool fresh=!File.Exists(path);
                var a=Asset<UnitDefinitionSO>(path);
                if(fresh) {
                    a.From(d);
                    EditorUtility.SetDirty(a);
                }
                units.Add(a);
            }
            AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var matchRoot=new GameObject("MatchRoot");
            var composition=matchRoot.AddComponent<GameCompositionRoot>();
            var presenter=matchRoot.AddComponent<BattlePresenter>();
            var audio=matchRoot.AddComponent<AudioPresenter>();
            presenter.audioPresenter=audio;
            presenter.catalog=catalog;
            presenter.viewsRoot=new GameObject("Views - pooled units and structures").transform;
            var world=new GameObject("World").transform;
            var ground=Material("Board",new Color(.13f,.17f,.22f));
            var laneMat=Material("Lane",new Color(.25f,.29f,.34f));
            var steel=Material("Steel",new Color(.4f,.5f,.6f));
            Cube("Board",world,new Vector3(13,-.22f,6),new Vector3(28,.4f,14),ground);
            var gateViews=new List<GateView>();
            for(int i=0;    i<3;    i++) {
                float y=level.laneY[i];
                Cube("L"+(i+1)+" LanePath",world,new Vector3(13,-.005f,y),new Vector3(25,.02f,1.6f),laneMat);
                Label("LANE "+(i+1),world,new Vector3(3,.05f,y+.9f),.15f);
                for(int j=0;    j<2;    j++) {
                    Cube($"L{i+1}_S{j+1} SlotMarker",world,new Vector3(level.padX[j],.03f,y+1),new Vector3(1.5f,.07f,1.1f),steel);
                    Label($"S{j+1}",world,new Vector3(level.padX[j],.1f,y+1),.12f);
                }
                var gateGo=new GameObject("Gate L"+(i+1));
                gateGo.transform.SetParent(world);
                gateGo.transform.position=new Vector3(level.gate,0,y);
                var gate=gateGo.AddComponent<GateView>();
                gate.lane=i+1;
                gate.visual=(GameObject)PrefabUtility.InstantiatePrefab(catalog.Find("gate").prefab);
                gate.visual.transform.SetParent(gateGo.transform,false);
                gate.visual.transform.localScale=new Vector3(.65f,.6f,.65f);
                gate.label=Label("GATE",gateGo.transform,new Vector3(level.gate,2,y),.14f);
                gateViews.Add(gate);
            }
            for(int i=0;    i<4;    i++)Cube("WallSegment",world,new Vector3(level.gate,.5f,1.5f+i*3),new Vector3(.7f,1,1.2f),steel);
            var refinery=(GameObject)PrefabUtility.InstantiatePrefab(catalog.Find("refinery").prefab);
            refinery.name="Shared refinery (tower asset stand-in)";
            refinery.transform.SetParent(world);
            refinery.transform.position=new Vector3(25,0,6);
            refinery.transform.localScale=new Vector3(1,1,2.2f);
            Label("REFINERY",world,new Vector3(25,3,6),.2f);
            var cameraGo=new GameObject("OrthographicCamera");
            var camera=cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<AudioListener>();
            camera.transform.position=new Vector3(13,18,-9);
            camera.transform.LookAt(new Vector3(13,0,6));
            camera.orthographic=true;
            camera.orthographicSize=8.2f;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.075f,.09f,.12f);
            camera.nearClipPlane=.1f;
            camera.farClipPlane=100;
            cameraGo.tag="MainCamera";
            presenter.boardCamera=camera;
            presenter.gates=gateViews.ToArray();
            var lightGo=new GameObject("Sun");
            var light=lightGo.AddComponent<Light>();
            light.type=LightType.Directional;
            light.intensity=1.25f;
            lightGo.transform.rotation=Quaternion.Euler(50,-35,0);
            RenderSettings.ambientLight=new Color(.6f,.65f,.7f);
            AssetDatabase.SaveAssets();
            composition.match=AssetDatabase.LoadAssetAtPath<MatchDefinitionSO>("Assets/Game/Data/Levels/Match.asset");
            composition.level=AssetDatabase.LoadAssetAtPath<LevelDefinitionSO>("Assets/Game/Data/Levels/FactoryOutskirts.asset");
            composition.units=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(MatchDefinition.Units().Keys,id=>AssetDatabase.LoadAssetAtPath<UnitDefinitionSO>("Assets/Game/Data/Units/"+id+".asset")));
            composition.provider=AssetDatabase.LoadAssetAtPath<ProviderProfileSO>("Assets/Game/Data/AI/Provider.asset");
            presenter.catalog=AssetDatabase.LoadAssetAtPath<VisualCatalogSO>("Assets/Game/Data/Visuals/VisualCatalog.asset");
            composition.presenter=presenter;
            composition.Validate();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene,ScenePath);
            Validate();
            Debug.Log("RATF_SCENE_BUILT "+ScenePath);
        }
        [MenuItem("Rage Against Factory/Validate scene and bindings")]  public static void Validate() {
            var catalog=AssetDatabase.LoadAssetAtPath<VisualCatalogSO>("Assets/Game/Data/Visuals/VisualCatalog.asset");
            if(catalog==null)throw new InvalidOperationException("Run scene builder first");
            catalog.Validate();
            foreach(var b in catalog.bindings)if(b.prefab.GetComponent<UnitView>()==null)throw new InvalidOperationException("Missing view: "+b.id);
            var scene=EditorSceneManager.OpenScene(ScenePath);
            foreach(var root in scene.GetRootGameObjects()) {
                var c=root.GetComponent<GameCompositionRoot>();
                if(c!=null) {
                    c.Validate();
                    Debug.Log("RATF_VALIDATION_PASS");
                    return;
                }
            }
            throw new InvalidOperationException("Missing composition root");
        }
        public static void BuildStandalone() {
            Validate();
            Directory.CreateDirectory("Builds");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[] {
                    ScenePath
                },locationPathName="Builds/RageAgainstFactory.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None
            });
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("RATF_BUILD_PASS");
        }
    }
}
