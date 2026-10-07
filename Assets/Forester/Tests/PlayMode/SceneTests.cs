using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Forester.Domain;
using Forester.Presentation;
namespace Forester.Tests {
    public sealed class SceneTests {
        static IEnumerator Load() {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Forester/Scenes/VillageForestEdge.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("VillageForestEdge");
#endif
            yield return null;
        }
        [UnityTest]public IEnumerator CardsGhostMouseMoveSellPauseRestart() {
            yield return Load();
            var p=Object.FindAnyObjectByType<GamePresenter>();
            Assert.NotNull(p);
            p.StartMatch();
            yield return null;
            Assert.That(p.hud.tray.Buttons.Count,Is.EqualTo(5));
            p.hud.tray.Buttons["card_water_team"].onClick.Invoke();
            Assert.That(p.SelectedCard,Is.EqualTo("card_water_team"));
            Assert.That(p.PreviewCell(new Cell(2,8)).Success,Is.True);
            Assert.NotNull(p.Ghost);
            Assert.That(p.PreviewCell(new Cell(0,9)).Success,Is.False);
            var mouse=InputSystem.AddDevice<Mouse>();
            var keyboard=InputSystem.AddDevice<Keyboard>();
            var oldFocus=InputSystem.settings.backgroundBehavior;
            var oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            try {
                var pointer=(Vector2)p.boardCamera.WorldToScreenPoint(new Vector3(4,0,16));
                InputState.Change(mouse,new MouseState {
                    position=pointer
                }.WithButton(MouseButton.Left));
                mouse.MakeCurrent();
                keyboard.MakeCurrent();
                p.ProcessInput();
                Assert.That(p.Controller.World.Board.Defenders.Count,Is.EqualTo(1),$"Raycast placement pointer={pointer} hovered={p.HoveredCell?.Id} message={p.Message}");
                Assert.That(p.Controller.World.Board.PP,Is.EqualTo(85));
                InputState.Change(mouse,new MouseState {
                    position=pointer
                });
                InputSystem.Update();
                yield return null;
                p.SelectCard("card_watch_post");
                p.ClickCell(new Cell(4,8));
                p.StartWave();
                Assert.That(p.Controller.World.Board.Phase,Is.EqualTo(Phase.Planning));
                Assert.That(p.Controller.Placement.Move(new MoveDefenderCommand(1,new Cell(6,8))).Success,Is.False);
                p.CancelPlanning();
                p.TogglePause();
                int tick=p.Controller.World.Tick;
                yield return null;
                Assert.That(p.Controller.World.Tick,Is.EqualTo(tick));
                Assert.That(p.Controller.World.Board.Paused,Is.True);
                p.TogglePause();
                p.StartMatch();
                Assert.That(p.Controller.World.Board.PP,Is.EqualTo(120));
                Assert.That(p.Controller.World.Board.Defenders.Count,Is.Zero);
                p.ReturnToMenu();
                yield return null;
                Assert.That(Object.FindObjectsByType<EntityView>(FindObjectsSortMode.None).Length,Is.GreaterThanOrEqualTo(1));
            }
            finally {
                InputSystem.settings.backgroundBehavior=oldFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
                p.ReturnToMenu();
            }
        }
        [UnityTest]public IEnumerator CompleteDefaultThreeWaves() {
            yield return Load();
            var p=Object.FindAnyObjectByType<GamePresenter>();
            p.StartMatch();
            Place(p,"card_watch_post",new Cell(4,8));
            Place(p,"card_watch_post",new Cell(8,5));
            Place(p,"card_watch_post",new Cell(12,4));
            Place(p,"card_water_team",new Cell(6,3));
            for(int wave=1;      wave<=3;      wave++) {
                if(wave==2)Place(p,"card_foam_station",new Cell(6,5));
                if(wave==3) {
                    Place(p,"card_water_team",new Cell(6,8));
                    Place(p,"card_water_tank",new Cell(10,7));
                }
                p.hud.start.onClick.Invoke();
                yield return null;
                Assert.That(p.Controller.World.Board.Phase,Is.EqualTo(Phase.Telegraph));
                yield return new WaitForSecondsRealtime(3.1f);
                Assert.That(p.Controller.World.Board.Phase,Is.EqualTo(Phase.Active));
                for(int i=0;      i<2400&&p.Controller.World.Board.Phase==Phase.Active;      i++)p.Controller.World.Advance();
                Assert.That(p.Controller.World.Board.Phase,Is.EqualTo(Phase.Cleanup),"Default tuning survives wave "+wave);
                p.Controller.Update(0);
                yield return null;
            }
            Assert.That(p.Controller.World.Board.Phase,Is.EqualTo(Phase.Won));
            Assert.That(p.Controller.World.Integrity,Is.GreaterThan(0));
            Assert.That(p.Controller.World.History.Count,Is.EqualTo(3));
            p.ReturnToMenu();
            yield return null;
        }
        [UnityTest]public IEnumerator OrbitZoomResetAndPlacementStayIndependent() {
            yield return Load();
            var p=Object.FindAnyObjectByType<GamePresenter>();
            p.StartMatch();
            var camera=p.boardCamera.GetComponent<BoardCamera>();
            var original=p.boardCamera.transform.position;
            camera.Orbit(new Vector2(500,10000));
            Assert.That(camera.Pitch,Is.EqualTo(25));
            camera.Zoom(10000);
            Assert.That(camera.Distance,Is.GreaterThan(0));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(Vector3.Distance(original,p.boardCamera.transform.position),Is.GreaterThan(1));
            var position=AssetViewRegistry.World(p.Controller.World.Board.Level.Position(new Cell(8,5)));
            var ray=p.boardCamera.ScreenPointToRay(p.boardCamera.WorldToScreenPoint(position));
            Assert.That(Physics.Raycast(ray,out var hit,500,1<<30),Is.True);
            Assert.That(hit.collider.GetComponent<GridCellView>().cell,Is.EqualTo(new Cell(8,5)));
            Place(p,"card_watch_post",new Cell(8,5));
            Assert.That(p.Controller.World.Board.PP,Is.EqualTo(95));
            camera.ResetView();
            Assert.That(Vector3.Distance(original,p.boardCamera.transform.position),Is.LessThan(.01f));
            p.StartMatch();
            Assert.That(p.Controller.World.Board.Defenders.Count,Is.Zero);
            p.ReturnToMenu();
        }
        [UnityTest]public IEnumerator FullFrameCameraAndSafeHud() {
            yield return Load();
            var p=Object.FindAnyObjectByType<GamePresenter>();
            Assert.That(p.boardCamera.rect,Is.EqualTo(new Rect(0,0,1,1)),"Every output pixel must be cleared");
            Assert.That(p.boardCamera.clearFlags,Is.EqualTo(CameraClearFlags.SolidColor));
            var scaler=p.hud.canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            Assert.That(scaler.screenMatchMode,Is.EqualTo(UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand));
            p.StartMatch();
            Canvas.ForceUpdateCanvases();
            var layout=p.hud.canvas.transform.Find("Safe HUD layout") as RectTransform;
            Assert.NotNull(layout);
            var corners=new Vector3[4];
            layout.GetWorldCorners(corners);
            foreach(var corner in corners) {
                Assert.That(corner.x,Is.InRange(-1f,Screen.width+1f));
                Assert.That(corner.y,Is.InRange(-1f,Screen.height+1f));
            }
            p.ReturnToMenu();
        }
        [UnityTest]public IEnumerator FeedbackBurstUsesTintAndClearsOnRestart() {
            yield return Load();
            var p=Object.FindAnyObjectByType<GamePresenter>();
            p.StartMatch();
            Place(p,"card_water_team",new Cell(2,8));
            var feedback=p.GetComponent<BoardFeedback>();
            var lines=feedback.GetComponentsInChildren<LineRenderer>();
            Assert.That(lines.Length,Is.GreaterThan(0));
            var block=new MaterialPropertyBlock();
            lines[0].GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor").g,Is.GreaterThan(.5f));
            Assert.That(feedback.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(6));
            p.StartMatch();
            Assert.That(feedback.GetComponentsInChildren<LineRenderer>().Length,Is.Zero);
            Assert.That(feedback.GetComponentsInChildren<MeshRenderer>().Length,Is.Zero);
            p.ReturnToMenu();
        }
        static void Place(GamePresenter p,string card,Cell cell) {
            p.SelectCard(card);
            p.ClickCell(cell);
            Assert.That(p.Controller.World.Board.Occupancy.ContainsKey(cell),Is.True,p.Message);
        }
        [UnityTest]public IEnumerator UnreachableLocalFallbackAndCancellation() {
            yield return Load();
            var p=Object.FindAnyObjectByType<GamePresenter>();
            p.Settings.Mode=ProviderMode.LocalApertus;
            p.Settings.Endpoint="http://127.0.0.1:1/v1/chat/completions";
            p.Settings.Timeout=.2;
            p.StartMatch();
            Place(p,"card_watch_post",new Cell(4,8));
            p.StartWave();
            float deadline=Time.realtimeSinceStartup+2;
            while(p.Controller.World.Board.Phase==Phase.Planning&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(p.Controller.World.Board.Phase,Is.EqualTo(Phase.Telegraph));
            Assert.That(p.Controller.Decisions.Status,Does.Contain("fallback"));
            p.StartMatch();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(p.Controller.World.Board.Phase,Is.EqualTo(Phase.Preparation));
            Assert.That(p.Controller.World.Board.Defenders.Count,Is.Zero);
            p.ReturnToMenu();
            yield return null;
        }
    }
}
