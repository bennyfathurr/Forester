using System;
using System.Linq;
using UnityEngine;
using Forester.Domain;
namespace Forester.Presentation {
    [CreateAssetMenu(menuName="Forester/Defender")] public sealed class DefenderDefinitionSO:ScriptableObject {
        public DefenderDefinition definition=new DefenderDefinition();
        public DefenderDefinition Copy()=>new DefenderDefinition {
            Id=definition.Id,VisualId=definition.VisualId,Kind=definition.Kind,Damage=definition.Damage,Interval=definition.Interval,Range=definition.Range,SplashRadius=definition.SplashRadius,SlowMultiplier=definition.SlowMultiplier,SlowSeconds=definition.SlowSeconds,AuraMultiplier=definition.AuraMultiplier
        };
    }
}
