using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Forester.Domain;
using Forester.Application;
using RATF.Infrastructure;
namespace Forester.Infrastructure {
    public sealed class ProviderOptions {
        public string Endpoint,Model="swiss-ai/Apertus-8B-Instruct-2509",Schema,SessionToken;
        public double Timeout=8;
        public bool VerifiedSchemaOutput,SendTemperature,SendMaxTokens;
    }
    public static class ThreatJson {
        public const string SystemPrompt="You are the adaptive threat commander for Forester, a fictional forest-protection tower-defense game. Choose a finite wave using only the legal enemies, spawn-route pairs, condition IDs and limits in the observation. Observe defender placement and previous results to produce a strategically challenging but rule-compliant wave. Never exceed the wave threat budget, total enemy count, spawn rate or schedule window. Use up to six groups. The game computes costs and validates every expanded spawn. Return only the exact JSON plan schema, matching request_id and wave_index. Do not invent mechanics, modify defenses, change paths, issue code, or present real-world wildfire advice. Include at least one enemy and a short cosmetic announcement. Conditions are allowed only when explicitly provided in the observation.";
        static JObject Object(JToken t,string[] keys) {
            if(!(t is JObject o)||o.Properties().Count()!=keys.Length||keys.Any(k=>o[k]==null)||o.Properties().Any(p=>!keys.Contains(p.Name)))throw new FormatException("Exact plan properties required");
            return o;
        }
        static int Int(JToken t,int lo,int hi) {
            if(t.Type!=JTokenType.Integer)throw new FormatException("Integer required");
            long x=t.Value<long>();
            if(x<lo||x>hi)throw new FormatException("Integer out of bounds");
            return (int)x;
        }
        static string Str(JToken t,int max,int min=0) {
            if(t.Type!=JTokenType.String)throw new FormatException("String required");
            string s=t.Value<string>();
            if(s.Length<min||s.Length>max)throw new FormatException("String bounds");
            return s;
        }
        public static ThreatPlan Parse(string json) {
            if(json==null||json.Length>65536)throw new FormatException("Response size");
            JToken token;
            using(var reader=new JsonTextReader(new System.IO.StringReader(json))) {
                reader.MaxDepth=12;
                reader.DateParseHandling=DateParseHandling.None;
                token=JToken.Load(reader,new JsonLoadSettings {
                    DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error
                });
                if(reader.Read())throw new FormatException("Trailing JSON");
            }
            var root=Object(token,new[] {
                "schema_version","request_id","wave_index","groups","conditions","announcement"
            });
            if(!(root["groups"] is JArray gs)||gs.Count<1||gs.Count>6||!(root["conditions"] is JArray cs)||cs.Count>1)throw new FormatException("Array bounds");
            var p=new ThreatPlan {
                schema_version=Int(root["schema_version"],1,1),request_id=Str(root["request_id"],80,1),wave_index=Int(root["wave_index"],1,3),announcement=Str(root["announcement"],120),groups=gs.Select(t=> {
                    var o=Object(t,new[] {
                        "enemy_id","spawn_id","route_id","count","start_seconds","interval_seconds"
                    });      string e=Str(o["enemy_id"],40),s=Str(o["spawn_id"],40),r=Str(o["route_id"],40);      if(!new[] {
                        "emberling","wind_runner","brush_cluster"
                    }.Contains(e)||!new[] {
                        "S_NORTH","S_SOUTH"
                    }.Contains(s)||!new[] {
                        "NORTH","SOUTH"
                    }.Contains(r))throw new FormatException("Schema enum");      return new ThreatGroup {
                        enemy_id=e,spawn_id=s,route_id=r,count=Int(o["count"],1,24),start_seconds=Int(o["start_seconds"],0,48),interval_seconds=Int(o["interval_seconds"],1,48)
                    };
                }).ToArray(),conditions=cs.Select(t=> {
                    var o=Object(t,new[] {
                        "condition_id","start_seconds"
                    });      string id=Str(o["condition_id"],40);      if(id!="gust_front")throw new FormatException("Condition enum");      return new PlannedCondition {
                        condition_id=id,start_seconds=Int(o["start_seconds"],5,20)
                    };
                }).ToArray()
            };
            return p;
        }
        public static string ChatRequest(ThreatObservation o,ProviderOptions options) {
            var b=new JObject {
                ["model"]=options.Model,["stream"]=false,["messages"]=new JArray(new JObject {
                    ["role"]="system",["content"]=SystemPrompt+"\nExact JSON schema:\n"+options.Schema
                },new JObject {
                    ["role"]="user",["content"]=JsonConvert.SerializeObject(o)
                })
            };
            if(options.SendTemperature)b["temperature"]=.2;
            if(options.SendMaxTokens)b["max_tokens"]=700;
            if(options.VerifiedSchemaOutput)b["response_format"]=new JObject {
                ["type"]="json_schema",["json_schema"]=new JObject {
                    ["name"]="forester_threat_plan",["strict"]=true,["schema"]=JObject.Parse(options.Schema)
                }
            };
            return b.ToString(Formatting.None);
        }
        public static void ValidateEndpoint(string endpoint,bool remote) {
            if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Fragment)||!(uri.Scheme=="https"||uri.Scheme=="http"&&uri.IsLoopback)||!remote&&!uri.IsLoopback)throw new ArgumentException("Use loopback HTTP for local or HTTPS for remote");
        }
    }
    public sealed class LocalApertusCommander:IThreatCommander {
        readonly IHttpTransport transport;
        readonly ProviderOptions options;
        public LocalApertusCommander(IHttpTransport t,ProviderOptions o) {
            ThreatJson.ValidateEndpoint(o.Endpoint,false);
            if(string.IsNullOrWhiteSpace(o.Model))throw new ArgumentException("Model ID required");
            transport=t;
            options=o;
        }
        public async Task<ThreatPlan> PlanAsync(ThreatObservation obs,CancellationToken ct) {
            string response=await transport.PostAsync(options.Endpoint,ThreatJson.ChatRequest(obs,options),options.Timeout,null,ct);
            var root=JObject.Parse(response);
            var choices=root["choices"] as JArray;
            if(choices==null||choices.Count==0||!(choices[0]["message"] is JObject message)||message["refusal"]?.Type==JTokenType.String&&message["refusal"].Value<string>().Length>0||message["content"]?.Type!=JTokenType.String)throw new FormatException("Missing assistant content or refusal");
            return ThreatJson.Parse(message["content"].Value<string>());
        }
    }
    public sealed class RemoteApertusCommander:IThreatCommander {
        readonly IHttpTransport transport;
        readonly ProviderOptions options;
        public RemoteApertusCommander(IHttpTransport t,ProviderOptions o) {
            ThreatJson.ValidateEndpoint(o.Endpoint,true);
            transport=t;
            options=o;
        }
        public async Task<ThreatPlan> PlanAsync(ThreatObservation o,CancellationToken ct)=>ThreatJson.Parse(await transport.PostAsync(options.Endpoint,JsonConvert.SerializeObject(o),options.Timeout,options.SessionToken,ct));
    }
    public sealed class UnavailableCommander:IThreatCommander {
        readonly string reason;
        public UnavailableCommander(string r) {
            reason=r;
        }
        public Task<ThreatPlan> PlanAsync(ThreatObservation o,CancellationToken ct)=>Task.FromException<ThreatPlan>(new InvalidOperationException(reason));
    }
}
