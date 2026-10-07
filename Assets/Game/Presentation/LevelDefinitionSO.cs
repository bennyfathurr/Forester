using UnityEngine;
using RATF.Domain;
namespace RATF.Presentation {
    [CreateAssetMenu(menuName="RATF/Level definition")] public sealed class LevelDefinitionSO:ScriptableObject {
        public float spawn=1.5f,gate=13,robotBoundary=13.8f,robotSpawn=22.5f,refinery=24;
        public float[] laneY= {
            9,6,3
        },padX= {
            16,19
        };
        public LevelDefinition ToDefinition() {
            return new LevelDefinition(spawn,gate,robotBoundary,robotSpawn,refinery,laneY,padX);
        }
    }
}
