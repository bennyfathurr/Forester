using UnityEngine;
using RATF.Domain;
namespace RATF.Presentation {
    [CreateAssetMenu(menuName="RATF/Unit definition")] public sealed class UnitDefinitionSO:ScriptableObject {
        public string id;
        public int cost,hp,damage,intervalTicks,buildTicks,structureMultiplier=1;
        public float speed,range;
        public bool factory,machine,support;
        public UnitDefinition ToDefinition() {
            return new UnitDefinition(id,cost,hp,speed,damage,intervalTicks,range,factory,machine,support,structureMultiplier,buildTicks);
        }
        public void From(UnitDefinition d) {
            id=d.Id;
            cost=d.Cost;
            hp=d.Hp;
            speed=d.Speed;
            damage=d.Damage;
            intervalTicks=d.Interval;
            range=d.Range;
            factory=d.Factory;
            machine=d.Machine;
            support=d.Support;
            structureMultiplier=d.StructureMultiplier;
            buildTicks=d.BuildTicks;
        }
    }
}
