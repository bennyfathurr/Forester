using UnityEngine;
namespace RATF.Presentation {
    public enum ProviderMode {
        Scripted,LocalOllama,RemoteGateway
    }
    [CreateAssetMenu(menuName="RATF/AI profile")] public sealed class ProviderProfileSO:ScriptableObject {
        public ProviderMode mode;
        public string endpoint="http://127.0.0.1:11434/api/chat",model="";
        public float timeout=8;
        public int decisionIntervalTicks=100;
        public bool schemaCapability=true;
    }
}
