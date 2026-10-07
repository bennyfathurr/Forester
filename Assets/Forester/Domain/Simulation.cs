using System;
using System.Collections.Generic;
using System.Linq;
namespace Forester.Domain {
    public sealed class ThreatState {
        public long Id;
        public string DefinitionId,RouteId;
        public double HP,Distance,SlowMultiplier=1;
        public int SlowUntil;
        public Point Position,PreviousPosition;
    }
    public sealed class CombatEvent {
        public long DefenderId,ThreatId;
        public Point From,To;
    }
    public sealed class WaveSummary {
        public int Wave,Contained,Leaked,Integrity,UnusedPP;
        public Dictionary<string,int> LeaksByRoute=new Dictionary<string,int>();
        public string Reflection=>"Contained "+Contained+"; leaked "+Leaked+" ("+string.Join(", ",LeaksByRoute.Select(x=>x.Key+" "+x.Value))+ "). "+UnusedPP+" PP unused. Add coverage near routes with leaks.";
    }
    public sealed class SimulationWorld {
        public const double Step=.05;
        public readonly BoardState Board;
        public readonly PathRegistry Paths;
        public readonly Dictionary<long,ThreatState> Threats=new Dictionary<long,ThreatState>();
        public readonly List<CombatEvent> Shots=new List<CombatEvent>();
        public readonly List<WaveSummary> History=new List<WaveSummary>();
        public int Integrity,Wave=1,Tick,Contained,Leaked,TotalSpent,TotalPlacements;
        public string Error="";
        public ValidatedPlan Plan {
            get;
            private set;
        }
        int nextSpawn;
        long nextThreat=1;
        public int Remaining=>Plan==null?0:Plan.Count-nextSpawn+Threats.Count;
        public SimulationWorld(LevelDefinition l,Catalog c) {
            c.Validate();
            PathValidator.Validate(l,c);
            Board=new BoardState(l,c);
            Paths=new PathRegistry(l);
            Integrity=l.Integrity;
        }
        public void SetTelegraphPlan(ValidatedPlan plan) {
            Plan=plan;
            Board.Phase=Phase.Telegraph;
        }
        public void Begin(ValidatedPlan plan) {
            if(Board.Phase!=Phase.Telegraph&&Board.Phase!=Phase.Preparation)throw new InvalidOperationException("Wave cannot begin");
            Plan=plan??throw new ArgumentNullException(nameof(plan));
            Tick=0;
            nextSpawn=0;
            Contained=Leaked=0;
            Threats.Clear();
            foreach(var d in Board.Defenders.Values)d.NextAttack=0;
            Board.Phase=Phase.Active;
        }
        public void Advance() {
            Shots.Clear();
            if(Board.Paused||Board.Phase!=Phase.Active)return;
            while(nextSpawn<Plan.Events.Length&&Plan.Events[nextSpawn].Tick<=Tick) {
                var e=Plan.Events[nextSpawn++];
                var d=Board.Catalog.Enemies[e.EnemyId];
                var p=Paths.Routes[e.RouteId].At(0);
                long id=nextThreat++;
                Threats.Add(id,new ThreatState {
                    Id=id,DefinitionId=e.EnemyId,RouteId=e.RouteId,HP=d.HP,Position=p,PreviousPosition=p
                });
            }
            double gust=1;
            foreach(var c in Plan.Conditions)if(Tick>=c.StartTick&&Tick<c.EndTick)gust=c.SpeedMultiplier;
            foreach(var t in Threats.Values) {
                if(t.SlowUntil<=Tick)t.SlowMultiplier=1;
                t.PreviousPosition=t.Position;
                t.Distance+=Board.Catalog.Enemies[t.DefinitionId].Speed*gust*t.SlowMultiplier*Step;
                t.Position=Paths.Routes[t.RouteId].At(t.Distance);
            }
            var damage=new Dictionary<long,double>();
            foreach(var d in Board.Defenders.Values.OrderBy(d=>d.Id)) {
                var def=Board.Catalog.Defenders[d.DefinitionId];
                if(def.Kind==CombatKind.Aura)continue;
                double now=Tick*Step;
                var pos=Board.Level.Position(d.Cell);
                var target=Acquire(pos,def.Range);
                if(target==null) {
                    d.NextAttack=Math.Max(d.NextAttack,now);
                    continue;
                }
                if(now+1e-8<d.NextAttack)continue;
                bool aura=Board.Defenders.Values.Any(a=>Board.Catalog.Defenders[a.DefinitionId].Kind==CombatKind.Aura&&Point.Distance(pos,Board.Level.Position(a.Cell))<=Board.Catalog.Defenders[a.DefinitionId].Range);
                double boost=1;
                if(aura)boost=Board.Defenders.Values.Where(a=>Board.Catalog.Defenders[a.DefinitionId].Kind==CombatKind.Aura&&Point.Distance(pos,Board.Level.Position(a.Cell))<=Board.Catalog.Defenders[a.DefinitionId].Range).Max(a=>Board.Catalog.Defenders[a.DefinitionId].AuraMultiplier);
                d.NextAttack=now+def.Interval/boost;
                Shots.Add(new CombatEvent {
                    DefenderId=d.Id,ThreatId=target.Id,From=pos,To=target.Position
                });
                var hits=def.Kind==CombatKind.SplashSlow?Threats.Values.Where(t=>Point.Distance(t.Position,target.Position)<=def.SplashRadius).ToArray():new[] {
                    target
                };
                foreach(var t in hits) {
                    damage[t.Id]=(damage.TryGetValue(t.Id,out var v)?v:0)+def.Damage;
                    if(def.Kind==CombatKind.SplashSlow) {
                        t.SlowMultiplier=def.SlowMultiplier;
                        t.SlowUntil=Tick+(int)Math.Round(def.SlowSeconds/Step);
                    }
                }
            }
            foreach(var h in damage)Threats[h.Key].HP-=h.Value;
            foreach(var id in Threats.Values.Where(t=>t.HP<=0).Select(t=>t.Id).ToArray()) {
                Threats.Remove(id);
                Contained++;
            }
            foreach(var t in Threats.Values.OrderBy(t=>t.Id).ToArray())if(t.Distance>=Paths.Routes[t.RouteId].Length) {
                Integrity=Math.Max(0,Integrity-Board.Catalog.Enemies[t.DefinitionId].IntegrityLoss);
                Threats.Remove(t.Id);
                Leaked++;
                if(!leaks.ContainsKey(t.RouteId))leaks[t.RouteId]=0;
                leaks[t.RouteId]++;
            }
            Tick++;
            if(Integrity==0) {
                Board.Phase=Phase.Lost;
                Record();
                return;
            }
            if(nextSpawn==Plan.Events.Length&&Threats.Count==0) {
                Board.Phase=Phase.Cleanup;
                Record();
                return;
            }
            if(Tick>=2400) {
                Error="Active wave exceeded 120 seconds; inspect route/speed data.";
                Board.Phase=Phase.Faulted;
            }
        }
        readonly Dictionary<string,int> leaks=new Dictionary<string,int>();
        public ThreatState Acquire(Point pos,double range)=>Threats.Values.Where(t=>Point.Distance(pos,t.Position)<=range).OrderBy(t=>Paths.Routes[t.RouteId].Length-t.Distance).ThenBy(t=>t.Id).FirstOrDefault();
        void Record() {
            History.Add(new WaveSummary {
                Wave=Wave,Contained=Contained,Leaked=Leaked,Integrity=Integrity,UnusedPP=Board.PP,LeaksByRoute=new Dictionary<string,int>(leaks)
            });
            leaks.Clear();
        }
        public void FinishCleanup() {
            if(Board.Phase!=Phase.Cleanup)return;
            if(Wave>=Board.Level.Waves.Length) {
                Board.Phase=Phase.Won;
                return;
            }
            Board.PP=Math.Min(Board.Level.CapPP,Board.PP+Board.Level.Awards[Wave-1]);
            Wave++;
            Plan=null;
            Board.Phase=Phase.Preparation;
        }
        public string ConditionNotice() {
            if(Plan==null)return "";
            foreach(var c in Plan.Conditions)if(Tick>=c.StartTick-(int)(c.Telegraph*20)&&Tick<c.EndTick)return Tick<c.StartTick?c.Id+" approaching":c.Id+" active";
            return "";
        }
    }
}
