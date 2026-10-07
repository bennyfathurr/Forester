using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using RATF.Presentation;
using RATF.Domain;
namespace RATF.Tests {
    public sealed class SceneTests {
        [UnityTest] public IEnumerator SceneDeployPauseRestartOutcome() {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Game/Scenes/FactoryOutskirts.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FactoryOutskirts");
#endif
            yield return null;
            var p=Object.FindAnyObjectByType<BattlePresenter>();
            Assert.NotNull(p);
            p.catalog.Validate();
            p.StartMatch();
            p.DismissTutorial();
            Assert.IsTrue(p.Deploy("villager_emak",2).Accepted);
            yield return null;
            Assert.AreEqual(1,p.Controller.Snapshot().Deployments);
            p.Controller.Pause(true);
            int tick=p.Controller.World.State.Tick;
            yield return null;
            Assert.AreEqual(tick,p.Controller.World.State.Tick);
            p.StartMatch();
            p.DismissTutorial();
            Assert.AreEqual(0,p.Controller.World.State.Entities.Count);
            for(int i=0;    i<7201&&p.Controller.World.State.Lifecycle==Lifecycle.Running;    i++)p.Controller.Advance(.05);
            Assert.AreEqual(Lifecycle.Lost,p.Controller.World.State.Lifecycle);
            p.ReturnToMenu();
            yield return null;
            Assert.AreEqual(4,Object.FindObjectsByType<UnitView>().Length);
        }
        [UnityTest] public IEnumerator KeyboardAndMouseDeploy() {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Game/Scenes/FactoryOutskirts.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FactoryOutskirts");
#endif
            yield return null;
            var p=Object.FindAnyObjectByType<BattlePresenter>();
            p.StartMatch();
            p.DismissTutorial();
            yield return null;
            var keyboard=InputSystem.AddDevice<Keyboard>();
            var mouse=InputSystem.AddDevice<Mouse>();
            var oldEditorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var oldBehavior=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            try {
                Vector2 point=p.boardCamera.WorldToScreenPoint(new Vector3(2,0,6));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit1));
                InputSystem.QueueStateEvent(mouse,new MouseState {
                    position=point
                }.WithButton(MouseButton.Left));
                InputSystem.Update();
                InputState.Change(keyboard,new KeyboardState(Key.Digit1));
                InputState.Change(mouse,new MouseState {
                    position=point
                }.WithButton(MouseButton.Left));
                keyboard.MakeCurrent();
                mouse.MakeCurrent();
                Assert.IsTrue(keyboard.digit1Key.isPressed,"Synthetic digit1 key reaches device");
                p.ProcessFrame(0);
                Assert.AreEqual(1,p.Controller.Snapshot().Deployments,$"Keyboard selection plus lane click: selected={p.SelectedUnit} hover={p.HoveredLane} screen={Screen.width}x{Screen.height} point={point}");
                Assert.NotNull(p.Controller.World.State.Entities.Find(e=>e.DefinitionId=="villager_emak"));
                InputSystem.QueueStateEvent(mouse,new MouseState {
                    position=point
                });
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                InputSystem.Update();
                yield return null;
#if UNITY_EDITOR
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null) {
                    ScreenCapture.CaptureScreenshot("/tmp/ratf-playmode.png");
                    yield return null;
                    yield return null;
                }
#endif
            }
            finally {
                InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorBehavior;
                InputSystem.settings.backgroundBehavior=oldBehavior;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
                p.ReturnToMenu();
            }
        }
        [UnityTest] public IEnumerator MissingEndpointFallbackAndRestart() {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Game/Scenes/FactoryOutskirts.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("FactoryOutskirts");
#endif
            yield return null;
            var p=Object.FindAnyObjectByType<BattlePresenter>();
            p.Settings.Mode=ProviderMode.LocalOllama;
            p.Settings.Endpoint="http://127.0.0.1:1/api/chat";
            p.Settings.Model="contract-test";
            p.Settings.Timeout=.2;
            p.StartMatch();
            p.DismissTutorial();
            float until=Time.realtimeSinceStartup+2;
            while(p.Controller.World.State.Entities.Count==0&&Time.realtimeSinceStartup<until)yield return null;
            Assert.Greater(p.Controller.World.State.Entities.Count,0,"Unavailable endpoint uses paid scripted fallback");
            var previous=p.Controller;
            p.StartMatch();
            p.DismissTutorial();
            Assert.AreEqual(Lifecycle.Disposed,previous.World.State.Lifecycle);
            Assert.AreEqual(0,p.Controller.World.State.Entities.Count);
            p.ReturnToMenu();
            yield return null;
        }
    }
}
