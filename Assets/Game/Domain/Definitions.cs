using System;
using System.Collections.Generic;
namespace RATF.Domain {
    public enum Lifecycle {
        Running, Paused, Won, Lost, Disposed
    }
    public sealed class UnitDefinition {
        public readonly string Id;
        public readonly int Cost,Hp,Damage,Interval,BuildTicks;
        public readonly float Speed,Range;
        public readonly bool Factory,Machine,Support;
        public readonly int StructureMultiplier;
        public UnitDefinition(string id,int cost,int hp,float speed,int damage,int interval,float range,bool factory=false,bool machine=false,bool support=false,int multiplier=1,int buildTicks=0) {
            Id=id;
            Cost=cost;
            Hp=hp;
            Speed=speed;
            Damage=damage;
            Interval=interval;
            Range=range;
            Factory=factory;
            Machine=machine;
            Support=support;
            StructureMultiplier=multiplier;
            BuildTicks=buildTicks;
        }
    }
    public sealed class MatchDefinition {
        public readonly int DurationTicks,RageInitial,OilInitial,ResourceCap,RagePerTick,OilPerTick,PlayerCap,RobotCap,PurchaseCooldown,RallyCost,RallyCooldown,RallyDuration,SlowDuration,GateHp,RefineryHp;
        public MatchDefinition(int durationTicks=7200,int rageInitial=50000,int oilInitial=60000,int resourceCap=120000,int ragePerTick=300,int oilPerTick=250,int playerCap=60,int robotCap=24,int purchaseCooldown=20,int rallyCost=35000,int rallyCooldown=400,int rallyDuration=100,int slowDuration=40,int gateHp=300,int refineryHp=1000) {
            DurationTicks=durationTicks;
            RageInitial=rageInitial;
            OilInitial=oilInitial;
            ResourceCap=resourceCap;
            RagePerTick=ragePerTick;
            OilPerTick=oilPerTick;
            PlayerCap=playerCap;
            RobotCap=robotCap;
            PurchaseCooldown=purchaseCooldown;
            RallyCost=rallyCost;
            RallyCooldown=rallyCooldown;
            RallyDuration=rallyDuration;
            SlowDuration=slowDuration;
            GateHp=gateHp;
            RefineryHp=refineryHp;
        }
        public static Dictionary<string,UnitDefinition> Units() {
            var a=new[] {
                new UnitDefinition("villager_emak",30,220,1,10,20,.8f),new UnitDefinition("villager_bapak",40,120,1.2f,12,20,.8f,multiplier:2),new UnitDefinition("villager_anak",20,0,1.4f,0,0,2.5f,support:true),new UnitDefinition("security_robot",25,100,1.3f,12,20,.8f,factory:true,buildTicks:20),new UnitDefinition("foam_cannon",40,150,0,4,30,5,factory:true,machine:true,buildTicks:40),new UnitDefinition("bolt_turret",50,180,0,18,20,5,factory:true,machine:true,buildTicks:40)
            };
            var d=new Dictionary<string,UnitDefinition>();
            foreach(var u in a)d.Add(u.Id,u);
            return d;
        }
    }
    public sealed class LevelDefinition {
        public readonly float Spawn,Gate,RobotBoundary,RobotSpawn,Refinery;
        public readonly float[] LaneY,PadX;
        public LevelDefinition(float spawn=1.5f,float gate=13,float robotBoundary=13.8f,float robotSpawn=22.5f,float refinery=24,float[] laneY=null,float[] padX=null) {
            Spawn=spawn;
            Gate=gate;
            RobotBoundary=robotBoundary;
            RobotSpawn=robotSpawn;
            Refinery=refinery;
            LaneY=(float[])(laneY??new[] {
                9f,6f,3f
            }).Clone();
            PadX=(float[])(padX??new[] {
                16f,19f
            }).Clone();
            if(LaneY.Length!=3||PadX.Length!=2)throw new ArgumentException("Exactly three lanes and two pads required");
        }
    }
}
