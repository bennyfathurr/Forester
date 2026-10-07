using System;
using System.Linq;
using UnityEngine;
using Forester.Domain;
namespace Forester.Presentation {
    [CreateAssetMenu(menuName="Forester/Condition")] public sealed class ConditionDefinitionSO:ScriptableObject {
        public ConditionDefinition definition=new ConditionDefinition();
        public ConditionDefinition Copy()=>new ConditionDefinition {
            Id=definition.Id,Name=definition.Name,Cost=definition.Cost,Duration=definition.Duration,SpeedMultiplier=definition.SpeedMultiplier,Telegraph=definition.Telegraph
        };
    }
}
