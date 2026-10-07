using System;
using System.Collections.Generic;
using System.Linq;
using RATF.Domain;
namespace RATF.Application {
    public sealed class SnapshotBuilder {
        public FactoryObservation Build(SimulationWorld w,string matchId,string requestId,int ttl=100) {
            var s=w.State;
            var lanes=new List<ObservedLane>();
            var legal=new List<LegalOption>();
            for(int i=1;    i<=3;    i++) {
                int lane=i;
                Func<EntityState,ObservedEntity> map=e=>new ObservedEntity {
                    id=e.Id,unit=e.DefinitionId,path_distance=e.X,hp=e.Hp
                };
                lanes.Add(new ObservedLane {
                    lane=i,gate_hp=s.GateHp[i-1],villagers=s.Entities.Where(e=>e.Lane==lane&&!w.Units[e.DefinitionId].Factory).Select(map).ToArray(),robots=s.Entities.Where(e=>e.Lane==lane&&w.Units[e.DefinitionId].Factory&&!w.Units[e.DefinitionId].Machine).Select(map).ToArray(),slots=s.Slots.Where(x=>x.Lane==lane).Select(x=>new ObservedSlot {
                        id=x.Id,state=x.State,revision=x.Revision
                    }).ToArray()
                });
                if(s.Lifecycle!=Lifecycle.Running||s.Tick<s.FactoryReady)continue;
                foreach(var u in w.Units.Values.Where(u=>u.Factory&&s.Oil>=u.Cost*1000)) {
                    if(u.Machine) {
                        foreach(var slot in s.Slots.Where(x=>x.Lane==lane&&x.Occupant==0))legal.Add(new LegalOption {
                            action="build",unit=u.Id,lane=lane,slot=slot.Id,cost=u.Cost
                        });
                    }
                    else if(s.Entities.Count(e=>w.Units[e.DefinitionId].Factory&&!w.Units[e.DefinitionId].Machine)<w.Definition.RobotCap)legal.Add(new LegalOption {
                        action="deploy",unit=u.Id,lane=lane,slot="",cost=u.Cost
                    });
                }
            }
            return new FactoryObservation {
                match_id=matchId,request_id=requestId,epoch=s.Epoch,issued_tick=s.Tick,expires_tick=s.Tick+ttl,oil=s.Oil/1000f,factory_cooldown_ticks=Math.Max(0,s.FactoryReady-s.Tick),seconds_remaining=Math.Max(0,(w.Definition.DurationTicks-s.Tick)/20),refinery_hp=s.RefineryHp,lanes=lanes.ToArray(),legal_options=legal.ToArray()
            };
        }
    }
    public sealed class ScriptedCommander : IFactoryCommander {
        public System.Threading.Tasks.Task<FactoryPlan> DecideAsync(FactoryObservation o,System.Threading.CancellationToken ct) {
            ct.ThrowIfCancellationRequested();
            return System.Threading.Tasks.Task.FromResult(Decide(o));
        }
        public FactoryPlan Decide(FactoryObservation o) {
            var actions=new List<FactoryAction>();
            float budget=o.oil;
            var lanes=o.lanes.OrderByDescending(l=>l.villagers.Length>0).ThenByDescending(l=>l.villagers.Length==0?0:l.villagers.Max(v=>v.path_distance)).ThenBy(l=>l.villagers.Length==0?l.slots.Count(s=>s.state!="Empty")+l.robots.Length:l.gate_hp).ThenBy(l=>l.lane);
            foreach(var lane in lanes) {
                foreach(var unit in new[] {
                    "bolt_turret","foam_cannon","security_robot"
                }) {
                    var option=o.legal_options.Where(x=>x.lane==lane.lane&&x.unit==unit&&x.cost<=budget&&!actions.Any(a=>a.slot!=""&&a.slot==x.slot)).OrderBy(x=>x.slot).FirstOrDefault();
                    if(option==null)continue;
                    actions.Add(new FactoryAction {
                        type=option.action,unit=option.unit,lane=option.lane,slot=option.slot
                    });
                    budget-=option.cost;
                    break;
                }
                if(actions.Count==2)break;
            }
            return new FactoryPlan {
                request_id=o.request_id,actions=actions.ToArray(),announcement="Safety inspection in progress."
            };
        }
    }
    public sealed class FactoryPlanValidator {
        public string Validate(FactoryPlan p,FactoryObservation o) {
            if(p==null||p.schema_version!=1||p.request_id!=o.request_id)return "Invalid schema or request ID";
            if(p.actions==null||p.actions.Length>2||p.announcement==null||p.announcement.Length>160)return "Invalid shape";
            float budget=o.oil;
            var slots=new HashSet<string>();
            foreach(var a in p.actions) {
                if(a==null||a.lane<1||a.lane>3)return "Invalid lane";
                if(a.type=="build") {
                    if((a.unit!="foam_cannon"&&a.unit!="bolt_turret")||!o.lanes.Any(l=>l.lane==a.lane&&l.slots.Any(s=>s.id==a.slot&&s.state=="Empty"))||!slots.Add(a.slot))return "Invalid build";
                }
                else if(a.type!="deploy"||a.unit!="security_robot"||a.slot!="")return "Unknown action";
                var option=o.legal_options.FirstOrDefault(x=>x.action==a.type&&x.unit==a.unit&&x.lane==a.lane&&x.slot==a.slot);
                if(option==null)return "Not observed legal option";
                budget-=option.cost;
                if(budget<0)return "Combined cost exceeds Oil";
            }
            return null;
        }
    }
}
