using System;
using System.Linq;
using UnityEngine;
using Forester.Domain;
namespace Forester.Presentation {
    [CreateAssetMenu(menuName="Forester/Card")] public sealed class CardDefinitionSO:ScriptableObject {
        public CardDefinition definition=new CardDefinition();
        public Texture2D icon;
        public string[] placementTags= {
            "Buildable"
        };
        public CardDefinition Copy()=>new CardDefinition {
            Id=definition.Id,Name=definition.Name,Description=definition.Description,DefenderId=definition.DefenderId,Category=definition.Category,Cost=definition.Cost,MaxCopies=definition.MaxCopies,Width=definition.Width,Height=definition.Height
        };
    }
}
