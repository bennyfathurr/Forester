using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Forester.Domain;
using Forester.Presentation;
using Forester.Editor;
namespace Forester.Tests {
    public sealed class AuthoringTests {
        [Test]public void AuthoredRoutesAndBuildMaskChangeAndReachGoal() {
            var original=AssetDatabase.LoadAssetAtPath<LevelDefinitionSO>("Assets/Forester/Data/Levels/VillageForestEdge.asset");
            var a=UnityEngine.Object.Instantiate(original);
            try {
                a.routes[0].Waypoints=new[] {
                    new Cell(0,9),new Cell(0,6),new Cell(10,6),new Cell(10,3),new Cell(15,3)
                };
                a.cells=a.cells.Concat(new[] {
                    new CellTag {
                        cell=new Cell(1,10),kind=CellKind.Buildable
                    }
                }).ToArray();
                LevelAuthoringWindow.RepaintPaths(a);
                PathValidator.Validate(a.Copy(),Defaults.Catalog());
                var w=new SimulationWorld(a.Copy(),Defaults.Catalog());
                var p=new PlacementService(w.Board);
                Assert.That(p.Place(new PlaceCardCommand("card_watch_post",new Cell(0,6))).Success,Is.False);
                var plan=new ThreatPlan {
                    request_id="authored",wave_index=1,groups=new[] {
                        new ThreatGroup {
                            enemy_id="emberling",spawn_id="S_NORTH",route_id="NORTH",count=1,start_seconds=0,interval_seconds=1
                        }
                    }
                };
                w.Begin(ThreatPlanValidator.Validate(plan,w.Board.Level,w.Board.Catalog,1,"authored"));
                for(int i=0;      i<1600&&w.Board.Phase==Phase.Active;      i++)w.Advance();
                Assert.That(w.Leaked,Is.EqualTo(1));
                Assert.That(w.Integrity,Is.EqualTo(19));
                Assert.That(w.Paths.Routes["NORTH"].At(w.Paths.Routes["NORTH"].Length).X,Is.EqualTo(30));
            }
            finally {
                UnityEngine.Object.DestroyImmediate(a);
            }
        }
        [Test]public void SixthCardThroughAuthoringCatalogAndPrefabOnly() {
            var original=AssetDatabase.LoadAssetAtPath<CardCatalogSO>("Assets/Forester/Data/Cards/CardCatalog.asset");
            var c=UnityEngine.Object.Instantiate(original);
            var card=ScriptableObject.CreateInstance<CardDefinitionSO>();
            var defender=ScriptableObject.CreateInstance<DefenderDefinitionSO>();
            var visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<VisualCatalogSO>("Assets/Forester/Data/Visuals/VisualCatalog.asset"));
            var wrapper=new GameObject("Sixth prefab wrapper");
            wrapper.AddComponent<EntityView>();
            var canvas=new GameObject("Tray test",typeof(RectTransform));
            try {
                defender.definition=new DefenderDefinition {
                    Id="sixth",VisualId="sixth",Kind=CombatKind.Direct,Range=100,Damage=100,Interval=.5
                };
                card.definition=new CardDefinition {
                    Id="card_sixth",Name="Sixth",DefenderId="sixth",Cost=10,MaxCopies=1
                };
                c.cards=c.cards.Concat(new[] {
                    card
                }).ToArray();
                c.defenders=c.defenders.Concat(new[] {
                    defender
                }).ToArray();
                visual.bindings=visual.bindings.Concat(new[] {
                    new VisualBinding {
                        id="sixth",prefab=wrapper
                    }
                }).ToArray();
                var domain=c.Copy();
                visual.Validate(domain);
                var tray=canvas.AddComponent<CardTrayPresenter>();
                tray.Build(canvas.transform,c,visual,id=> {
                });
                Assert.That(tray.Buttons.ContainsKey("card_sixth"),Is.True);
                var w=new SimulationWorld(Defaults.Level(),domain);
                var p=new PlacementService(w.Board);
                Assert.That(p.Validator.Preview("card_sixth",new Cell(2,8)).Success,Is.True);
                Assert.That(p.Place(new PlaceCardCommand("card_sixth",new Cell(2,8))).Success,Is.True);
                Assert.That(w.Board.PP,Is.EqualTo(110));
                w.Begin(ThreatPlanValidator.Validate(ScriptedPlans.Create(1,"sixth"),w.Board.Level,w.Board.Catalog,1,"sixth"));
                w.Advance();
                Assert.That(w.Contained,Is.EqualTo(1));
            }
            finally {
                foreach(var o in new UnityEngine.Object[] {
                    c,card,defender,visual,wrapper,canvas
                })UnityEngine.Object.DestroyImmediate(o);
            }
        }
    }
}
