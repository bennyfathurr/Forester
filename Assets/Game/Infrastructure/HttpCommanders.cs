using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RATF.Application;
namespace RATF.Infrastructure {
    public interface IHttpTransport {
        Task<string> PostAsync(string endpoint,string json,double timeout,string sessionToken,CancellationToken ct);
    }
    public sealed class LocalOllamaCommander:IFactoryCommander {
        readonly IHttpTransport transport;
        readonly string endpoint,model;
        readonly double timeout;
        public LocalOllamaCommander(IHttpTransport transport,string endpoint,string model,double timeout=8) {
            this.transport=transport;
            this.endpoint=endpoint;
            this.model=model;
            this.timeout=timeout;
            if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||(uri.Scheme!="http"&&uri.Scheme!="https"))throw new ArgumentException("Invalid local endpoint");
        }
        public async Task<FactoryPlan> DecideAsync(FactoryObservation o,CancellationToken ct) {
            if(string.IsNullOrWhiteSpace(model))throw new InvalidOperationException("Model ID is not configured");
            var body=new JObject {
                ["model"]=model,["stream"]=false,["messages"]=new JArray(new JObject {
                    ["role"]="system",["content"]=PlanJson.SystemPrompt+"\n"+PlanJson.Schema
                },new JObject {
                    ["role"]="user",["content"]=PlanJson.Serialize(o)
                }),["format"]=JObject.Parse(PlanJson.Schema),["options"]=new JObject {
                    ["temperature"]=0,["num_predict"]=384
                }
            };
            var result=PlanJson.Object(await transport.PostAsync(endpoint,body.ToString(Newtonsoft.Json.Formatting.None),timeout,"",ct));
            if(result["message"]?["content"]?.Type!=JTokenType.String)throw new FormatException("Unsupported schema output");
            return PlanJson.Parse((string)result["message"]["content"]);
        }
    }
    public sealed class RemoteGatewayCommander:IFactoryCommander {
        readonly IHttpTransport transport;
        readonly string endpoint,token;
        readonly double timeout;
        public RemoteGatewayCommander(IHttpTransport transport,string endpoint,string sessionToken,double timeout=4) {
            if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||uri.Scheme!="https")throw new ArgumentException("Hosted gateway requires HTTPS");
            this.transport=transport;
            this.endpoint=endpoint;
            token=sessionToken;
            this.timeout=timeout;
        }
        public async Task<FactoryPlan> DecideAsync(FactoryObservation o,CancellationToken ct) {
            return PlanJson.Parse(await transport.PostAsync(endpoint,PlanJson.Serialize(o),timeout,token,ct));
        }
    }
}
