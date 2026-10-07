using System;
using System.Linq;
using UnityEngine;
using Forester.Domain;
namespace Forester.Presentation {
    public enum ProviderMode {
        Scripted,LocalApertus,RemoteApertus
    }
    [Serializable] public sealed class ProviderSettings {
        public ProviderMode Mode;
        public string Endpoint="http://127.0.0.1:8000/v1/chat/completions",Model="swiss-ai/Apertus-8B-Instruct-2509";
        public double Timeout=8;
        public bool VerifiedSchemaOutput,SendTemperature,SendMaxTokens;
        [NonSerialized]public string SessionToken="";
        public ProviderSettings Copy()=>new ProviderSettings {
            Mode=Mode,Endpoint=Endpoint,Model=Model,Timeout=Timeout,VerifiedSchemaOutput=VerifiedSchemaOutput,SendTemperature=SendTemperature,SendMaxTokens=SendMaxTokens
        };
    }
    [CreateAssetMenu(menuName="Forester/Provider Profile")] public sealed class ProviderProfileSO:ScriptableObject {
        public ProviderSettings settings=new ProviderSettings();
        public TextAsset planSchema;
    }
}
