using System;
using System.Collections.Generic;
using System.Linq;
namespace Forester.Domain {
    public sealed class ThreatPlan {
        public int schema_version=1,wave_index;
        public string request_id,announcement="";
        public ThreatGroup[] groups;
        public PlannedCondition[] conditions=new PlannedCondition[0];
    }
    public sealed class ThreatGroup {
        public string enemy_id,spawn_id,route_id;
        public int count,start_seconds,interval_seconds;
    }
    public sealed class PlannedCondition {
        public string condition_id;
        public int start_seconds;
    }
    public sealed class SpawnEvent {
        public int Tick,Order;
        public string EnemyId,RouteId;
    }
    public sealed class FrozenCondition {
        public int StartTick,EndTick;
        public string Id;
        public double SpeedMultiplier,Telegraph;
    }
    public sealed class ValidatedPlan {
        internal readonly SpawnEvent[] Events;
        internal readonly FrozenCondition[] Conditions;
        public readonly string Announcement;
        public readonly int Cost,Count;
        internal ValidatedPlan(SpawnEvent[] e,FrozenCondition[] c,string a,int cost) {
            Events=e;
            Conditions=c;
            Announcement=a;
            Cost=cost;
            Count=e.Length;
        }
        public SpawnEvent[] Schedule=>Events.Select(e=>new SpawnEvent {
            Tick=e.Tick,Order=e.Order,EnemyId=e.EnemyId,RouteId=e.RouteId
        }).ToArray();
        public FrozenCondition[] ConditionSchedule=>Conditions.Select(c=>new FrozenCondition {
            StartTick=c.StartTick,EndTick=c.EndTick,Id=c.Id,SpeedMultiplier=c.SpeedMultiplier,Telegraph=c.Telegraph
        }).ToArray();
    }
    public static class ThreatPlanValidator {
        public static ValidatedPlan Validate(ThreatPlan p,LevelDefinition l,Catalog c,int wave,string request) {
            if(p==null||p.schema_version!=1||p.wave_index!=wave||p.request_id!=request||string.IsNullOrWhiteSpace(request)||request.Length>80||wave<1||wave>l.Waves.Length)throw new ArgumentException("Plan binding/schema mismatch");
            if(p.groups==null||p.groups.Length<1||p.groups.Length>6||p.conditions==null||p.conditions.Length>1||p.announcement==null||p.announcement.Length>120||p.announcement.Any(ch=>char.IsControl(ch)||ch=='<'||ch=='>'))throw new ArgumentException("Invalid plan structure/text");
            var rule=l.Waves[wave-1];
            var events=new List<SpawnEvent>();
            var density=new HashSet<string>();
            int cost=0;
            foreach(var g in p.groups) {
                if(g==null||!rule.Enemies.Contains(g.enemy_id)||!rule.Routes.Contains(g.route_id)||!c.Enemies.TryGetValue(g.enemy_id,out var enemy)||g.count<1||g.count>24||g.start_seconds<0||g.start_seconds>48||g.interval_seconds<1||g.interval_seconds>48)throw new ArgumentException("Illegal group");
                var route=l.Routes.SingleOrDefault(r=>r.Id==g.route_id);
                if(route==null||route.SpawnId!=g.spawn_id)throw new ArgumentException("Spawn-route mismatch");
                for(int i=0;      i<g.count;      i++) {
                    int t=g.start_seconds+i*g.interval_seconds;
                    if(t>rule.Window||!density.Add(g.spawn_id+":"+t))throw new ArgumentException("Schedule window/density");
                    events.Add(new SpawnEvent {
                        Tick=t*20,Order=events.Count,EnemyId=g.enemy_id,RouteId=g.route_id
                    });
                    cost+=enemy.Cost;
                    if(events.Count>rule.MaxCount||cost>rule.Budget)throw new ArgumentException("Budget/count exceeded");
                }
            }
            var conditions=new List<FrozenCondition>();
            foreach(var x in p.conditions) {
                if(x==null||!rule.Conditions.Contains(x.condition_id)||!c.Conditions.TryGetValue(x.condition_id,out var def)||x.start_seconds<5||x.start_seconds>20)throw new ArgumentException("Illegal condition");
                cost+=def.Cost;
                conditions.Add(new FrozenCondition {
                    Id=x.condition_id,StartTick=x.start_seconds*20,EndTick=x.start_seconds*20+(int)Math.Round(def.Duration*20),SpeedMultiplier=def.SpeedMultiplier,Telegraph=def.Telegraph
                });
            }
            if(cost>rule.Budget)throw new ArgumentException("Condition exceeds budget");
            return new ValidatedPlan(events.OrderBy(e=>e.Tick).ThenBy(e=>e.Order).ToArray(),conditions.ToArray(),p.announcement,cost);
        }
    }
    public static class ScriptedPlans {
        public static ThreatPlan Create(int wave,string request) {
            var groups=new List<ThreatGroup>();
            Action<string,string,int,int,int> add=(enemy,route,count,start,interval)=>groups.Add(new ThreatGroup {
                enemy_id=enemy,spawn_id="S_"+route,route_id=route,count=count,start_seconds=start,interval_seconds=interval
            });
            if(wave==1) {
                add("emberling","NORTH",6,0,2);
                add("brush_cluster","NORTH",2,14,4);
            }
            else {
                foreach(var r in new[] {
                    "NORTH","SOUTH"
                })add("emberling",r,wave==2?4:5,0,2);
                foreach(var r in new[] {
                    "NORTH","SOUTH"
                })add("wind_runner",r,wave==2?2:3,12,wave==2?4:3);
                foreach(var r in new[] {
                    "NORTH","SOUTH"
                })add("brush_cluster",r,1,wave==2?22:24,1);
            }
            return new ThreatPlan {
                request_id=request,wave_index=wave,groups=groups.ToArray(),conditions=wave==3?new[] {
                    new PlannedCondition {
                        condition_id="gust_front",start_seconds=10
                    }
                }
                :new PlannedCondition[0],announcement=wave==1?"Emberlings approach the north trail.":wave==2?"Both trails are under pressure.":"Gust Front arrives at second 10."
            };
        }
    }
}
