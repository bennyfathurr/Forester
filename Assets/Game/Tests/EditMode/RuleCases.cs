using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RATF.Domain;
using RATF.Application;
namespace RATF.Tests {
    public static class RuleCases {
        static void Check(bool b,string message) {
            if(!b)throw new Exception(message);
        }
        static void Steps(SimulationWorld w,int n) {
            for(int i=0;    i<n;    i++)w.Step();
        }
        static EntityState Entity(SimulationWorld w,string unit,int lane,float x,int hp=-1) {
            var u=w.Units[unit];
            var e=new EntityState {
                Id=w.State.NextId++,DefinitionId=unit,Lane=lane,X=x,PreviousX=x,Hp=hp<0?u.Hp:hp
            };
            w.State.Entities.Add(e);
            return e;
        }
        public static void Costs() {
            var w=new SimulationWorld();
            w.State.Rage=30000;
            Check(w.Submit(new PlayerCommand("villager_emak",1)).Accepted,"Equality");
            Check(w.State.Rage==0,"Exact deduction");
            Steps(w,20);
            w.State.Rage=29999;
            Check(!w.Submit(new PlayerCommand("villager_emak",1)).Accepted&&w.State.Rage==29999,"Insufficient funds");
        }
        public static void CapsCooldown() {
            var w=new SimulationWorld(definition:new MatchDefinition(playerCap:1,robotCap:1));
            Check(w.Submit(new PlayerCommand("villager_anak",1)).Accepted,"Spawn");
            Check(!w.Submit(new PlayerCommand("villager_anak",2)).Accepted,"Cooldown");
            Steps(w,20);
            Check(!w.Submit(new PlayerCommand("villager_anak",2)).Accepted,"Cap");
            Check(w.Submit(new PlayerCommand("security_robot",1)).Accepted,"Robot reserve");
            Steps(w,20);
            Check(!w.Submit(new PlayerCommand("security_robot",2)).Accepted,"Reserved robot cap");
            Steps(w,1000);
            Check(w.State.Rage<=120000&&w.State.Oil<=120000,"Resource caps");
        }
        public static void Construction() {
            var w=new SimulationWorld();
            Check(w.Submit(new PlayerCommand("foam_cannon",1,"L1_S1")).Accepted,"Build");
            var slot=w.State.Slots[0];
            Check(slot.Revision==1&&slot.State=="Constructing","Reservation");
            Steps(w,39);
            Check(slot.State=="Constructing","Delay");
            w.Step();
            Check(slot.State=="Active","Completion");
            w.Remove(w.State.Entities[0],"breakdown");
            Check(slot.Revision==2&&slot.Occupant==0,"Freed");
            w.State.Oil=120000;
            Check(!w.Submit(new PlayerCommand("foam_cannon",1,slot.Id,revision:0)).Accepted,"Revision guard");
            Check(w.Submit(new PlayerCommand("foam_cannon",1,slot.Id,revision:2)).Accepted,"Rebuild");
        }
        public static void GateAndVictory() {
            var w=new SimulationWorld();
            w.State.GateHp[0]=24;
            Entity(w,"villager_bapak",1,12.2f);
            w.Step();
            Check(w.State.GateHp[0]==0&&w.State.GatesBreached==1,"Gate breach");
            var v=new SimulationWorld(definition:new MatchDefinition(durationTicks:1));
            v.State.GateHp[0]=0;
            v.State.RefineryHp=24;
            Entity(v,"villager_bapak",1,23.2f);
            v.Step();
            Check(v.State.Lifecycle==Lifecycle.Won,"Victory beats timeout");
        }
        public static void Simultaneous() {
            var w=new SimulationWorld();
            Entity(w,"villager_bapak",1,14,12);
            Entity(w,"security_robot",1,14.5f,12);
            w.Step();
            Check(w.State.Entities.Count==0,"Mutual damage simultaneous");
        }
        public static void TieAndSupport() {
            var w=new SimulationWorld();
            var a=Entity(w,"villager_emak",1,14);
            var b=Entity(w,"villager_bapak",1,15);
            var robot=Entity(w,"security_robot",1,14.5f);
            Check(w.Targets.Find(w,robot).Entity==a,"Lowest ID tie");
            w.State.Entities.Remove(robot);
            Entity(w,"villager_anak",1,12);
            Entity(w,"villager_anak",1,12.2f);
            w.State.GateHp[0]=300;
            a.X=12.2f;
            b.X=12.2f;
            w.Step();
            Check(a.AttackTick-w.State.Tick==18,"Nonstack aura");
            Check(w.State.Entities.Where(e=>e.DefinitionId=="villager_anak").All(e=>e.Hp==0),"Supports have no health");
        }
        public static void Effects() {
            var w=new SimulationWorld();
            var a=Entity(w,"villager_emak",1,5);
            a.SlowUntil=40;
            a.RallyUntil=100;
            Check(Math.Abs(w.Status.Speed(a,0)-.845f)<.0001,"Combined");
            Check(Math.Abs(w.Status.Speed(a,40)-1.3f)<.0001,"Slow expiry");
            Check(w.Status.Speed(a,100)==1,"Rally expiry");
            w.State.Rage=35000;
            Check(w.Submit(new PlayerCommand("",1,rally:true)).Accepted,"Rally equality");
            Check(a.RallyUntil==100,"Existing buff");
            w.State.Tick=20;
            w.State.Rage=50000;
            Check(w.Submit(new PlayerCommand("villager_bapak",1)).Accepted,"Later spawn");
            Check(w.State.Entities.Last().RallyUntil==0,"No inherited rally");
        }
        public static void ConstructionDestroyed() {
            var w=new SimulationWorld();
            w.State.GateHp[0]=0;
            w.Submit(new PlayerCommand("foam_cannon",1,"L1_S1"));
            var machine=w.State.Entities[0];
            machine.Hp=24;
            Entity(w,"villager_bapak",1,15.2f);
            w.Step();
            Check(!w.State.Entities.Contains(machine)&&w.State.Slots[0].State=="Empty","Destruction before activation");
        }
        public static void PauseSnapshot() {
            var w=new SimulationWorld();
            var c=new MatchController(w,new ScriptedCommander(),()=>0);
            c.Pause(true);
            int tick=w.State.Tick,epoch=w.State.Epoch;
            c.Advance(1);
            Check(w.State.Tick==tick,"Paused");
            c.Pause(false);
            Check(w.State.Epoch>epoch,"Epoch invalidation");
            var snap=c.Snapshot();
            snap.Gates[0]=0;
            Check(w.State.GateHp[0]==300,"Detached snapshot");
            c.Dispose();
        }
        public static void PlanValidation() {
            var w=new SimulationWorld();
            var o=new SnapshotBuilder().Build(w,"m","r");
            var v=new FactoryPlanValidator();
            var p=new FactoryPlan {
                request_id="r"
            };
            Check(v.Validate(p,o)==null,"Intentional wait");
            p.request_id="wrong";
            Check(v.Validate(p,o)!=null,"Request ID");
            p.request_id="r";
            p.actions=new[] {
                new FactoryAction {
                    type="deploy",unit="unknown",lane=1,slot=""
                }
            };
            Check(v.Validate(p,o)!=null,"Unknown unit");
            p.actions=new[] {
                new FactoryAction {
                    type="build",unit="bolt_turret",lane=1,slot="L1_S1"
                },new FactoryAction {
                    type="build",unit="bolt_turret",lane=2,slot="L2_S1"
                }
            };
            Check(v.Validate(p,o)!=null,"Combined budget");
        }
        sealed class PendingCommander:IFactoryCommander {
            public readonly TaskCompletionSource<FactoryPlan> Source=new TaskCompletionSource<FactoryPlan>();
            public FactoryObservation Seen;
            public Task<FactoryPlan> DecideAsync(FactoryObservation o,CancellationToken c) {
                Seen=o;
                return Source.Task;
            }
        }
        public static void TimeoutLate() {
            var w=new SimulationWorld();
            var fake=new PendingCommander();
            double time=0;
            using(var c=new FactoryDecisionCoordinator(w,fake,()=>time,mode:"local",timeout:4)) {
                c.Pump();
                Check(c.Status=="planning","Pending");
                time=5;
                c.Pump();
                Check(c.Status=="executing"||c.Status=="timeout/fallback","Fallback");
                int count=w.State.Entities.Count;
                fake.Source.SetResult(new FactoryPlan {
                    request_id=fake.Seen.request_id
                });
                c.Pump();
                Check(w.State.Entities.Count==count,"Late completion discarded");
            }
        }
        public static void OfflineMatch() {
            var w=new SimulationWorld();
            using(var c=new MatchController(w,new ScriptedCommander(),()=>0)) {
                for(int i=0;    i<7201&&w.State.Lifecycle==Lifecycle.Running;    i++) {
                    if(i%40==0)c.Submit(new PlayerCommand(i%80==0?"villager_emak":"villager_bapak",i/40%3+1));
                    c.Advance(.05);
                }
                Check(w.State.Lifecycle==Lifecycle.Won||w.State.Lifecycle==Lifecycle.Lost,"Full outcome");
                Check(w.State.Deployments>0&&w.State.Tick<=7200,"Valid match");
            }
        }
        public static void EmptyZeroResources() {
            var w=new SimulationWorld(definition:new MatchDefinition(rageInitial:0,oilInitial:0));
            Check(w.State.Entities.Count==0&&w.State.Slots.All(x=>x.State=="Empty"),"Empty scenario");
            Check(!w.Submit(new PlayerCommand("villager_emak",1)).Accepted,"No hidden resources");
            w.Step();
            Check(w.State.Rage==300&&w.State.Oil==250,"Per tick regeneration");
        }
        public static void FoamAndSupportRetreat() {
            var w=new SimulationWorld();
            w.State.GateHp[0]=150;
            w.Submit(new PlayerCommand("foam_cannon",1,"L1_S1"));
            w.State.Entities[0].ActiveTick=0;
            var a=Entity(w,"villager_emak",1,12.2f);
            var b=Entity(w,"villager_bapak",1,12.4f);
            var child=Entity(w,"villager_anak",1,10.1f);
            w.Step();
            Check(a.Hp==216&&b.Hp==116,"Foam area damage");
            Check(a.SlowUntil==41&&b.SlowUntil==41,"Foam refreshed duration");
            Check(w.State.Entities.Contains(child),"Child not target");
            Entity(w,"security_robot",1,10.5f);
            w.Step();
            Check(!w.State.Entities.Contains(child),"Child retreats near robot");
        }
        public static void AttackDeadlineAndBoundary() {
            var w=new SimulationWorld();
            var a=Entity(w,"villager_bapak",1,5);
            for(int i=0;    i<200;    i++) {
                a.X=5;
                w.Step();
            }
            Check(w.State.GateHp[0]==300,"No out-of-range damage");
            a.X=12.2f;
            w.Step();
            Check(w.State.GateHp[0]==276,"First acquired attack, no idle bank");
            w.Step();
            Check(w.State.GateHp[0]==276,"Cooldown respects deadline");
            var robot=Entity(w,"security_robot",2,14);
            for(int i=0;    i<100;    i++)w.Step();
            Check(robot.X>=13.8f,"Closed gate robot boundary");
            w.State.GateHp[1]=0;
            for(int i=0;    i<20;    i++)w.Step();
            Check(robot.X<13.8f,"Open gate permits traversal");
        }
        public static Action[] All=>new Action[] {
            Costs,CapsCooldown,Construction,GateAndVictory,Simultaneous,TieAndSupport,Effects,ConstructionDestroyed,PauseSnapshot,PlanValidation,TimeoutLate,OfflineMatch,EmptyZeroResources,FoamAndSupportRetreat,AttackDeadlineAndBoundary
        };
    }
}
