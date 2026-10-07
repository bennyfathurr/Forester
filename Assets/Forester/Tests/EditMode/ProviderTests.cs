using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using NUnit.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Forester.Domain;
using Forester.Application;
using Forester.Infrastructure;
using RATF.Infrastructure;
namespace Forester.Tests {
    public sealed class ProviderTests {
        sealed class Transport:IHttpTransport {
            public string Response,Body,Token;
            public Task<string> PostAsync(string e,string j,double timeout,string token,CancellationToken ct) {
                Body=j;
                Token=token;
                return Task.FromResult(Response);
            }
        }
        static ThreatObservation Observation()=>ThreatObservationBuilder.Build(new SimulationWorld(Defaults.Level(),Defaults.Catalog()),"match",1,"req");
        ProviderOptions Options()=>new ProviderOptions {
            Endpoint="http://127.0.0.1:8000/v1/chat/completions",Schema=File.ReadAllText("Assets/Forester/Data/Providers/threat_plan.schema.json")
        };
        [Test]public async Task LocalEnvelopeAndPlainJson() {
            var t=new Transport {
                Response=JsonConvert.SerializeObject(new {
                    choices=new[] {
                        new {
                            message=new {
                                content=JsonConvert.SerializeObject(ScriptedPlans.Create(1,"req"))
                            }
                        }
                    }
                })
            };
            var p=await new LocalApertusCommander(t,Options()).PlanAsync(Observation(),CancellationToken.None);
            Assert.That(p.request_id,Is.EqualTo("req"));
            var body=JObject.Parse(t.Body);
            Assert.That(body["response_format"],Is.Null);
            Assert.That(body["temperature"],Is.Null);
            Assert.That(body["max_tokens"],Is.Null);
            Assert.That(body["model"].Value<string>(),Does.Contain("Apertus"));
        }
        [Test]public async Task ExplicitCapabilitiesOnly() {
            var o=Options();
            o.VerifiedSchemaOutput=o.SendTemperature=o.SendMaxTokens=true;
            var t=new Transport {
                Response=JsonConvert.SerializeObject(new {
                    choices=new[] {
                        new {
                            message=new {
                                content=JsonConvert.SerializeObject(ScriptedPlans.Create(1,"req"))
                            }
                        }
                    }
                })
            };
            await new LocalApertusCommander(t,o).PlanAsync(Observation(),CancellationToken.None);
            var b=JObject.Parse(t.Body);
            Assert.That(b["response_format"]["type"].Value<string>(),Is.EqualTo("json_schema"));
            Assert.That(b["max_tokens"].Value<int>(),Is.EqualTo(700));
        }
        [Test]public void RefusalRejected() {
            var t=new Transport {
                Response="{\"choices\":[{\"message\":{\"refusal\":\"no\",\"content\":\"{}\"}}]}"
            };
            Assert.ThrowsAsync<FormatException>(async()=>await new LocalApertusCommander(t,Options()).PlanAsync(Observation(),CancellationToken.None));
        }
        [Test]public void MissingContentRejected() {
            var t=new Transport {
                Response="{\"choices\":[]}"
            };
            Assert.ThrowsAsync<FormatException>(async()=>await new LocalApertusCommander(t,Options()).PlanAsync(Observation(),CancellationToken.None));
        }
        [Test]public void ExactJsonNoMarkdownExtraFieldsOrFractions() {
            string good=JsonConvert.SerializeObject(ScriptedPlans.Create(1,"req"));
            Assert.Throws<JsonReaderException>(()=>ThreatJson.Parse("```json\n"+good+"\n```"));
            var o=JObject.Parse(good);
            o["code"]="oops";
            Assert.Throws<FormatException>(()=>ThreatJson.Parse(o.ToString()));
            o.Remove("code");
            o["groups"][0]["count"]=1.5;
            Assert.Throws<FormatException>(()=>ThreatJson.Parse(o.ToString()));
        }
        [Test]public void DuplicatePropertiesRejected() {
            Assert.Throws<JsonReaderException>(()=>ThreatJson.Parse("{\"schema_version\":1,\"schema_version\":1}"));
        }
        [Test]public async Task RemoteOnlyObservationAndSessionToken() {
            var t=new Transport {
                Response=JsonConvert.SerializeObject(ScriptedPlans.Create(1,"req"))
            };
            var o=Options();
            o.Endpoint="https://owned.example/forester/plan";
            o.SessionToken="test-memory";
            await new RemoteApertusCommander(t,o).PlanAsync(Observation(),CancellationToken.None);
            Assert.That(t.Token,Is.EqualTo("test-memory"));
            Assert.That(t.Body,Does.Not.Contain("SystemPrompt"));
            Assert.That(t.Body,Does.Not.Contain("Assets/"));
            Assert.That(t.Body,Does.Not.Contain("test-memory"));
        }
        [Test]public void InsecureOrNonlocalEndpointsRejected() {
            Assert.Throws<ArgumentException>(()=>ThreatJson.ValidateEndpoint("http://public.example/v1/chat/completions",true));
            Assert.Throws<ArgumentException>(()=>ThreatJson.ValidateEndpoint("https://public.example/v1/chat/completions",false));
        }
    }
}
