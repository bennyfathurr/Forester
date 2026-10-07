using System.Linq;
namespace RATF.Domain {
    public sealed class CommandValidator {
        public CommandResult Validate(SimulationWorld w,PlayerCommand c) {
            var s=w.State;
            var d=w.Definition;
            if(s.Lifecycle!=Lifecycle.Running)return CommandResult.Reject("Match is not running");
            if(c.Lane<1||c.Lane>3)return CommandResult.Reject("Invalid lane");
            if(c.Rally) {
                if(s.Rage<d.RallyCost)return CommandResult.Reject("Insufficient Rage");
                if(s.Tick<s.RallyReady)return CommandResult.Reject("Rally cooldown");
                return CommandResult.Ok();
            }
            if(!w.Units.TryGetValue(c.Unit??"",out var u))return CommandResult.Reject("Unknown unit");
            if((u.Factory?s.Oil:s.Rage)<u.Cost*1000)return CommandResult.Reject(u.Factory?"Insufficient Oil":"Insufficient Rage");
            if(s.Tick<(u.Factory?s.FactoryReady:s.PlayerReady))return CommandResult.Reject("Purchase cooldown");
            if(!u.Factory&&s.Entities.Count(e=>!w.Units[e.DefinitionId].Factory)>=d.PlayerCap)return CommandResult.Reject("Villager cap");
            if(u.Factory&&!u.Machine&&s.Entities.Count(e=>w.Units[e.DefinitionId].Factory&&!w.Units[e.DefinitionId].Machine)>=d.RobotCap)return CommandResult.Reject("Robot cap");
            if(u.Machine) {
                var slot=s.Slots.FirstOrDefault(x=>x.Id==c.Slot&&x.Lane==c.Lane);
                if(slot==null)return CommandResult.Reject("Invalid slot");
                if(c.ExpectedRevision>=0&&slot.Revision!=c.ExpectedRevision)return CommandResult.Reject("Stale slot revision");
                if(slot.Occupant!=0)return CommandResult.Reject("Occupied slot");
            }
            else if(!string.IsNullOrEmpty(c.Slot))return CommandResult.Reject("Deploy requires empty slot");
            return CommandResult.Ok();
        }
    }
    public sealed class ResourceSystem {
        public void Step(MatchState s,MatchDefinition d) {
            s.Rage=System.Math.Min(d.ResourceCap,s.Rage+d.RagePerTick);
            s.Oil=System.Math.Min(d.ResourceCap,s.Oil+d.OilPerTick);
        }
    }
    public sealed class SpawnSystem {
        public void Step(SimulationWorld w) {
            foreach(var slot in w.State.Slots)if(slot.Occupant!=0) {
                var e=w.State.Entities.Find(x=>x.Id==slot.Occupant);
                if(e!=null)slot.State=w.State.Tick>=e.ActiveTick?"Active":"Constructing";
            }
        }
    }
    public sealed class StatusSystem {
        public float Speed(EntityState e,int tick) {
            return (tick<e.SlowUntil?.65f:1)*(tick<e.RallyUntil?1.3f:1);
        }
    }
    public sealed class OutcomeSystem {
        public void Step(SimulationWorld w) {
            if(w.State.RefineryHp<=0)w.State.Lifecycle=Lifecycle.Won;
            else if(w.State.Tick>=w.Definition.DurationTicks)w.State.Lifecycle=Lifecycle.Lost;
        }
    }
}
