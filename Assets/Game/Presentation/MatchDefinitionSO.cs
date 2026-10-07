using UnityEngine;
using RATF.Domain;
namespace RATF.Presentation {
    [CreateAssetMenu(menuName="RATF/Match definition")] public sealed class MatchDefinitionSO:ScriptableObject {
        public int durationTicks=7200,rageInitial=50000,oilInitial=60000,resourceCap=120000,ragePerTick=300,oilPerTick=250,playerCap=60,robotCap=24,purchaseCooldown=20,rallyCost=35000,rallyCooldown=400,rallyDuration=100,slowDuration=40,gateHp=300,refineryHp=1000;
        public MatchDefinition ToDefinition() {
            return new MatchDefinition(durationTicks,rageInitial,oilInitial,resourceCap,ragePerTick,oilPerTick,playerCap,robotCap,purchaseCooldown,rallyCost,rallyCooldown,rallyDuration,slowDuration,gateHp,refineryHp);
        }
    }
}
