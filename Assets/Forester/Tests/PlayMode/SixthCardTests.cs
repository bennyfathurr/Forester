using System;
using System.Linq;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Forester.Domain;
using Forester.Application;
using Forester.Presentation;
namespace Forester.Tests {
    public sealed class SixthCardTests {
        [UnityTest]public IEnumerator AuthoringOnlySixthCardHasVisualPreviewPaidPlacementAndAttack() {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Forester/Scenes/VillageForestEdge.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("VillageForestEdge");
#endif
            yield return null;
            var source=UnityEngine.Object.FindAnyObjectByType<GamePresenter>();
            source.hud.canvas.gameObject.SetActive(false);
            source.enabled=false;
            // Fixture is entirely content: native-model wrapper, visual binding and three authoring assets.
            var catalog=UnityEngine.Object.Instantiate(source.cards);
            var visuals=UnityEngine.Object.Instantiate(source.visuals);
            var defender=ScriptableObject.CreateInstance<DefenderDefinitionSO>();
            defender.definition=new DefenderDefinition {
                Id="sixth",VisualId="sixth",Kind=CombatKind.Direct,Damage=100,Interval=.5,Range=100
            };
            var card=ScriptableObject.CreateInstance<CardDefinitionSO>();
            card.definition=new CardDefinition {
                Id="card_sixth",Name="Sixth fixture",DefenderId="sixth",Cost=10,MaxCopies=1,Description="Uses existing direct containment strategy.",Category="Containment"
            };
            var wrapper=UnityEngine.Object.Instantiate(source.visuals.Find("water_team").prefab);
            wrapper.name="Sixth native asset wrapper";
            wrapper.SetActive(false);
            catalog.cards=catalog.cards.Concat(new[] {
                card
            }).ToArray();
            catalog.defenders=catalog.defenders.Concat(new[] {
                defender
            }).ToArray();
            visuals.bindings=visuals.bindings.Concat(new[] {
                new VisualBinding {
                    id="sixth",prefab=wrapper,portrait=source.visuals.Find("water_team").portrait
                }
            }).ToArray();
            var root=new GameObject("Sixth authoring fixture");
            var p=root.AddComponent<GamePresenter>();
            var hud=root.AddComponent<HudPresenter>();
            var canvasObject=new GameObject("Fixture Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var views=new GameObject("Fixture views");
            hud.canvas=canvasObject.GetComponent<Canvas>();
            hud.canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            p.hud=hud;
            p.cards=catalog;
            p.visuals=visuals;
            p.level=source.level;
            p.boardCamera=source.boardCamera;
            p.viewsRoot=views.transform;
            p.placementPreviewMaterial=source.placementPreviewMaterial;
            double now=0;
            p.Initialize(settings=>new MatchController(source.level.Copy(),catalog.Copy(),new ScriptedThreatCommander(),false,()=>now),(settings,ct)=>System.Threading.Tasks.Task.FromResult("fixture"));
            p.StartMatch();
            try {
                Assert.That(hud.tray.Buttons.Count,Is.EqualTo(6));
                hud.tray.Buttons["card_sixth"].onClick.Invoke();
                Assert.That(p.SelectedCard,Is.EqualTo("card_sixth"));
                Assert.That(p.PreviewCell(new Cell(2,8)).Success,Is.True);
                Assert.That(p.Ghost.activeSelf,Is.True);
                Assert.That(p.Ghost.GetComponentsInChildren<Renderer>().Length,Is.GreaterThan(0));
                p.ClickCell(new Cell(2,8));
                Assert.That(p.Controller.World.Board.PP,Is.EqualTo(110));
                p.StartWave();
                p.Controller.Update(0);
                now=4;
                p.Controller.Update(0);
                p.Controller.World.Advance();
                Assert.That(p.Controller.World.Contained,Is.EqualTo(1));
                yield return null;
                Assert.That(views.GetComponentsInChildren<EntityView>().Any(v=>v.name.Contains("sixth")),Is.True);
            }
            finally {
                p.ReturnToMenu();
                foreach(var o in new UnityEngine.Object[] {
                    root,canvasObject,views,wrapper,catalog,visuals,card,defender
                })UnityEngine.Object.Destroy(o);
            }
            yield return null;
        }
    }
}
