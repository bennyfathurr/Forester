using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Forester.Domain;
namespace Forester.Application {
    public interface IThreatCommander {
        Task<ThreatPlan> PlanAsync(ThreatObservation observation,CancellationToken cancellationToken);
    }
    public sealed class ScriptedThreatCommander:IThreatCommander {
        public Task<ThreatPlan> PlanAsync(ThreatObservation o,CancellationToken ct) {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(ScriptedPlans.Create(o.wave_index,o.request_id));
        }
    }
    internal sealed class ThreatObservationDraft {
        public int schema_version=1,epoch,wave_index,integrity,pp;
        public string match_id,request_id;
        public object board;
        public object[] defenders,route_summaries,previous_outcomes,legal_spawn_routes,enemy_catalog,allowed_conditions;
        public object wave_bounds;
    }
    public sealed class ThreatObservation {
        public readonly int schema_version,epoch,wave_index,integrity,pp;
        public readonly string match_id,request_id;
        public readonly object board,wave_bounds;
        public readonly IReadOnlyList<object> defenders,route_summaries,previous_outcomes,legal_spawn_routes,enemy_catalog,allowed_conditions;
        internal ThreatObservation(ThreatObservationDraft d) {
            schema_version=d.schema_version;
            epoch=d.epoch;
            wave_index=d.wave_index;
            integrity=d.integrity;
            pp=d.pp;
            match_id=d.match_id;
            request_id=d.request_id;
            board=d.board;
            wave_bounds=d.wave_bounds;
            defenders=Array.AsReadOnly(d.defenders);
            route_summaries=Array.AsReadOnly(d.route_summaries);
            previous_outcomes=Array.AsReadOnly(d.previous_outcomes);
            legal_spawn_routes=Array.AsReadOnly(d.legal_spawn_routes);
            enemy_catalog=Array.AsReadOnly(d.enemy_catalog);
            allowed_conditions=Array.AsReadOnly(d.allowed_conditions);
        }
    }
    public static class ThreatObservationBuilder {
        public static ThreatObservation Build(SimulationWorld w,string match,int epoch,string request) {
            var b=w.Board;
            var rule=b.Level.Waves[w.Wave-1];
            return new ThreatObservation(new ThreatObservationDraft {
                match_id=match,epoch=epoch,request_id=request,wave_index=w.Wave,integrity=w.Integrity,pp=b.PP,board=new {
                    columns=b.Level.Columns,rows=b.Level.Rows,cell_size=b.Level.CellSize
                },defenders=b.Defenders.Values.OrderBy(d=>d.Id).Select(d=> {
                    var f=b.Catalog.Defenders[d.DefinitionId];      return (object)new {
                        entity_id=d.Id,definition_id=d.DefinitionId,cell_id=d.Cell.Id,column=d.Cell.Column,row=d.Cell.Row,range=f.Range,behavior=f.Kind.ToString()
                    };
                }).ToArray(),route_summaries=w.Paths.Routes.Values.Select(r=> {
                    var segments=new List<object>();      for(int i=1;      i<r.Points.Length;      i++) {
                        double covered=0;      int samples=20;      for(int j=0;      j<samples;      j++) {
                            var p=r.At(r.Distances[i-1]+(r.Distances[i]-r.Distances[i-1])*(j+.5)/samples);      if(b.Defenders.Values.Any(d=>b.Catalog.Defenders[d.DefinitionId].Kind!=CombatKind.Aura&&Point.Distance(p,b.Level.Position(d.Cell))<=b.Catalog.Defenders[d.DefinitionId].Range))covered++;
                        }
                        segments.Add(new {
                            segment=i-1,length=r.Distances[i]-r.Distances[i-1],coverage_fraction=covered/samples
                        });
                    }
                    return (object)new {
                        route_id=r.Definition.Id,length=r.Length,segments=segments.ToArray()
                    };
                }).ToArray(),previous_outcomes=w.History.Select(h=>(object)new {
                    wave=h.Wave,contained=h.Contained,leaked=h.Leaked,integrity=h.Integrity,unused_pp=h.UnusedPP,leaks_by_route=new Dictionary<string,int>(h.LeaksByRoute)
                }).ToArray(),legal_spawn_routes=b.Level.Routes.Where(r=>rule.Routes.Contains(r.Id)).Select(r=>(object)new {
                    spawn_id=r.SpawnId,route_id=r.Id,goal_id=r.GoalId
                }).ToArray(),enemy_catalog=rule.Enemies.Select(id=> {
                    var e=b.Catalog.Enemies[id];      return (object)new {
                        enemy_id=e.Id,cost=e.Cost,hp=e.HP,speed=e.Speed,integrity_loss=e.IntegrityLoss
                    };
                }).ToArray(),wave_bounds=new {
                    budget=rule.Budget,max_count=rule.MaxCount,max_groups=6,max_per_spawn_per_second=1,schedule_window_seconds=rule.Window
                },allowed_conditions=rule.Conditions.Select(id=> {
                    var c=b.Catalog.Conditions[id];      return (object)new {
                        condition_id=c.Id,cost=c.Cost,duration_seconds=c.Duration,speed_multiplier=c.SpeedMultiplier,telegraph_seconds=c.Telegraph,min_start_seconds=5,max_start_seconds=20
                    };
                }).ToArray()
            });
        }
    }
    public sealed class ThreatDecisionCoordinator:IDisposable {
        readonly SimulationWorld world;
        readonly IThreatCommander commander;
        readonly bool external;
        readonly Func<double> clock;
        readonly double timeout;
        readonly ConcurrentQueue<Completion> inbox=new ConcurrentQueue<Completion>();
        CancellationTokenSource cancellation;
        int epoch,sequence,requests;
        double deadline,telegraphUntil;
        string request;
        bool disposed,resumePlanning;
        public readonly string MatchId=Guid.NewGuid().ToString("N");
        public string Status="Scripted offline";
        public ThreatObservation LastObservation {
            get;
            private set;
        }
        public int ExternalRequests=>requests;
        sealed class Completion {
            public int Epoch;
            public string Request;
            public ThreatPlan Plan;
            public string Error;
        }
        public ThreatDecisionCoordinator(SimulationWorld w,IThreatCommander c,bool isExternal,Func<double> realtime,double seconds=8) {
            world=w;
            commander=c;
            external=isExternal;
            Status=isExternal?"External provider selected":"Scripted offline";
            clock=realtime;
            timeout=seconds;
        }
        public bool Start() {
            if(disposed||world.Board.Paused||world.Board.Phase!=Phase.Preparation||!world.Board.Defenders.Values.Any(d=>world.Board.Catalog.Defenders[d.DefinitionId].Kind!=CombatKind.Aura))return false;
            Invalidate();
            world.Board.Phase=Phase.Planning;
            request="wave"+world.Wave+"-req"+(++sequence);
            LastObservation=ThreatObservationBuilder.Build(world,MatchId,epoch,request);
            deadline=clock()+timeout;
            cancellation=new CancellationTokenSource();
            Status="Planning";
            if(external&&requests>=6) {
                ApplyFallback("External request limit");
                return true;
            }
            if(external)requests++;
            _ = RequestAsync(LastObservation,epoch,request,cancellation.Token);
            return true;
        }
        async Task RequestAsync(ThreatObservation o,int e,string r,CancellationToken ct) {
            try {
                var plan=await commander.PlanAsync(o,ct);
                inbox.Enqueue(new Completion {
                    Epoch=e,Request=r,Plan=plan
                });
            }
            catch(Exception ex) {
                inbox.Enqueue(new Completion {
                    Epoch=e,Request=r,Error=ex is OperationCanceledException?"Cancelled":ex.GetType().Name
                });
            }
        }
        public void Pump() {
            if(disposed||world.Board.Paused)return;
            while(inbox.TryDequeue(out var c)) {
                if(world.Board.Phase!=Phase.Planning||c.Epoch!=epoch||c.Request!=request)continue;
                if(clock()>=deadline) {
                    ApplyFallback("Deadline exceeded");
                    continue;
                }
                try {
                    if(c.Error!=null)throw new ArgumentException(c.Error);
                    Accept(ThreatPlanValidator.Validate(c.Plan,world.Board.Level,world.Board.Catalog,world.Wave,request),external?"AI plan accepted":"Scripted offline");
                }
                catch {
                    ApplyFallback(c.Error??"Invalid plan");
                }
            }
            if(world.Board.Phase==Phase.Planning&&clock()>=deadline)ApplyFallback("Deadline exceeded");
            if(world.Board.Phase==Phase.Telegraph&&clock()>=telegraphUntil)world.Begin(world.Plan);
        }
        void Accept(ValidatedPlan p,string status) {
            cancellation?.Cancel();
            Status=status;
            world.SetTelegraphPlan(p);
            telegraphUntil=clock()+3;
        }
        void ApplyFallback(string reason) {
            Accept(ThreatPlanValidator.Validate(ScriptedPlans.Create(world.Wave,request),world.Board.Level,world.Board.Catalog,world.Wave,request),"Scripted fallback: "+reason);
        }
        public void Cancel() {
            if(world.Board.Phase!=Phase.Planning)return;
            Invalidate();
            world.Board.Phase=Phase.Preparation;
            Status="Planning cancelled";
        }
        public void Pause() {
            if(world.Board.Paused)return;
            resumePlanning=world.Board.Phase==Phase.Planning;
            if(resumePlanning)Cancel();
            world.Board.Paused=true;
            pauseAt=clock();
        }
        double pauseAt;
        public void Resume() {
            if(!world.Board.Paused)return;
            world.Board.Paused=false;
            if(world.Board.Phase==Phase.Telegraph)telegraphUntil+=clock()-pauseAt;
            if(resumePlanning) {
                resumePlanning=false;
                Start();
            }
        }
        public void Outcome() {
            Invalidate();
        }
        void Invalidate() {
            epoch++;
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation=null;
        }
        public void Dispose() {
            if(disposed)return;
            disposed=true;
            Invalidate();
            while(inbox.TryDequeue(out _)) {
            }
        }
    }
    public sealed class MatchController:IDisposable {
        public readonly SimulationWorld World;
        public readonly List<CombatEvent> FrameShots=new List<CombatEvent>();
        public readonly PlacementService Placement;
        public readonly ThreatDecisionCoordinator Decisions;
        double accumulator;
        bool outcome;
        public MatchController(LevelDefinition l,Catalog c,IThreatCommander commander,bool external,Func<double> clock,double timeout=8) {
            World=new SimulationWorld(l,c);
            Placement=new PlacementService(World.Board);
            Decisions=new ThreatDecisionCoordinator(World,commander,external,clock,timeout);
        }
        public PlacementResult Place(PlaceCardCommand c) {
            var r=Placement.Place(c);
            if(r.Success) {
                World.TotalSpent+=World.Board.Catalog.Cards[c.CardId].Cost;
                World.TotalPlacements++;
            }
            return r;
        }
        public void Update(double dt) {
            FrameShots.Clear();
            Decisions.Pump();
            if(World.Board.Phase==Phase.Cleanup)World.FinishCleanup();
            if(World.Board.Phase==Phase.Active&&!World.Board.Paused) {
                accumulator+=Math.Min(.5,Math.Max(0,dt));
                while(accumulator>=SimulationWorld.Step&&World.Board.Phase==Phase.Active) {
                    World.Advance();
                    FrameShots.AddRange(World.Shots);
                    accumulator-=SimulationWorld.Step;
                }
            }
            else accumulator=0;
            if(!outcome&&(World.Board.Phase==Phase.Won||World.Board.Phase==Phase.Lost||World.Board.Phase==Phase.Faulted)) {
                outcome=true;
                Decisions.Outcome();
            }
        }
        public double Interpolation=>accumulator/SimulationWorld.Step;
        public void Dispose()=>Decisions.Dispose();
    }
}
