using System;
using System.Linq;
using UnityEngine;
using Forester.Domain;
namespace Forester.Presentation {
    [CreateAssetMenu(menuName="Forester/Card Catalog")] public sealed class CardCatalogSO:ScriptableObject {
        public CardDefinitionSO[] cards;
        public DefenderDefinitionSO[] defenders;
        public EnemyDefinitionSO[] enemies;
        public ConditionDefinitionSO[] conditions;
        public Catalog Copy() {
            var c=new Catalog();
            foreach(var x in cards)c.Cards.Add(x.definition.Id,x.Copy());
            foreach(var x in defenders)c.Defenders.Add(x.definition.Id,x.Copy());
            foreach(var x in enemies)c.Enemies.Add(x.definition.Id,x.Copy());
            foreach(var x in conditions)c.Conditions.Add(x.definition.Id,x.Copy());
            c.Validate();
            return c;
        }
    }
}
