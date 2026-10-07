using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RATF.Domain;
using RATF.Application;
using RATF.Infrastructure;
namespace RATF.Tests {
    public static class ProviderCases {
        static void Check(bool ok,string why) {
            if(!ok)throw new Exception(why);
        }
        static void Reject(Action a,string why) {
            try {
                a();
            }
            catch(FormatException) {
                return;
            }
            catch(Newtonsoft.Json.JsonException) {
                return;
            }
            throw new Exception(why);
        }
        sealed class Http:IHttpTransport {
            public string Response,Body;
            public Exception Error;
            public Task<string> PostAsync(string e,string json,double t,string token,CancellationToken ct) {
                ct.ThrowIfCancellationRequested();
                Body=json;
                return Error!=null?Task.FromException<string>(Error):Task.FromResult(Response);
            }
        }
        sealed class Commander:IFactoryCommander {
            public Func<FactoryObservation,Task<FactoryPlan>> Respond;
            public Task<FactoryPlan> DecideAsync(FactoryObservation o,CancellationToken ct) {
                return Respond(o);
            }
        }
        static FactoryPlan Robot(FactoryObservation o,int count=1) {
            return new FactoryPlan {
                request_id=o.request_id,actions=Enumerable.Range(0,count).Select(i=>new FactoryAction {
                    type="deploy",unit="security_robot",lane=i+1,slot=""
                }).ToArray()
            };
        }
        public static void Parser() {
            var valid=PlanJson.Serialize(new FactoryPlan {
                request_id="r"
            });
            Check(PlanJson.Parse(valid).actions.Length==0,"Valid wait");
            foreach(var json in new[] {
                "garbage",valid.Replace("\"actions\":[]","\"actions\":null"),valid.Replace("\"schema_version\":1","\"schema_version\":1,\"schema_version\":1"),valid.Replace("\"schema_version\":1","\"schema_version\":true"),valid.Replace("\"actions\":[]","\"actions\":[],\"code\":\"x\"")
            })Reject(()=>PlanJson.Parse(json),"Reject malformed JSON");
        }
        public static void LocalValid() {
            var o=new SnapshotBuilder().Build(new SimulationWorld(),"m","r");
            var h=new Http {
                Response=new JObject {
                    ["message"]=new JObject {
                        ["content"]=PlanJson.Serialize(Robot(o))
                    }
                }.ToString()
            };
            var p=new LocalOllamaCommander(h,"http://127.0.0.1:11434/api/chat","installed-model").DecideAsync(o,CancellationToken.None).GetAwaiter().GetResult();
            Check(p.request_id=="r","Envelope");
            var body=JObject.Parse(h.Body);
            Check(body["format"] is JObject,"Schema object, not placeholder");
            Check(!(bool)body["stream"],"Stateless nonstream");
            Check(((JArray)body["messages"]).Count==2,"Two messages");
        }
        public static void Invalid200() {
            var o=new SnapshotBuilder().Build(new SimulationWorld(),"m","r");
            var h=new Http {
                Response="{\"message\":{\"content\":\"not JSON\"}}"
            };
            Reject(()=>new LocalOllamaCommander(h,"http://localhost/api/chat","model").DecideAsync(o,CancellationToken.None).GetAwaiter().GetResult(),"Invalid 200");
            h.Response="{\"arbitrary\":1}";
            Reject(()=>new RemoteGatewayCommander(h,"https://gateway.example/factory/decide","").DecideAsync(o,CancellationToken.None).GetAwaiter().GetResult(),"Invalid gateway 200");
        }
        public static void HttpFallback() {
            foreach(var status in new[] {
                "401","429","500","local offline","gateway offline"
            }) {
                var w=new SimulationWorld();
                var h=new Http {
                    Error=new InvalidOperationException(status)
                };
                IFactoryCommander provider=status=="gateway offline"? (IFactoryCommander)new RemoteGatewayCommander(h,"https://gateway.example/factory/decide",""):new LocalOllamaCommander(h,"http://localhost/api/chat","model");
                using(var c=new FactoryDecisionCoordinator(w,provider,()=>0,mode:"http")) {
                    c.Pump();
                    c.Pump();
                    Check(w.State.Entities.Count==1,"Fallback "+status);
                }
            }
        }
        public static void WrongRequestFallback() {
            var w=new SimulationWorld();
            var fake=new Commander {
                Respond=o=>Task.FromResult(new FactoryPlan {
                    request_id="wrong"
                })
            };
            using(var c=new FactoryDecisionCoordinator(w,fake,()=>0,mode:"test")) {
                c.Pump();
                c.Pump();
                Check(w.State.Entities.Count==1,"Wrong request fallback");
            }
        }
        public static void IntentionalWait() {
            var w=new SimulationWorld();
            var fake=new Commander {
                Respond=o=>Task.FromResult(new FactoryPlan {
                    request_id=o.request_id
                })
            };
            using(var c=new FactoryDecisionCoordinator(w,fake,()=>0,mode:"test")) {
                c.Pump();
                c.Pump();
                Check(w.State.Entities.Count==0&&c.Status=="waiting","Wait doesn't fall back");
            }
        }
        public static void QueueRevalidate() {
            var w=new SimulationWorld();
            var fake=new Commander {
                Respond=o=>Task.FromResult(Robot(o,2))
            };
            using(var c=new FactoryDecisionCoordinator(w,fake,()=>0,mode:"test")) {
                c.Pump();
                c.Pump();
                Check(w.State.Entities.Count==1,"First action");
                c.Pump();
                Check(w.State.Entities.Count==1,"No duplicate settlement");
                for(int i=0;    i<20;    i++)w.Step();
                w.State.Oil=0;
                c.Pump();
                Check(w.State.Entities.Count==1,"Second balance revalidation");
            }
        }
        public static void StaleRevision() {
            var w=new SimulationWorld();
            var source=new TaskCompletionSource<FactoryPlan>();
            FactoryObservation seen=null;
            var fake=new Commander {
                Respond=o=> {
                    seen=o;
                    return source.Task;
                }
            };
            using(var c=new FactoryDecisionCoordinator(w,fake,()=>0,mode:"test")) {
                c.Pump();
                var slot=w.State.Slots[0];
                slot.Revision=2;
                source.SetResult(new FactoryPlan {
                    request_id=seen.request_id,actions=new[] {
                        new FactoryAction {
                            type="build",unit="bolt_turret",lane=1,slot=slot.Id
                        }
                    }
                });
                c.Pump();
                Check(slot.Occupant==0,"Rebuild revision stale");
            }
        }
        public static void OccupiedAndCap() {
            foreach(bool cap in new[] {
                false,true
            }) {
                var w=new SimulationWorld(definition:new MatchDefinition(robotCap:1));
                var source=new TaskCompletionSource<FactoryPlan>();
                FactoryObservation seen=null;
                var fake=new Commander {
                    Respond=o=> {
                        seen=o;
                        return source.Task;
                    }
                };
                using(var c=new FactoryDecisionCoordinator(w,fake,()=>0,mode:"test")) {
                    c.Pump();
                    var action=cap?Robot(seen).actions[0]:new FactoryAction {
                        type="build",unit="bolt_turret",lane=1,slot="L1_S1"
                    };
                    w.Submit(cap?new PlayerCommand("security_robot",2):new PlayerCommand("foam_cannon",1,"L1_S1"));
                    for(int i=0;    i<20;    i++)w.Step();
                    w.State.Oil=120000;
                    source.SetResult(new FactoryPlan {
                        request_id=seen.request_id,actions=new[] {
                            action
                        }
                    });
                    c.Pump();
                    Check(w.State.Entities.Count==1,"Cap/occupied revalidation");
                }
            }
        }
        public static void PauseRestartPending() {
            var w=new SimulationWorld();
            var source=new TaskCompletionSource<FactoryPlan>();
            FactoryObservation seen=null;
            var fake=new Commander {
                Respond=o=> {
                    seen=o;
                    return source.Task;
                }
            };
            using(var controller=new MatchController(w,fake,()=>0,mode:"test")) {
                controller.Advance(.05);
                controller.Pause(true);
                source.SetResult(Robot(seen));
                controller.Advance(.05);
                Check(w.State.Entities.Count==0,"Pause rejects completion");
                controller.Dispose();
                Check(w.State.Lifecycle==Lifecycle.Disposed,"Restart dispose old world");
                var fresh=new SimulationWorld(epoch:10);
                Check(fresh.State.Entities.Count==0,"Fresh epoch");
            }
        }
        public static void QueueExpiry() {
            var w=new SimulationWorld();
            var fake=new Commander {
                Respond=o=>Task.FromResult(Robot(o,2))
            };
            using(var c=new FactoryDecisionCoordinator(w,fake,()=>0,mode:"test")) {
                c.Pump();
                c.Pump();
                w.State.FactoryReady=200;
                for(int i=0;    i<100;    i++)w.Step();
                c.Pump();
                Check(w.State.Entities.Count==1,"Expired action removed");
            }
        }
        public static Action[] All=>new Action[] {
            Parser,LocalValid,Invalid200,HttpFallback,WrongRequestFallback,IntentionalWait,QueueRevalidate,StaleRevision,OccupiedAndCap,PauseRestartPending,QueueExpiry
        };
    }
}
