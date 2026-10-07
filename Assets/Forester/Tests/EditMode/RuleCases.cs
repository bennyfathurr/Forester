using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Forester.Domain;
using Forester.Application;
namespace Forester.Tests {
    public static class RuleCases {
        public static int Passed;
        static void Check(bool ok,string label) {
            if(!ok)throw new Exception(label);
            Passed++;
        }
        static void Reject(Action a,string label) {
            try {
                a();
            }
            catch(ArgumentException) {
                Passed++;
                return;
            }
            throw new Exception(label);
        }
        static SimulationWorld World()=>new SimulationWorld(Defaults.Level(),Defaults.Catalog());
        public static void Run() {
            Passed=0;
            var w=World();
            var p=new PlacementService(w.Board);
            Check(w.Board.PP==120&&w.Board.Level.Cells.Count(x=>x.Value==CellKind.Buildable)==18,"Initial economy/mask");
            Check(w.Paths.Routes["NORTH"].Length==42&&w.Paths.Routes["SOUTH"].Length==44,"Derived path length");
            Check(!p.Place(new PlaceCardCommand("card_water_team",new Cell(0,9))).Success&&w.Board.PP==120,"Path rejects atomically");
            var a=p.Place(new PlaceCardCommand("card_water_team",new Cell(2,8)));
            Check(a.Success&&w.Board.PP==85,"Paid placement");
            Check(!p.Move(new MoveDefenderCommand(a.EntityId,new Cell(5,9))).Success&&w.Board.Occupancy[new Cell(2,8)]==a.EntityId,"Failed move retains origin");
            Check(p.Move(new MoveDefenderCommand(a.EntityId,new Cell(4,8))).Success&&w.Board.PP==85&&!w.Board.Occupancy.ContainsKey(new Cell(2,8)),"Free move");
            Check(p.Sell(new SellDefenderCommand(a.EntityId)).Success&&w.Board.PP==111,"Actual paid refund floor");
            Check(!p.Sell(new SellDefenderCommand(a.EntityId)).Success&&w.Board.PP==111,"Sell once");
            w.Board.PP=200;
            var card=w.Board.Catalog.Cards["card_water_team"];
            card.MaxCopies=1;
            a=p.Place(new PlaceCardCommand(card.Id,new Cell(2,8)));
            Check(!p.Place(new PlaceCardCommand(card.Id,new Cell(4,8))).Success,"Live copy limit");
            w.Board.Phase=Phase.Planning;
            Check(!p.Place(new PlaceCardCommand("card_watch_post",new Cell(4,8))).Success&&!p.Move(new MoveDefenderCommand(a.EntityId,new Cell(4,8))).Success&&!p.Sell(new SellDefenderCommand(a.EntityId)).Success,"Planning locks all transactions");
            w.Board.Phase=Phase.Preparation;
            w.Board.Paused=true;
            Check(!p.Move(new MoveDefenderCommand(a.EntityId,new Cell(4,8))).Success,"Pause locks");
            w=World();
            w.Board.Catalog.Cards["card_water_team"].Width=2;
            Check(!new PlacementService(w.Board).Place(new PlaceCardCommand("card_water_team",new Cell(2,8))).Success&&w.Board.PP==120,"Footprint all cells");
            var l=Defaults.Level();
            l.Routes[0].Waypoints[1]=l.Routes[0].Waypoints[0];
            Reject(()=>PathValidator.Validate(l),"Zero segment rejected");
            l=Defaults.Level();
            l.Cells[new Cell(3,9)]=CellKind.Buildable;
            Reject(()=>PathValidator.Validate(l),"Intermediate path build rejected");
            l=Defaults.Level();
            l.Routes[0].Waypoints[2]=new Cell(50,6);
            Reject(()=>PathValidator.Validate(l),"Outside path rejected");
            w=World();
            for(int wave=1;      wave<=3;      wave++) {
                var plan=ThreatPlanValidator.Validate(ScriptedPlans.Create(wave,"test"),w.Board.Level,w.Board.Catalog,wave,"test");
                Check(plan.Cost==new[] {
                    12,22,32
                }
                [wave-1],"Scripted budgets");
            }
            var bad=ScriptedPlans.Create(1,"x");
            bad.groups[0].count=13;
            Reject(()=>ThreatPlanValidator.Validate(bad,w.Board.Level,w.Board.Catalog,1,"x"),"Over budget/count");
            bad=ScriptedPlans.Create(1,"x");
            bad.groups[1].start_seconds=0;
            Reject(()=>ThreatPlanValidator.Validate(bad,w.Board.Level,w.Board.Catalog,1,"x"),"Overlapping spawn density");
            bad=ScriptedPlans.Create(1,"x");
            bad.groups[1].route_id="SOUTH";
            bad.groups[1].spawn_id="S_SOUTH";
            Reject(()=>ThreatPlanValidator.Validate(bad,w.Board.Level,w.Board.Catalog,1,"x"),"Wave route allowlist");
            bad=ScriptedPlans.Create(2,"x");
            bad.conditions=new[] {
                new PlannedCondition {
                    condition_id="gust_front",start_seconds=10
                }
            };
            Reject(()=>ThreatPlanValidator.Validate(bad,w.Board.Level,w.Board.Catalog,2,"x"),"Condition allowlist");
            bad=ScriptedPlans.Create(3,"x");
            bad.conditions[0].start_seconds=4;
            Reject(()=>ThreatPlanValidator.Validate(bad,w.Board.Level,w.Board.Catalog,3,"x"),"Condition bounds");
            Reject(()=>ThreatPlanValidator.Validate(ScriptedPlans.Create(1,"old"),w.Board.Level,w.Board.Catalog,1,"new"),"Request binding");
            // Containment resolves before goal leakage in the same tick.
            w=World();
            w.Board.PP=200;
            p=new PlacementService(w.Board);
            a=p.Place(new PlaceCardCommand("card_watch_post",new Cell(14,4)));
            w.Board.Catalog.Defenders["watch_post"].Damage=100;
            var one=new ThreatPlan {
                request_id="one",wave_index=1,groups=new[] {
                    new ThreatGroup {
                        enemy_id="emberling",spawn_id="S_NORTH",route_id="NORTH",count=1,start_seconds=0,interval_seconds=1
                    }
                }
            };
            w.Begin(ThreatPlanValidator.Validate(one,w.Board.Level,w.Board.Catalog,1,"one"));
            w.Advance();
            var t=w.Threats.Values.Single();
            t.Distance=w.Paths.Routes["NORTH"].Length-.01;
            w.Board.Defenders[a.EntityId].NextAttack=0;
            w.Advance();
            Check(w.Contained==1&&w.Leaked==0&&w.Integrity==20&&w.Board.Phase==Phase.Cleanup,"Same tick contained prevents leak");
            w=World();
            w.Begin(ThreatPlanValidator.Validate(one,w.Board.Level,w.Board.Catalog,1,"one"));
            w.Advance();
            Check(w.Board.Phase==Phase.Active&&w.Threats.Count==1,"Queue empty alone does not clear");
            for(int i=0;      i<1200&&w.Board.Phase==Phase.Active;      i++)w.Advance();
            Check(w.Leaked==1&&w.Integrity==19&&w.Board.Phase==Phase.Cleanup,"Route reaches goal and clears");
            w.FinishCleanup();
            Check(w.Board.PP==175&&w.Wave==2,"Interwave award only");
            // Target tie uses remaining route length and stable IDs.
            w=World();
            w.Threats.Add(5,new ThreatState {
                Id=5,RouteId="NORTH",Position=new Point(0,0),Distance=1
            });
            w.Threats.Add(3,new ThreatState {
                Id=3,RouteId="NORTH",Position=new Point(0,0),Distance=1
            });
            Check(w.Acquire(new Point(0,0),1).Id==3,"Target stable tie");
            // A brief target gap cannot bypass an unfinished attack cooldown.
            w=World();
            new PlacementService(w.Board).Place(new PlaceCardCommand("card_water_team",new Cell(2,8)));
            w.Begin(ThreatPlanValidator.Validate(Pair(),w.Board.Level,w.Board.Catalog,1,"pair"));
            w.Advance();
            var gapTarget=w.Threats.Values.Single();
            gapTarget.Distance=20;
            w.Advance();
            gapTarget.Distance=0;
            w.Advance();
            Check(w.Shots.Count==0,"Target gap preserves attack cooldown");
            w=World();
            w.Wave=3;
            w.Begin(ThreatPlanValidator.Validate(ScriptedPlans.Create(3,"gust"),w.Board.Level,w.Board.Catalog,3,"gust"));
            for(int i=0;  i<200;  i++)w.Advance();
            var gustTarget=w.Threats.Values.OrderBy(t=>t.Id).First();
            double beforeGust=gustTarget.Distance;
            w.Advance();
            Check(Math.Abs(gustTarget.Distance-beforeGust-.115)<1e-8,"Allowlisted gust speed bounded");
            CombatChecks();
            LifecycleChecks();
            // Catalog-only sixth defender uses the same typed attack strategy.
            w=World();
            var d=new DefenderDefinition {
                Id="sixth",VisualId="sixth",Kind=CombatKind.Direct,Damage=100,Interval=.5,Range=100
            };
            w.Board.Catalog.Defenders.Add(d.Id,d);
            w.Board.Catalog.Cards.Add("card_sixth",new CardDefinition {
                Id="card_sixth",DefenderId=d.Id,Name="Sixth",Cost=10,MaxCopies=1
            });
            p=new PlacementService(w.Board);
            Check(p.Validator.Preview("card_sixth",new Cell(2,8)).Success,"Sixth preview");
            Check(p.Place(new PlaceCardCommand("card_sixth",new Cell(2,8))).Success&&w.Board.PP==110,"Sixth paid place");
            w.Begin(ThreatPlanValidator.Validate(one,w.Board.Level,w.Board.Catalog,1,"one"));
            w.Advance();
            Check(w.Contained==1,"Sixth attacks without ID switch");
            // All three finite waves complete with a data-configured attacking defense.
            w=World();
            w.Board.Catalog.Defenders["watch_post"].Range=100;
            w.Board.Catalog.Defenders["watch_post"].Damage=1000;
            w.Board.Catalog.Defenders["watch_post"].Interval=.05;
            new PlacementService(w.Board).Place(new PlaceCardCommand("card_watch_post",new Cell(2,8)));
            for(int wave=1;      wave<=3;      wave++) {
                w.Begin(ThreatPlanValidator.Validate(ScriptedPlans.Create(wave,"full"),w.Board.Level,w.Board.Catalog,wave,"full"));
                for(int i=0;      i<2400&&w.Board.Phase==Phase.Active;      i++)w.Advance();
                Check(w.Board.Phase==Phase.Cleanup,"Finite full wave");
                w.FinishCleanup();
            }
            Check(w.Board.Phase==Phase.Won&&w.Integrity==20,"Three wave win");
            w=World();
            w.Integrity=1;
            w.Begin(ThreatPlanValidator.Validate(one,w.Board.Level,w.Board.Catalog,1,"one"));
            for(int i=0;      i<2400&&w.Board.Phase==Phase.Active;      i++)w.Advance();
            Check(w.Board.Phase==Phase.Lost,"Zero integrity loses");
        }
        static ThreatPlan Pair()=>new ThreatPlan {
            request_id="pair",wave_index=1,groups=new[] {
                new ThreatGroup {
                    enemy_id="emberling",spawn_id="S_NORTH",route_id="NORTH",count=2,start_seconds=0,interval_seconds=1
                }
            }
        };
        static void CombatChecks() {
            var w=World();
            var p=new PlacementService(w.Board);
            p.Place(new PlaceCardCommand("card_foam_station",new Cell(2,8)));
            w.Begin(ThreatPlanValidator.Validate(Pair(),w.Board.Level,w.Board.Catalog,1,"pair"));
            for(int i=0;      i<30;      i++)w.Advance();
            Check(w.Threats.Values.Any(t=>t.SlowMultiplier==.65),"Foam slow applied");
            Check(w.Threats.Values.All(t=>t.SlowMultiplier>=.65),"Slow nonstack");
            Check(w.Threats.Count==2&&w.Threats.Values.All(t=>t.HP<35),"Splash damages nearby targets");
            w.Board.Defenders.Clear();
            for(int i=0;  i<45;  i++)w.Advance();
            Check(w.Threats.Values.All(t=>t.SlowMultiplier==1),"Slow expires without refresh");
            w=World();
            p=new PlacementService(w.Board);
            var a=p.Place(new PlaceCardCommand("card_water_team",new Cell(2,8)));
            p.Place(new PlaceCardCommand("card_water_tank",new Cell(4,8)));
            p.Place(new PlaceCardCommand("card_water_tank",new Cell(2,10)));
            w.Begin(ThreatPlanValidator.Validate(Pair(),w.Board.Level,w.Board.Catalog,1,"pair"));
            w.Advance();
            Check(Math.Abs(w.Board.Defenders[a.EntityId].NextAttack-.8/1.15)<1e-8,"Aura nonstack boost");
            Check(w.Shots.Count==1,"Aura does not attack");
            var copy=w.Plan.Schedule;
            copy[0].Tick=1000;
            Check(w.Plan.Schedule[0].Tick==0,"Frozen plan copies");
        }
        sealed class Deferred:IThreatCommander {
            public TaskCompletionSource<ThreatPlan> Source=new TaskCompletionSource<ThreatPlan>();
            public Task<ThreatPlan> PlanAsync(ThreatObservation o,CancellationToken ct)=>Source.Task;
        }
        static void LifecycleChecks() {
            double now=0;
            var w=World();
            new PlacementService(w.Board).Place(new PlaceCardCommand("card_watch_post",new Cell(2,8)));
            var fake=new Deferred();
            var coordinator=new ThreatDecisionCoordinator(w,fake,true,()=>now);
            Check(coordinator.Start(),"Start planning");
            string old=coordinator.LastObservation.request_id;
            coordinator.Cancel();
            fake.Source.SetResult(ScriptedPlans.Create(1,old));
            Thread.Sleep(5);
            coordinator.Pump();
            Check(w.Board.Phase==Phase.Preparation,"Late cancelled plan ignored");
            fake.Source=new TaskCompletionSource<ThreatPlan>();
            coordinator.Start();
            now=9;
            coordinator.Pump();
            Check(w.Board.Phase==Phase.Telegraph&&coordinator.Status.Contains("fallback"),"Timeout fallback");
            coordinator.Pause();
            now=20;
            coordinator.Pump();
            Check(w.Board.Phase==Phase.Telegraph,"Pause freezes telegraph");
            coordinator.Resume();
            now=23;
            coordinator.Pump();
            Check(w.Board.Phase==Phase.Active,"Resume telegraph");
            coordinator.Dispose();
            w=World();
            new PlacementService(w.Board).Place(new PlaceCardCommand("card_watch_post",new Cell(2,8)));
            fake=new Deferred();
            now=0;
            coordinator=new ThreatDecisionCoordinator(w,fake,true,()=>now);
            for(int i=0;      i<6;      i++) {
                coordinator.Start();
                coordinator.Cancel();
            }
            coordinator.Start();
            Check(coordinator.ExternalRequests==6&&w.Board.Phase==Phase.Telegraph,"Six external requests cap");
            coordinator.Dispose();
        }
    }
}
