using System;
using System.Collections.Generic;
using System.Linq;
namespace RATF.Domain {
    public sealed class Target {
        public EntityState Entity;
        public int GateLane;
        public bool Refinery;
        public float X;
        public long Key=>Entity!=null?Entity.Id:GateLane>0?-GateLane:-4;
    }
    public sealed class TargetingSystem {
        public Target Find(SimulationWorld w,EntityState e) {
            var u=w.Units[e.DefinitionId];
            var s=w.State;
            if(u.Support||s.Tick<e.ActiveTick)return null;
            var enemies=s.Entities.Where(o=>o.Lane==e.Lane&&o.Id!=e.Id&&!w.Units[o.DefinitionId].Support&&w.Units[o.DefinitionId].Factory!=u.Factory);
            if(u.Factory) {
                var adult=enemies.OrderBy(o=>Math.Abs(o.X-e.X)).ThenBy(o=>o.Id).FirstOrDefault();
                return adult!=null&&Math.Abs(adult.X-e.X)<=u.Range?new Target {
                    Entity=adult,X=adult.X
                }
                :null;
            }
            var robot=enemies.Where(o=>!w.Units[o.DefinitionId].Machine&&s.Tick>=o.ActiveTick&&Math.Abs(o.X-e.X)<=u.Range).OrderBy(o=>Math.Abs(o.X-e.X)).ThenBy(o=>o.Id).FirstOrDefault();
            if(robot!=null)return new Target {
                Entity=robot,X=robot.X
            };
            if(s.GateHp[e.Lane-1]>0)return new Target {
                GateLane=e.Lane,X=w.Level.Gate
            };
            foreach(var slot in s.Slots.Where(x=>x.Lane==e.Lane).OrderBy(x=>x.X))if(slot.Occupant!=0) {
                var machine=s.Entities.Find(x=>x.Id==slot.Occupant);
                if(machine!=null)return new Target {
                    Entity=machine,X=slot.X
                };
            }
            return new Target {
                Refinery=true,X=w.Level.Refinery
            };
        }
    }
    public sealed class MovementSystem {
        public void Step(SimulationWorld w) {
            var s=w.State;
            foreach(var e in s.Entities)e.PreviousX=e.X;
            var acquired=s.Entities.ToDictionary(e=>e.Id,e=>w.Targets.Find(w,e));
            foreach(var e in s.Entities.ToArray()) {
                var u=w.Units[e.DefinitionId];
                if(s.Tick<e.ActiveTick||u.Machine)continue;
                if(u.Support) {
                    if(s.Entities.Any(r=>r.Lane==e.Lane&&w.Units[r.DefinitionId].Factory&&!w.Units[r.DefinitionId].Machine&&s.Tick>=r.ActiveTick&&Math.Abs(r.PreviousX-e.PreviousX)<=1.5f)) {
                        w.Remove(e,"support_retreat");
                        continue;
                    }
                    var adult=s.Entities.Where(a=>a.Lane==e.Lane&&!w.Units[a.DefinitionId].Factory&&!w.Units[a.DefinitionId].Support&&a.PreviousX>e.PreviousX).OrderBy(a=>a.PreviousX-e.PreviousX).ThenBy(a=>a.Id).FirstOrDefault();
                    float goal=adult==null?w.Level.Spawn:Math.Max(w.Level.Spawn,adult.PreviousX-2);
                    e.X=Towards(e.X,goal,u.Speed/20);
                    continue;
                }
                var t=acquired[e.Id];
                e.Engaged=t!=null&&Math.Abs(t.X-e.X)<=u.Range+0.0001f;
                if(e.Engaged)continue;
                float target;
                if(u.Factory) {
                    var a=s.Entities.Where(a=>a.Lane==e.Lane&&!w.Units[a.DefinitionId].Factory&&!w.Units[a.DefinitionId].Support).OrderBy(a=>Math.Abs(a.PreviousX-e.PreviousX)).ThenBy(a=>a.Id).FirstOrDefault();
                    target=a==null?w.Level.Spawn:a.PreviousX;
                    target=Math.Max(s.GateHp[e.Lane-1]>0?w.Level.RobotBoundary:w.Level.Spawn,target);
                }
                else target=t!=null?t.X-u.Range:w.Level.Refinery-u.Range;
                e.X=Towards(e.X,target,u.Speed*w.Status.Speed(e,s.Tick)/20);
            }
        }
        public static float Towards(float x,float to,float step) {
            return x<to?Math.Min(to,x+step):Math.Max(to,x-step);
        }
    }
    public sealed class CombatSystem {
        public void Step(SimulationWorld w) {
            var s=w.State;
            var damage=new Dictionary<long,int>();
            var gate=new int[3];
            int refinery=0;
            foreach(var e in s.Entities.ToArray()) {
                var u=w.Units[e.DefinitionId];
                if(u.Support||s.Tick<e.ActiveTick)continue;
                var t=w.Targets.Find(w,e);
                if(t==null||Math.Abs(t.X-e.X)>u.Range+.0001f) {
                    e.AttackTick=s.Tick;
                    continue;
                }
                if(s.Tick<e.AttackTick)continue;
                bool aura=!u.Factory&&s.Entities.Any(a=>w.Units[a.DefinitionId].Support&&a.Lane==e.Lane&&Math.Abs(a.X-e.X)<=2.5f);
                e.AttackTick=s.Tick+(aura?(int)Math.Ceiling(u.Interval/1.15):u.Interval);
                w.Emit("attack",e.Id);
                if(t.GateLane>0)gate[t.GateLane-1]+=u.Damage*u.StructureMultiplier;
                else if(t.Refinery)refinery+=u.Damage*u.StructureMultiplier;
                else if(u.Id=="foam_cannon") {
                    foreach(var a in s.Entities.Where(a=>a.Lane==e.Lane&&!w.Units[a.DefinitionId].Factory&&!w.Units[a.DefinitionId].Support&&Math.Abs(a.X-t.X)<=1)) {
                        Add(damage,a.Id,u.Damage);
                        a.SlowUntil=s.Tick+w.Definition.SlowDuration;
                    }
                    w.Emit("foam",e.Id);
                }
                else Add(damage,t.Entity.Id,u.Damage*(w.Units[t.Entity.DefinitionId].Machine?u.StructureMultiplier:1));
            }
            foreach(var p in damage) {
                var e=s.Entities.Find(x=>x.Id==p.Key);
                if(e!=null)e.Hp-=p.Value;
            }
            for(int i=0;    i<3;    i++) {
                int old=s.GateHp[i];
                s.GateHp[i]=Math.Max(0,old-gate[i]);
                if(old>0&&s.GateHp[i]==0) {
                    s.GatesBreached++;
                    s.GateTicks[i]=s.Tick;
                    w.Emit("gate_open",i+1);
                }
            }
            s.RefineryHp=Math.Max(0,s.RefineryHp-refinery);
            foreach(var e in s.Entities.Where(x=>!w.Units[x.DefinitionId].Support&&x.Hp<=0).ToArray())w.Remove(e,w.Units[e.DefinitionId].Factory?"breakdown":"retreat");
        }
        static void Add(Dictionary<long,int> d,long id,int n) {
            d[id]=(d.TryGetValue(id,out var old)?old:0)+n;
        }
    }
    public sealed class SimulationWorld {
        public readonly MatchDefinition Definition;
        public readonly LevelDefinition Level;
        public readonly Dictionary<string,UnitDefinition> Units;
        public readonly MatchState State;
        public readonly TargetingSystem Targets=new TargetingSystem();
        public readonly StatusSystem Status=new StatusSystem();
        readonly ResourceSystem resources=new ResourceSystem();
        readonly SpawnSystem spawns=new SpawnSystem();
        readonly MovementSystem movement=new MovementSystem();
        readonly CombatSystem combat=new CombatSystem();
        readonly OutcomeSystem outcome=new OutcomeSystem();
        readonly CommandValidator validator=new CommandValidator();
        public readonly List<WorldEvent> Events=new List<WorldEvent>();
        public SimulationWorld(int epoch=1,MatchDefinition definition=null,LevelDefinition level=null,Dictionary<string,UnitDefinition> units=null) {
            Definition=definition??new MatchDefinition();
            Level=level??new LevelDefinition();
            Units=units??MatchDefinition.Units();
            State=new MatchState(epoch,Definition,Level);
        }
        public CommandResult Submit(PlayerCommand c) {
            var r=validator.Validate(this,c);
            if(!r.Accepted)return r;
            var s=State;
            if(c.Rally) {
                s.Rage-=Definition.RallyCost;
                s.RallyReady=s.Tick+Definition.RallyCooldown;
                foreach(var a in s.Entities.Where(x=>x.Lane==c.Lane&&!Units[x.DefinitionId].Factory&&!Units[x.DefinitionId].Support))a.RallyUntil=s.Tick+Definition.RallyDuration;
                Emit("rally",c.Lane);
                return r;
            }
            var u=Units[c.Unit];
            if(u.Factory) {
                s.Oil-=u.Cost*1000;
                s.FactoryReady=s.Tick+Definition.PurchaseCooldown;
            }
            else {
                s.Rage-=u.Cost*1000;
                s.PlayerReady=s.Tick+Definition.PurchaseCooldown;
                s.Deployments++;
                s.LaneDeployments[c.Lane-1]++;
            }
            s.UnitUsage[u.Id]=(s.UnitUsage.TryGetValue(u.Id,out var count)?count:0)+1;
            var e=new EntityState {
                Id=s.NextId++,DefinitionId=u.Id,Lane=c.Lane,Hp=u.Hp,X=u.Factory?Level.RobotSpawn:Level.Spawn,ActiveTick=s.Tick+u.BuildTicks,Slot=c.Slot
            };
            if(u.Machine) {
                var slot=s.Slots.Find(x=>x.Id==c.Slot);
                e.X=slot.X;
                slot.Occupant=e.Id;
                slot.Revision++;
                slot.State="Constructing";
            }
            e.PreviousX=e.X;
            s.Entities.Add(e);
            Emit("spawn",e.Id);
            return r;
        }
        public void Step() {
            if(State.Lifecycle!=Lifecycle.Running)return;
            State.Tick++;
            spawns.Step(this);
            resources.Step(State,Definition);
            movement.Step(this);
            combat.Step(this);
            outcome.Step(this);
        }
        public void Remove(EntityState e,string kind) {
            State.Entities.Remove(e);
            if(!Units[e.DefinitionId].Factory)State.Retreats++;
            if(!string.IsNullOrEmpty(e.Slot)) {
                var slot=State.Slots.Find(x=>x.Id==e.Slot);
                slot.Occupant=0;
                slot.State="Empty";
                slot.Revision++;
            }
            Emit(kind,e.Id);
        }
        public void Emit(string kind,long id=0) {
            Events.Add(new WorldEvent(State.Tick,kind,id));
        }
        public MatchSnapshot Snapshot() {
            return new MatchSnapshot {
                Epoch=State.Epoch,Tick=State.Tick,Rage=State.Rage,Oil=State.Oil,RefineryHp=State.RefineryHp,Lifecycle=State.Lifecycle,Gates=(int[])State.GateHp.Clone(),GateTicks=(int[])State.GateTicks.Clone(),LaneDeployments=(int[])State.LaneDeployments.Clone(),UnitUsage=new Dictionary<string,int>(State.UnitUsage),Entities=State.Entities.Select(e=>e.Copy()).ToArray(),Slots=State.Slots.Select(e=>e.Copy()).ToArray(),Deployments=State.Deployments,Retreats=State.Retreats,GatesBreached=State.GatesBreached
            };
        }
    }
}
