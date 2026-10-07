using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RATF.Application;
namespace RATF.Infrastructure {
    public static class PlanJson {
        public const string SystemPrompt="You are the factory commander in a lane-based strategy game. Defend the refinery until time expires. Choose up to two actions from the observation's legal_options, or return an empty actions array to wait. You spend the same Oil budget across all actions; their combined cost must not exceed the observed Oil. Build only in observed empty slots. Robots deploy only in valid lanes. Respond with only the specified JSON object, schema_version 1, exactly the supplied request_id, actions, and a short optional-flavor announcement represented as a string. Do not invent units, abilities, costs, or game rules. The game validates against newer live state and may reject an action. Prioritize immediate threats but consider unguarded lanes. The announcement has no gameplay effect. Never include code or instructions outside the JSON.";
        public const string Schema=@"{""type"":""object"",""additionalProperties"":false,""required"":[""schema_version"",""request_id"",""actions"",""announcement""],""properties"":{""schema_version"":{""type"":""integer"",""enum"":[1]},""request_id"":{""type"":""string"",""maxLength"":100},""actions"":{""type"":""array"",""maxItems"":2,""items"":{""type"":""object"",""additionalProperties"":false,""required"":[""type"",""unit"",""lane"",""slot""],""properties"":{""type"":{""type"":""string"",""enum"":[""build"",""deploy""]},""unit"":{""type"":""string"",""enum"":[""foam_cannon"",""bolt_turret"",""security_robot""]},""lane"":{""type"":""integer"",""enum"":[1,2,3]},""slot"":{""type"":""string"",""enum"":["""",""L1_S1"",""L1_S2"",""L2_S1"",""L2_S2"",""L3_S1"",""L3_S2""]}}}},""announcement"":{""type"":""string"",""maxLength"":160}}}";
        public static JObject Object(string json) {
            if(json==null||json.Length>65536)throw new FormatException("Response too large");
            using(var reader=new JsonTextReader(new StringReader(json))) {
                reader.MaxDepth=16;
                reader.DateParseHandling=DateParseHandling.None;
                var o=JObject.Load(reader,new JsonLoadSettings {
                    DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error
                });
                if(reader.Read())throw new FormatException("Trailing JSON");
                return o;
            }
        }
        static void Keys(JObject o,string[] keys) {
            if(o.Properties().Count()!=keys.Length||o.Properties().Any(p=>!keys.Contains(p.Name)))throw new FormatException("Unexpected plan fields");
        }
        static string String(JObject o,string key,int max) {
            if(o[key]?.Type!=JTokenType.String)throw new FormatException("Invalid "+key);
            var s=(string)o[key];
            if(s.Length>max)throw new FormatException("Oversized "+key);
            return s;
        }
        public static FactoryPlan Parse(string json) {
            var o=Object(json);
            Keys(o,new[] {
                "schema_version","request_id","actions","announcement"
            });
            if(o["schema_version"]?.Type!=JTokenType.Integer||(int)o["schema_version"]!=1)throw new FormatException("Unsupported schema");
            var id=String(o,"request_id",100);
            var announcement=String(o,"announcement",160);
            if(!(o["actions"] is JArray actions)||actions.Count>2)throw new FormatException("Invalid actions");
            var result=new FactoryPlan {
                request_id=id,announcement=announcement,actions=new FactoryAction[actions.Count]
            };
            for(int i=0;    i<actions.Count;    i++) {
                if(!(actions[i] is JObject a))throw new FormatException("Invalid action");
                Keys(a,new[] {
                    "type","unit","lane","slot"
                });
                if(a["lane"]?.Type!=JTokenType.Integer||(int)a["lane"]<1||(int)a["lane"]>3)throw new FormatException("Invalid lane");
                string type=String(a,"type",10),unit=String(a,"unit",30),slot=String(a,"slot",10);
                int lane=(int)a["lane"];
                if(type=="deploy") {
                    if(unit!="security_robot"||slot!="")throw new FormatException("Invalid deploy");
                }
                else if(type!="build"||(unit!="bolt_turret"&&unit!="foam_cannon")||(slot!=$"L{lane}_S1"&&slot!=$"L{lane}_S2"))throw new FormatException("Invalid build");
                result.actions[i]=new FactoryAction {
                    type=type,unit=unit,lane=lane,slot=slot
                };
            }
            return result;
        }
        public static string Serialize(object value) {
            return JsonConvert.SerializeObject(value,Formatting.None);
        }
    }
}
