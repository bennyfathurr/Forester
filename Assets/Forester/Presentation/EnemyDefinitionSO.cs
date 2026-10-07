using System;
using System.Linq;
using UnityEngine;
using Forester.Domain;
namespace Forester.Presentation {
    [CreateAssetMenu(menuName="Forester/Threat")] public sealed class EnemyDefinitionSO:ScriptableObject {
        public EnemyDefinition definition=new EnemyDefinition();
        public EnemyDefinition Copy()=>new EnemyDefinition {
            Id=definition.Id,VisualId=definition.VisualId,Cost=definition.Cost,HP=definition.HP,Speed=definition.Speed,IntegrityLoss=definition.IntegrityLoss
        };
    }
}
