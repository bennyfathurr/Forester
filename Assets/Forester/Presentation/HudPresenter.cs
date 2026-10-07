using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Forester.Domain;
using Forester.Application;
namespace Forester.Presentation {
    public sealed class HudPresenter:MonoBehaviour {
        public Canvas canvas;
        public CardTrayPresenter tray;
        public Button start,pause,cancel,move,sell;
        public Text stats,status,message,details;
        public GameObject top,cardPanel,detailsPanel,modal;
        Transform root;
        GamePresenter owner;
        public InputField endpoint,model,token;
        ProviderMode selectedMode;
        bool schema,temp,maxTokens;
        Text settingsState;
        public void Build(GamePresenter game) {
            owner=game;
            var scaler=canvas.GetComponent<CanvasScaler>();
            if(scaler) {
                scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1280,900);
                scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            }
            var layout=CanvasWidgets.Rect("Safe HUD layout",canvas.transform,0,0,1280,900);
            layout.anchorMin=layout.anchorMax=layout.pivot=new Vector2(.5f,.5f);
            layout.anchoredPosition=Vector2.zero;
            root=layout;
            top=CanvasWidgets.PanelAt("TopHUD",root,16,16,1248,74);
            stats=CanvasWidgets.Label("Match stats",top.transform,20,12,1040,30,21);
            status=CanvasWidgets.Label("AI status",top.transform,20,43,930,23,15);
            pause=CanvasWidgets.Button("Pause [Esc]",top.transform,1080,16,150,40,game.TogglePause);
            message=CanvasWidgets.Label("Placement status",root,30,105,1200,45,18);
            cardPanel=CanvasWidgets.PanelAt("CardTray",root,16,710,1248,174);
            tray=gameObject.AddComponent<CardTrayPresenter>();
            tray.Build(cardPanel.transform,game.cards,game.visuals,game.SelectCard);
            start=CanvasWidgets.Button("Start Wave",cardPanel.transform,1060,16,174,60,game.StartWave);
            cancel=CanvasWidgets.Button("Cancel planning",cardPanel.transform,1060,88,174,52,game.CancelPlanning);
            detailsPanel=CanvasWidgets.PanelAt("PlacementDetails",root,20,638,850,62);
            details=CanvasWidgets.Label("Defender detail",detailsPanel.transform,14,8,545,48,14);
            move=CanvasWidgets.Button("Move",detailsPanel.transform,560,10,120,42,game.BeginMove);
            sell=CanvasWidgets.Button("Sell",detailsPanel.transform,700,10,120,42,game.SellSelected);
            var controls=CanvasWidgets.Label("Camera controls",root,30,148,1200,22,12);
            controls.text="CAMERA  Right drag: orbit  ·  Middle drag / arrows: pan  ·  Scroll: zoom  ·  Q / E: rotate  ·  Home: reset";
            controls.raycastTarget=false;
            ShowMenu();
        }
        void CloseModal() {
            if(modal) {
                var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
                if(selected&&selected.transform.IsChildOf(modal.transform))EventSystem.current.SetSelectedGameObject(null);
                modal.SetActive(false);
                Destroy(modal);
            }
            modal=null;
        }
        GameObject Modal(string title,float height=420) {
            CloseModal();
            modal=CanvasWidgets.PanelAt("Modal",root,350,180,580,height);
            var t=CanvasWidgets.Label("Heading",modal.transform,28,24,524,65,28);
            t.text=title;
            return modal;
        }
        public void ShowMenu() {
            top.SetActive(false);
            cardPanel.SetActive(false);
            detailsPanel.SetActive(false);
            message.text="";
            CloseModal();
            modal=CanvasWidgets.PanelAt("Forest briefing",root,36,185,405,466);
            var m=modal;
            CanvasWidgets.Label("Eyebrow",m.transform,28,28,350,26,14).text="TACTICAL FOREST DEFENSE";
            var title=CanvasWidgets.Label("Title",m.transform,26,68,355,60,43);
            title.fontStyle=FontStyle.Bold;
            title.text="FORESTER";
            var subtitle=CanvasWidgets.Label("Location",m.transform,28,133,350,30,21);
            subtitle.color=CanvasWidgets.Accent;
            subtitle.text="Village Forest Edge";
            CanvasWidgets.Label("Briefing",m.transform,28,188,350,90,18).text="Protect the forest. Build your team.\nContain three waves along two trails.\nEvery placement matters.";
            CanvasWidgets.Button("DEPLOY TEAM  >",m.transform,28,306,349,54,()=> {
                CloseModal(); owner.StartMatch();
            });
            CanvasWidgets.Button("AI connection settings",m.transform,28,378,349,42,()=>ShowSettings(false));
        }

        public void EnterMatch() {
            CloseModal();
            top.SetActive(true);
            cardPanel.SetActive(true);
        }
        public void ShowPause() {
            var m=Modal("Paused",400);
            CanvasWidgets.Button("Resume",m.transform,28,102,524,46,()=> {
                CloseModal();      owner.TogglePause();
            });
            CanvasWidgets.Button("Restart match",m.transform,28,164,524,46,()=> {
                CloseModal();      owner.StartMatch();
            });
            CanvasWidgets.Button("Connection settings",m.transform,28,226,524,46,()=>ShowSettings(true));
            CanvasWidgets.Button("Return to menu",m.transform,28,288,524,46,owner.ReturnToMenu);
        }
        public void DismissPause()=>CloseModal();
        public void ShowResults(SimulationWorld w) {
            var m=Modal(w.Board.Phase==Phase.Won?"Forest protected":w.Board.Phase==Phase.Lost?"Forest gate lost":"Simulation stopped",440);
            var t=CanvasWidgets.Label("Results",m.transform,28,100,524,210,17);
            t.text=$"Integrity {w.Integrity}/{w.Board.Level.Integrity} · spent {w.TotalSpent} PP\nPlacements {w.TotalPlacements} · waves finished {w.History.Count}\n"+string.Join("\n",w.History.Select(h=>$"Wave {h.Wave}: {h.Contained} contained, {h.Leaked} leaked"))+"\n"+w.Error;
            CanvasWidgets.Button("Restart",m.transform,28,322,252,50,owner.StartMatch);
            CanvasWidgets.Button("Menu",m.transform,298,322,252,50,owner.ReturnToMenu);
        }
        public void Refresh(MatchController c,string selected,long defender,string feedback) {
            var w=c.World;
            stats.text=$"FORESTER    Integrity {w.Integrity}/{w.Board.Level.Integrity}    {w.Board.PP} PP    Wave {w.Wave}/{w.Board.Level.Waves.Length}    {w.Board.Phase}    Remaining {w.Remaining}";
            status.text=c.Decisions.Status+"  ·  "+(w.Board.Phase==Phase.Telegraph?w.Plan.Announcement+" · "+string.Join(", ",w.Plan.Schedule.GroupBy(e=>e.RouteId).Select(g=>g.Key+" "+g.Count()))+" "+string.Join(", ",w.Plan.ConditionSchedule.Select(condition=>condition.Id+" @"+(condition.StartTick/20)+"s")):w.ConditionNotice());
            message.text=feedback;
            tray.Refresh(w.Board,selected);
            start.interactable=w.Board.Phase==Phase.Preparation&&!w.Board.Paused&&w.Board.Defenders.Values.Any(d=>w.Board.Catalog.Defenders[d.DefinitionId].Kind!=CombatKind.Aura);
            start.gameObject.SetActive(w.Board.Phase!=Phase.Planning);
            cancel.gameObject.SetActive(w.Board.Phase==Phase.Planning);
            bool show=w.Board.Defenders.TryGetValue(defender,out var d);
            detailsPanel.SetActive(show);
            if(show) {
                var card=w.Board.Catalog.Cards[d.CardId];
                details.text=card.Name+" · "+d.Cell.Id+"\n"+card.Description+" · Sell refund "+Math.Floor(d.Paid*.75)+" PP";
                move.interactable=!w.Board.Paused&&w.Board.Level.Permissions.Move.Contains(w.Board.Phase);
                sell.interactable=!w.Board.Paused&&w.Board.Level.Permissions.Sell.Contains(w.Board.Phase);
            }
        }
        void ShowSettings(bool fromPause) {
            var m=Modal("AI connection",510);
            var settings=owner.Settings;
            selectedMode=settings.Mode;
            schema=settings.VerifiedSchemaOutput;
            temp=settings.SendTemperature;
            maxTokens=settings.SendMaxTokens;
            settingsState=CanvasWidgets.Label("Mode",m.transform,28,84,524,40,16);
            Action refresh=()=>settingsState.text=$"Mode: {selectedMode} · Schema: {schema} · temperature: {temp} · tokens: {maxTokens}";
            refresh();
            CanvasWidgets.Button("Cycle mode",m.transform,28,130,252,36,()=> {
                selectedMode=(ProviderMode)(((int)selectedMode+1)%3);      refresh();
            });
            CanvasWidgets.Button("Verified schema: toggle",m.transform,298,130,252,36,()=> {
                schema=!schema;      refresh();
            });
            CanvasWidgets.Label("EndpointLabel",m.transform,28,180,524,24,15).text="Endpoint (local chat or remote /forester/plan)";
            endpoint=CanvasWidgets.Input("Endpoint",m.transform,28,206,524,settings.Endpoint);
            CanvasWidgets.Label("ModelLabel",m.transform,28,250,524,24,15).text="Apertus model ID";
            model=CanvasWidgets.Input("Model",m.transform,28,276,524,settings.Model);
            CanvasWidgets.Label("TokenLabel",m.transform,28,320,524,24,15).text="Gateway session token (memory only)";
            token=CanvasWidgets.Input("SessionToken",m.transform,28,346,524,settings.SessionToken,true);
            CanvasWidgets.Button("Probe plan",m.transform,28,405,162,42,()=> {
                Save();      owner.Probe(result=> {
                    if(settingsState)settingsState.text=result;
                });
            });
            CanvasWidgets.Button("Save for next match",m.transform,205,405,172,42,()=> {
                Save();      if(fromPause)ShowPause();      else ShowMenu();
            });
            CanvasWidgets.Button("Optional params",m.transform,392,405,158,42,()=> {
                temp=!temp;      maxTokens=temp;      refresh();
            });
        }
        void Save() {
            owner.Settings.Mode=selectedMode;
            owner.Settings.Endpoint=endpoint.text.Trim();
            owner.Settings.Model=model.text.Trim();
            owner.Settings.SessionToken=token.text;
            owner.Settings.VerifiedSchemaOutput=schema;
            owner.Settings.SendTemperature=temp;
            owner.Settings.SendMaxTokens=maxTokens;
        }
    }
}
