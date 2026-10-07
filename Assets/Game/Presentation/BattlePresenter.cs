using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using RATF.Domain;
using RATF.Application;
namespace RATF.Presentation {
    public sealed class ProviderSettings {
        public ProviderMode Mode;
        public string Endpoint="http://127.0.0.1:11434/api/chat",Model="",SessionToken="";
        public double Timeout=8;
        public int Interval=100;
    }
    public sealed class BattlePresenter:MonoBehaviour {
        public Camera boardCamera;
        public VisualCatalogSO catalog;
        public Transform viewsRoot;
        public GateView[] gates;
        public AudioPresenter audioPresenter;
        public string SelectedUnit=>selection;
        public int HoveredLane=>hoverLane;
        public string LastMessage=>message;
        public MatchController Controller {
            get;
            private set;
        }
        [NonSerialized] public ProviderSettings Settings=new ProviderSettings();
        Func<ProviderSettings,MatchController> create;
        Func<ProviderSettings,Task<string>> probe;
        AssetViewRegistry registry;
        string screen="menu",selection="",message="",confirm="",details="",probeResult="";
        bool tutorial,introSeen,reducedMotion,probing;
        float uiScale;
        GUIStyle title,label,small,card;
        Texture2D white;
        float alpha;
        GameObject ghost;
        Material ghostMaterial;
        int hoverLane;
        int tutorialStage;
        bool settingsFromPause;
        LaneSelectionView laneSelection;
        readonly Color orange=new Color(1,.47f,.12f),blue=new Color(.12f,.75f,.95f),panel=new Color(.045f,.07f,.12f,.97f);
        public void Initialize(Func<ProviderSettings,MatchController> create,Func<ProviderSettings,Task<string>> probe) {
            this.create=create;
            this.probe=probe;
            laneSelection=gameObject.AddComponent<LaneSelectionView>();
            catalog.Validate();
            reducedMotion=PlayerPrefs.GetInt("RATF.ReducedMotion",0)==1;
            introSeen=PlayerPrefs.GetInt("RATF.TutorialSeen",0)==1;
        }
        public void StartMatch() {
            registry?.Dispose();
            Controller?.Dispose();
            Controller=create(Settings);
            registry=new AssetViewRegistry(catalog,viewsRoot,boardCamera);
            screen="battle";
            selection="";
            message="";
            confirm="";
            tutorial=!introSeen;
            tutorialStage=0;
            if(tutorial)Controller.Pause(true);
        }
        public void DismissTutorial(bool remember=false) {
            tutorial=false;
            introSeen=true;
            tutorialStage=1;
            if(remember)PlayerPrefs.SetInt("RATF.TutorialSeen",1);
            if(Controller!=null)Controller.Pause(false);
        }
        public CommandResult Deploy(string id,int lane) {
            var r=Controller.Submit(new PlayerCommand(id,lane,rally:id=="rally"));
            message=r.Accepted?"Deployed on Lane "+lane:r.Reason;
            if(r.Accepted&&tutorialStage==1)tutorialStage=2;
            return r;
        }
        public void TogglePause() {
            if(Controller==null)return;
            if(screen=="pause") {
                Controller.Pause(false);
                screen="battle";
            }
            else if(screen=="battle") {
                Controller.Pause(true);
                screen="pause";
            }
        }
        public void ReturnToMenu() {
            registry?.Dispose();
            registry=null;
            Controller?.Dispose();
            Controller=null;
            screen="menu";
            selection="";
            if(ghost!=null)ghost.SetActive(false);
        }
        void Update() {
            ProcessFrame(Time.unscaledDeltaTime);
        }
        public void ProcessFrame(float delta) {
            if(boardCamera!=null)boardCamera.orthographicSize=Mathf.Max(8.2f,14f/Mathf.Max(.1f,boardCamera.aspect));
            if(Controller==null)return;
            var kb=Keyboard.current;
            var mouse=Mouse.current;
            if(kb!=null&&kb.escapeKey.wasPressedThisFrame) {
                if(selection!="")selection="";
                else if(tutorial)DismissTutorial(true);
                else if(screen=="battle"||screen=="pause")TogglePause();
            }
            if(screen=="battle"&&!tutorial) {
                if(kb!=null) {
                    if(kb.digit1Key.wasPressedThisFrame)selection="villager_emak";
                    if(kb.digit2Key.wasPressedThisFrame)selection="villager_bapak";
                    if(kb.digit3Key.wasPressedThisFrame)selection="villager_anak";
                    if(kb.spaceKey.wasPressedThisFrame)selection="rally";
                }
                if(mouse!=null&&mouse.rightButton.wasPressedThisFrame)selection="";
                hoverLane=0;
                if(mouse!=null) {
                    var position=mouse.position.ReadValue();
                    uiScale=Mathf.Min(Screen.width/1280f,Screen.height/900f);
                    float y=Screen.height-position.y;
                    if(y>120*uiScale&&y<Screen.height-210*uiScale&&details=="") {
                        hoverLane=laneSelection.Pick(boardCamera,position,Controller.World.Level.LaneY);
                        if(hoverLane>0&&selection!=""&&mouse.leftButton.wasPressedThisFrame)Deploy(selection,hoverLane);
                    }
                }
                Controller.Advance(delta);
                alpha=Mathf.Repeat(alpha+delta*20,1);
            }
            registry?.Sync(Controller.World,alpha,reducedMotion);
            audioPresenter.Events(Controller.World.Events);
            Controller.World.Events.Clear();
            foreach(var gate in gates)gate.Render(Controller.World.State.GateHp[gate.lane-1],Controller.World.Definition.GateHp,boardCamera);
            if(Controller.World.State.Lifecycle==Lifecycle.Won||Controller.World.State.Lifecycle==Lifecycle.Lost)screen="results";
            Ghost();
        }
        void Ghost() {
            if(selection==""||hoverLane==0||screen!="battle"||tutorial) {
                if(ghost!=null)ghost.SetActive(false);
                return;
            }
            if(ghost==null) {
                ghost=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ghost.name="Lane selection / spawn ghost";
                Destroy(ghost.GetComponent<Collider>());
                ghost.transform.localScale=new Vector3(1.2f,.025f,1.2f);
                ghostMaterial=new Material(catalog.Find("villager_emak").prefab.GetComponent<UnitView>().TeamMarker.sharedMaterial);
                ghostMaterial.color=orange;
                ghost.GetComponent<Renderer>().sharedMaterial=ghostMaterial;
            }
            ghost.SetActive(true);
            ghost.transform.position=new Vector3(Controller.World.Level.Spawn,.05f,Controller.World.Level.LaneY[hoverLane-1]);
        }
        void Styles() {
            if(white!=null)return;
            white=Texture2D.whiteTexture;
            title=new GUIStyle(GUI.skin.label) {
                fontSize=30,fontStyle=FontStyle.Bold,normal= {
                    textColor=Color.white
                }
            };
            label=new GUIStyle(GUI.skin.label) {
                fontSize=19,normal= {
                    textColor=Color.white
                }
            };
            small=new GUIStyle(GUI.skin.label) {
                fontSize=14,wordWrap=true,normal= {
                    textColor=new Color(.64f,.72f,.8f)
                }
            };
            card=new GUIStyle(GUI.skin.button) {
                fontSize=18,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperLeft,padding=new RectOffset(12,12,10,10),normal= {
                    textColor=Color.white,background=white
                },hover= {
                    textColor=Color.white,background=white
                },active= {
                    textColor=Color.white,background=white
                }
            };
        }
        void Box(Rect r,Color color) {
            var old=GUI.color;
            GUI.color=color.linear;
            GUI.DrawTexture(r,white);
            GUI.color=old;
        }
        bool Button(Rect r,string text) {
            var previous=GUI.backgroundColor;
            GUI.backgroundColor=new Color(.06f,.095f,.16f).linear;
            bool pressed=GUI.Button(r,text,card);
            GUI.backgroundColor=previous;
            return pressed;
        }
        void Text(Rect r,string text,GUIStyle style=null) {
            GUI.Label(r,text,style??label);
        }
        void OnGUI() {
            Styles();
            uiScale=Mathf.Min(Screen.width/1280f,Screen.height/900f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*uiScale)/2,0,0),Quaternion.identity,Vector3.one*uiScale);
            if(screen=="menu") {
                Box(new Rect(290,150,700,540),panel);
                Text(new Rect(340,200,640,50),"RAGE AGAINST THE FACTORY",title);
                Text(new Rect(340,255,620,60),"Breach the gates. Dismantle the machines.\nShut down the refinery before time expires.",small);
                if(Button(new Rect(340,340,600,64),"START PROTEST"))StartMatch();
                if(Button(new Rect(340,420,290,60),"HOW TO PLAY"))screen="help";
                if(Button(new Rect(650,420,290,60),"SETTINGS")) {
                    settingsFromPause=false;
                    screen="settings";
                }
                Text(new Rect(340,520,620,80),"Factory AI: "+Settings.Mode+"\nExisting Kenney assets • 3 lanes • offline ready",small);
                return;
            }
            if(screen=="help") {
                Box(new Rect(270,130,740,620),panel);
                Text(new Rect(310,175,660,45),"HOW TO PLAY",title);
                Text(new Rect(310,235,660,340),"1 / 2 / 3 select Emak, Bapak, or Anak. Click a lane to deploy.\n\nEmak absorbs pressure. Bapak deals double structure damage. Anak follows adults and boosts attack speed; children never attack or take damage.\n\nRage regenerates +6/s. Factory Oil +5/s. No kill rewards.\n\nSpace selects Rally: 35 Rage, adults in one lane move faster for 5s.\n\nRight-click cancels. Escape cancels selection, then pauses. Breached gates stay open. Win by reducing shared refinery HP to zero before six minutes.",label);
                if(Button(new Rect(310,650,660,55),"BACK"))screen="menu";
                return;
            }
            if(screen=="settings") {
                DrawSettings();
                return;
            }
            if(Controller==null)return;
            var s=Controller.World.State;
            var d=Controller.World.Definition;
            Box(new Rect(20,18,310,92),panel);
            Box(new Rect(20,18,5,92),orange);
            Text(new Rect(40,27,270,28),"VILLAGE RAGE   +6/s",small);
            Text(new Rect(40,56,270,36),$"{s.Rage/1000f:0} / {d.ResourceCap/1000}",title);
            HudPresenter.Bar(new Rect(40,95,260,5),s.Rage/(float)d.ResourceCap,orange,white);
            Box(new Rect(410,18,440,92),panel);
            int secs=Math.Max(0,(d.DurationTicks-s.Tick)/20);
            Text(new Rect(430,28,400,36),$"{secs/60:00}:{secs%60:00}   REFINERY {s.RefineryHp}",title);
            Text(new Rect(430,72,400,28),"BREACH GATES • BREAK THE FACTORY LINE",small);
            HudPresenter.Bar(new Rect(430,100,400,5),s.RefineryHp/(float)d.RefineryHp,blue,white);
            Box(new Rect(930,18,330,92),panel);
            Box(new Rect(1255,18,5,92),blue);
            Text(new Rect(950,27,270,28),"FACTORY OIL   +5/s",small);
            Text(new Rect(950,55,220,36),$"{s.Oil/1000f:0} / {d.ResourceCap/1000}",title);
            if(Button(new Rect(1190,56,55,42),"Ⅱ"))TogglePause();
            HudPresenter.Bar(new Rect(950,95,240,5),s.Oil/(float)d.ResourceCap,blue,white);
            Text(new Rect(30,120,900,25),"AI: "+Settings.Mode+" • "+Controller.AI.Status+"    "+message,small);
            Box(new Rect(220,690,840,195),panel);
            string[] ids= {
                "villager_emak","villager_bapak","villager_anak"
            };
            string[] names= {
                "EMAK • TANK","BAPAK • BREAKER","ANAK • SUPPORT"
            };
            for(int i=0;    i<3;    i++) {
                string id=ids[i];
                float x=240+i*215;
                var u=Controller.World.Units[id];
                if(selection==id)Box(new Rect(x-3,702,206,160),orange);
                if(Button(new Rect(x,705,200,155),names[i]+"\n"+u.Cost+" RAGE"))selection=id;
                var b=catalog.Find(id);
                if(b.portrait!=null)GUI.DrawTexture(new Rect(x+55,755,90,75),b.portrait,ScaleMode.ScaleToFit);
                Text(new Rect(x+10,836,150,25),u.Support?"AURA +15%":u.Hp+" HP • "+u.Damage+" ATK",small);
                if(GUI.Button(new Rect(x+164,837,30,22),"i"))details=id;
                if(s.Rage<u.Cost*1000||s.Tick<s.PlayerReady)Text(new Rect(x+10,807,150,25),s.Rage<u.Cost*1000?"WAIT FOR RAGE":"COOLDOWN",small);
            }
            if(Button(new Rect(905,735,130,75),"RALLY\n35 RAGE"))selection="rally";
            Text(new Rect(905,820,130,40),s.Tick<s.RallyReady?$"{(s.RallyReady-s.Tick)/20f:0}s":"READY • SPACE",small);
            if(selection!="")Text(new Rect(330,658,700,25),"Selected: "+selection+" • click Lane 1, 2 or 3 near its spawn",label);
            if(details!="") {
                Box(new Rect(360,230,560,350),panel);
                var u=Controller.World.Units[details];
                Text(new Rect(390,265,500,45),details.Replace("villager_","").ToUpper(),title);
                string desc=u.Support?"Noncombat supporter. No health, no attacks. Boosts nearby adults' attack speed by 15%, without stacking. Retreats near robots.":details=="villager_bapak"?"Structure breaker. Double damage to gates, machines and refinery. Vulnerable to robot interception.":"Durable frontline adult. Protects groups by standing nearest the enemy. No hidden defense aura.";
                Text(new Rect(390,320,500,150),desc,label);
                if(Button(new Rect(390,495,500,50),"CLOSE"))details="";
            }
            if(tutorial) {
                Box(new Rect(350,235,580,350),panel);
                Text(new Rect(385,270,510,45),"WELCOME TO THE PROTEST",title);
                Text(new Rect(385,330,510,130),"Select Emak (1), then deploy in Lane 2.\nAdd Bapak when Rage regenerates.\nFactory Oil pays for its response.\n\nThis introduction pauses the match.",label);
                if(Button(new Rect(385,490,510,55),"LET'S GO")) {
                    DismissTutorial(true);
                }
            }
            else if(tutorialStage==1)Text(new Rect(430,155,650,28),"Tutorial: select Emak and click Lane 2.",label);
            else if(tutorialStage==2)Text(new Rect(430,155,650,28),"Tutorial: wait for Rage and add Bapak. Oil funds the factory.",small);
            if(screen=="pause"||screen=="results") {
                Box(new Rect(380,210,520,410),panel);
                Text(new Rect(415,250,450,45),screen=="pause"?"PAUSED":s.Lifecycle==Lifecycle.Won?"REFINERY SHUT DOWN":"TIME EXPIRED",title);
                if(screen=="results")Text(new Rect(415,307,450,70),$"Elapsed {s.Tick/20f:0.0}s • Deployments {s.Deployments}\nRetreats {s.Retreats} • Gates breached {s.GatesBreached}",small);
                if(screen=="pause"&&Button(new Rect(415,315,450,50),"RESUME"))TogglePause();
                if(Button(new Rect(415,385,450,50),screen=="results"?"REPLAY":"RESTART")) {
                    if(screen=="results")StartMatch();
                    else confirm="restart";
                }
                if(Button(new Rect(415,447,215,50),"MENU")) {
                    if(screen=="results")ReturnToMenu();
                    else confirm="menu";
                }
                if(screen=="pause"&&Button(new Rect(645,447,220,50),"SETTINGS")) {
                    settingsFromPause=true;
                    screen="settings";
                }
                if(confirm!="") {
                    Text(new Rect(415,510,450,30),"Confirm "+confirm+"?",label);
                    if(Button(new Rect(415,550,215,45),"YES")) {
                        if(confirm=="restart")StartMatch();
                        else ReturnToMenu();
                        confirm="";
                    }
                    if(Button(new Rect(645,550,220,45),"CANCEL"))confirm="";
                }
            }
        }
        void DrawSettings() {
            Box(new Rect(280,100,720,690),panel);
            Text(new Rect(320,135,650,45),"SETTINGS",title);
            var modes=new[] {
                "SCRIPTED","LOCAL OLLAMA","HOSTED GATEWAY"
            };
            int mode=GUI.SelectionGrid(new Rect(320,195,640,50),(int)Settings.Mode,modes,3);
            if(mode!=(int)Settings.Mode) {
                Settings.Mode=(ProviderMode)mode;
                Settings.Endpoint=mode==2?"https://your-gateway.example/factory/decide":"http://127.0.0.1:11434/api/chat";
                Settings.Timeout=mode==2?4:8;
            }
            Text(new Rect(320,260,640,25),"Endpoint (HTTPS required for hosted)",small);
            Settings.Endpoint=GUI.TextField(new Rect(320,290,640,35),Settings.Endpoint);
            Text(new Rect(320,337,640,25),"Installed local model ID",small);
            Settings.Model=GUI.TextField(new Rect(320,367,640,35),Settings.Model);
            Text(new Rect(320,412,640,25),"Gateway session token (memory only; not a provider key)",small);
            Settings.SessionToken=GUI.PasswordField(new Rect(320,442,640,35),Settings.SessionToken,'*');
            reducedMotion=GUI.Toggle(new Rect(320,492,300,30),reducedMotion," Reduced motion");
            PlayerPrefs.SetInt("RATF.ReducedMotion",reducedMotion?1:0);
            Text(new Rect(630,488,150,30),"Audio",small);
            audioPresenter.volume=GUI.HorizontalSlider(new Rect(705,503,250,20),audioPresenter.volume,0,1);
            if(!probing&&Button(new Rect(320,540,640,50),"TEST CONNECTION + SCHEMA CONTRACT"))BeginProbe();
            Text(new Rect(320,603,640,80),probing?"Testing…":probeResult,small);
            if(Button(new Rect(320,710,640,50),"BACK"))screen=settingsFromPause?"pause":"menu";
            Text(new Rect(320,660,640,36),"Provider changes apply when the next match starts.",small);
        }
        async void BeginProbe() {
            if(probe==null)return;
            probing=true;
            try {
                probeResult=await probe(Settings);
            }
            catch(Exception e) {
                probeResult="Connection failed: "+e.Message;
            }
            finally {
                probing=false;
            }
        }
        void OnDestroy() {
            registry?.Dispose();
            Controller?.Dispose();
            if(ghost!=null)Destroy(ghost);
            if(ghostMaterial!=null)Destroy(ghostMaterial);
        }
    }
}
