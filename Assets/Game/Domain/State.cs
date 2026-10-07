using System;
using System.Collections.Generic;
namespace RATF.Domain {
    public sealed class EntityState {
        public long Id;
        public string DefinitionId,Slot;
        public int Lane,Hp,ActiveTick,AttackTick,SlowUntil,RallyUntil;
        public float X,PreviousX;
        public bool Engaged;
        public EntityState Copy() {
            return (EntityState)MemberwiseClone();
        }
    }
    public sealed class SlotState {
        public string Id;
        public int Lane,Revision;
        public float X;
        public long Occupant;
        public string State="Empty";
        public SlotState Copy() {
            return (SlotState)MemberwiseClone();
        }
    }
    public sealed class MatchState {
        public Lifecycle Lifecycle=Lifecycle.Running;
        public int Epoch,Tick,Rage,Oil,PlayerReady,FactoryReady,RallyReady,RefineryHp,Deployments,Retreats,GatesBreached;
        public long NextId=1;
        public readonly int[] GateHp=new int[3];
        public readonly int[] GateTicks= {
            -1,-1,-1
        };
        public readonly int[] LaneDeployments=new int[3];
        public readonly Dictionary<string,int> UnitUsage=new Dictionary<string,int>();
        public readonly List<EntityState> Entities=new List<EntityState>();
        public readonly List<SlotState> Slots=new List<SlotState>();
        public MatchState(int epoch,MatchDefinition d,LevelDefinition l) {
            Epoch=epoch;
            Rage=d.RageInitial;
            Oil=d.OilInitial;
            RefineryHp=d.RefineryHp;
            for(int i=0;    i<3;    i++) {
                GateHp[i]=d.GateHp;
                for(int j=0;    j<2;    j++)Slots.Add(new SlotState {
                    Id=$"L{i+1}_S{j+1}",Lane=i+1,X=l.PadX[j]
                });
            }
        }
    }
    public sealed class PlayerCommand {
        public string Unit,Slot;
        public int Lane;
        public bool Rally;
        public int ExpectedRevision=-1;
        public PlayerCommand(string unit,int lane,string slot="",bool rally=false,int revision=-1) {
            Unit=unit;
            Lane=lane;
            Slot=slot;
            Rally=rally;
            ExpectedRevision=revision;
        }
    }
    public sealed class CommandResult {
        public bool Accepted;
        public string Reason;
        public static CommandResult Ok() {
            return new CommandResult {
                Accepted=true,Reason="Accepted"
            };
        }
        public static CommandResult Reject(string reason) {
            return new CommandResult {
                Reason=reason
            };
        }
    }
    public sealed class WorldEvent {
        public readonly int Tick;
        public readonly string Kind;
        public readonly long Id;
        public WorldEvent(int tick,string kind,long id=0) {
            Tick=tick;
            Kind=kind;
            Id=id;
        }
    }
    public sealed class MatchSnapshot {
        public int Epoch,Tick,Rage,Oil,RefineryHp,Deployments,Retreats,GatesBreached;
        public Lifecycle Lifecycle;
        public int[] Gates,GateTicks,LaneDeployments;
        public Dictionary<string,int> UnitUsage;
        public EntityState[] Entities;
        public SlotState[] Slots;
    }
}
