using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Forester.Domain;
using Forester.Application;
namespace Forester.Presentation {
    public sealed class GamePresenter:MonoBehaviour {
        public CardCatalogSO cards;
        public VisualCatalogSO visuals;
        public LevelDefinitionSO level;
        public Camera boardCamera;
        public Transform viewsRoot;
        public Material placementPreviewMaterial;
        public HudPresenter hud;
        public MatchController Controller {
            get;
            private set;
        }
        public ProviderSettings Settings=new ProviderSettings();
        public string SelectedCard=>selected;
        public string Message=>message;
        public Cell? HoveredCell {
            get;
            private set;
        }
        public GameObject Ghost=>ghost;
        Func<ProviderSettings,MatchController> create;
        Func<ProviderSettings,CancellationToken,Task<string>> probe;
        AssetViewRegistry registry;
        BoardFeedback feedback;
        CancellationTokenSource probeCancellation;
        string selected="",message="";
        long selectedDefender,moving;
        GameObject ghost;
        Material ghostMaterial;
        LineRenderer range;
        bool showingResult;
        Phase previousPhase;
        readonly List<(LineRenderer line,double expiry)> beams=new List<(LineRenderer,double)>();
        public void Initialize(Func<ProviderSettings,MatchController> factory,Func<ProviderSettings,CancellationToken,Task<string>> connection) {
            create=factory;
            probe=connection;
            feedback=gameObject.AddComponent<BoardFeedback>();
            feedback.Initialize(placementPreviewMaterial);
            hud.Build(this);
        }
        public void StartMatch() {
            CleanMatch();
            Controller=create(Settings);
            registry=new AssetViewRegistry(visuals,viewsRoot,feedback,boardCamera);
            selected="";
            selectedDefender=moving=0;
            message="Choose a card, then an outlined build cell. Placement is allowed before waves.";
            showingResult=false;
            previousPhase=Phase.Preparation;
            hud.EnterMatch();
            Refresh();
        }
        public void ReturnToMenu() {
            CleanMatch();
            hud.ShowMenu();
        }
        void CleanMatch() {
            probeCancellation?.Cancel();
            probeCancellation?.Dispose();
            probeCancellation=null;
            Controller?.Dispose();
            Controller=null;
            registry?.Dispose();
            registry=null;
            ClearGhost();
            foreach(var b in beams)if(b.line)Destroy(b.line.gameObject);
            beams.Clear();
            if(feedback)feedback.Clear();
        }
        void OnDestroy() {
            CleanMatch();
            if(ghostMaterial)Destroy(ghostMaterial);
        }
        void Update() {
            if(Controller==null)return;
            ProcessInput();
            Controller.Update(Time.deltaTime);
            feedback.Paused=Controller.World.Board.Paused;
            if(previousPhase!=Controller.World.Board.Phase) {
                previousPhase=Controller.World.Board.Phase;
                if(previousPhase==Phase.Active)message="Containment underway — defenses act automatically. Orbit or zoom to inspect the trails.";
                else if(previousPhase==Phase.Telegraph)message="Incoming wave — "+Controller.World.Plan.Announcement;
                else if(previousPhase==Phase.Cleanup)message="Wave contained. Preparing the next deployment.";
                if(previousPhase==Phase.Preparation&&Controller.World.History.Count>0)                     message=Controller.World.History.Last().Reflection+" "+CoverageGaps();
            }
            registry.Sync(Controller.World,Controller.Interpolation);
            RenderShots();
            Refresh();
            var phase=Controller.World.Board.Phase;
            if(!showingResult&&(phase==Phase.Won||phase==Phase.Lost||phase==Phase.Faulted)) {
                showingResult=true;
                ClearGhost();
                hud.ShowResults(Controller.World);
            }
        }
        string CoverageGaps() {
            var w=Controller.World;
            return string.Join(" · ",w.Paths.Routes.Values.Select(route=> {
                int gaps=0;                     for(int i=0;     i<20;     i++) {
                    var point=route.At(route.Length*(i+.5)/20);                         if(!w.Board.Defenders.Values.Any(d=>w.Board.Catalog.Defenders[d.DefinitionId].Kind!=CombatKind.Aura&&Point.Distance(point,w.Board.Level.Position(d.Cell))<=w.Board.Catalog.Defenders[d.DefinitionId].Range))gaps++;
                }
                return route.Definition.Id+" uncovered "+gaps+"/20 samples";
            }));
        }
        void Refresh() {
            if(Controller==null)return;
            if(Controller.World.Board.Phase!=Phase.Preparation&&!Controller.World.Board.Level.Permissions.Place.Contains(Controller.World.Board.Phase))ClearGhost();
            hud.Refresh(Controller,selected,selectedDefender,message);
        }
        public void SelectCard(string id) {
            if(Controller==null||!Controller.World.Board.Catalog.Cards.ContainsKey(id))return;
            selected=id;
            selectedDefender=moving=0;
            CreateGhost();
            message=Controller.World.Board.Catalog.Cards[id].Description;
            Refresh();
        }
        public void StartWave() {
            if(Controller?.Decisions.Start()==true) {
                selected="";
                moving=0;
                ClearGhost();
                message="Planning: preparation is locked. The accepted wave will be previewed for three seconds.";
                Refresh();
            }
        }
        public void CancelPlanning() {
            Controller?.Decisions.Cancel();
            message="Returned to preparation.";
            Refresh();
        }
        public void TogglePause() {
            if(Controller==null||showingResult)return;
            if(Controller.World.Board.Paused) {
                Controller.Decisions.Resume();
                hud.DismissPause();
            }
            else {
                Controller.Decisions.Pause();
                ClearGhost();
                hud.ShowPause();
            }
            Refresh();
        }
        public void BeginMove() {
            if(Controller==null||!Controller.World.Board.Defenders.TryGetValue(selectedDefender,out var d))return;
            moving=d.Id;
            selected=d.CardId;
            CreateGhost();
            message="Choose the destination. A rejected move keeps the original cell.";
        }
        public void SellSelected() {
            if(Controller==null)return;
            var result=Controller.Placement.Sell(new SellDefenderCommand(selectedDefender));
            message=result.Success?"Defender sold; original paid price refunded at 75%.":result.Error;
            if(result.Success)selectedDefender=0;
            Refresh();
        }
        public void ClickCell(Cell cell) {
            if(Controller==null)return;
            var board=Controller.World.Board;
            if(!string.IsNullOrEmpty(selected)) {
                var result=moving==0?Controller.Place(new PlaceCardCommand(selected,cell)):Controller.Placement.Move(new MoveDefenderCommand(moving,cell));
                message=result.Success?"Placed on "+cell.Id:result.Error;
                if(result.Success) {
                    feedback?.Emit(AssetViewRegistry.World(board.Level.Position(cell)),new Color(.3f,1,.7f),1.7f);
                    selectedDefender=result.EntityId;
                    moving=0;
                    selected="";
                    ClearGhost();
                }
            }
            else if(board.Occupancy.TryGetValue(cell,out var id))selectedDefender=id;
            else selectedDefender=0;
            Refresh();
        }
        public void ProcessInput() {
            if(Controller==null)return;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true) {
                TogglePause();
                return;
            }
            if(Controller.World.Board.Paused)return;
            if(Keyboard.current?.spaceKey.wasPressedThisFrame==true)StartWave();
            var mouse=Mouse.current;
            if(mouse==null)return;
            Vector2 pointer=mouse.position.ReadValue();
            if(OverUI(pointer)||mouse.rightButton.isPressed||mouse.middleButton.isPressed) {
                HoveredCell=null;
                if(ghost)ghost.SetActive(false);
                return;
            }
            if(Physics.Raycast(boardCamera.ScreenPointToRay(pointer),out var hit,500,1<<30)) {
                var cell=hit.collider.GetComponent<GridCellView>();
                HoveredCell=cell?.cell;
                if(cell!=null) {
                    PreviewCell(cell.cell);
                    if(mouse.leftButton.wasPressedThisFrame)ClickCell(cell.cell);
                }
            }
            else {
                HoveredCell=null;
                if(ghost)ghost.SetActive(false);
            }
        }
        bool OverUI(Vector2 position) {
            if(EventSystem.current==null)return false;
            var data=new PointerEventData(EventSystem.current) {
                position=position
            };
            var results=new List<RaycastResult>();
            EventSystem.current.RaycastAll(data,results);
            return results.Count>0;
        }
        public PlacementResult PreviewCell(Cell cell) {
            if(Controller==null||string.IsNullOrEmpty(selected))return PlacementResult.Reject("Select a card");
            var result=Controller.Placement.Validator.Preview(selected,cell,moving);
            if(ghost) {
                ghost.SetActive(true);
                ghost.transform.position=AssetViewRegistry.World(Controller.World.Board.Level.Position(cell),.05f);
                ghostMaterial.color=result.Success?new Color(.1f,1,.6f):new Color(1,.16f,.16f);
                range.startColor=range.endColor=ghostMaterial.color;
            }
            message=result.Success?"Click to "+(moving==0?"place":"move")+" · "+cell.Id:result.Error;
            return result;
        }
        void CreateGhost() {
            ClearGhost();
            if(Controller==null)return;
            var d=Controller.World.Board.Catalog.Defenders[Controller.World.Board.Catalog.Cards[selected].DefenderId];
            ghost=Instantiate(visuals.Find(d.VisualId).prefab,viewsRoot);
            ghost.name="PlacementGhost";
            if(!ghostMaterial)ghostMaterial=new Material(placementPreviewMaterial);
            foreach(var r in ghost.GetComponentsInChildren<Renderer>())r.sharedMaterial=ghostMaterial;
            foreach(var col in ghost.GetComponentsInChildren<Collider>())Destroy(col);
            var line=new GameObject("Range");
            line.transform.SetParent(ghost.transform,false);
            range=line.AddComponent<LineRenderer>();
            range.sharedMaterial=ghostMaterial;
            range.useWorldSpace=false;
            range.widthMultiplier=.045f;
            range.loop=true;
            range.positionCount=64;
            for(int i=0;      i<64;      i++) {
                float a=i*Mathf.PI*2/64;
                range.SetPosition(i,new Vector3(Mathf.Cos(a)*(float)d.Range,.08f,Mathf.Sin(a)*(float)d.Range));
            }
            ghost.SetActive(false);
        }
        void ClearGhost() {
            if(ghost)Destroy(ghost);
            ghost=null;
            range=null;
        }
        void RenderShots() {
            double now=Time.realtimeSinceStartupAsDouble;
            foreach(var beam in beams.Where(b=>b.expiry<now).ToArray()) {
                if(beam.line)Destroy(beam.line.gameObject);
                beams.Remove(beam);
            }
            foreach(var shot in Controller.FrameShots) {
                var go=new GameObject("Cosmetic containment beam");
                go.transform.SetParent(viewsRoot);
                var lr=go.AddComponent<LineRenderer>();
                lr.sharedMaterial=feedback.Material;
                lr.startColor=lr.endColor=new Color(.3f,.85f,1);
                registry.Fire(shot.DefenderId,AssetViewRegistry.World(shot.To));
                feedback.Emit(AssetViewRegistry.World(shot.To),new Color(.4f,.9f,1),.65f,.25f);
                lr.positionCount=2;
                lr.widthMultiplier=.10f;
                lr.numCapVertices=4;
                lr.SetPosition(0,AssetViewRegistry.World(shot.From,.8f));
                lr.SetPosition(1,AssetViewRegistry.World(shot.To,.4f));
                beams.Add((lr,now+.12));
            }
            Controller.FrameShots.Clear();
        }
        public void Probe(Action<string> done) {
            probeCancellation?.Cancel();
            probeCancellation?.Dispose();
            probeCancellation=new CancellationTokenSource();
            _ = ProbeAsync(done,probeCancellation.Token);
        }
        async Task ProbeAsync(Action<string> done,CancellationToken ct) {
            try {
                done("Checking plan contract…");
                var result=await probe(Settings,ct);
                if(!ct.IsCancellationRequested&&this)done(result);
            }
            catch(Exception e) {
                if(!ct.IsCancellationRequested&&this)done("Probe failed: "+e.GetType().Name);
            }
        }
    }
}
