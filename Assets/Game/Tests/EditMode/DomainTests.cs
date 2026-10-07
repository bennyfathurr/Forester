using NUnit.Framework;
namespace RATF.Tests {
    public sealed class DomainTests {
        [Test] public void Costs() {
            RuleCases.Costs();
        }
        [Test] public void CapsCooldown() {
            RuleCases.CapsCooldown();
        }
        [Test] public void Construction() {
            RuleCases.Construction();
        }
        [Test] public void GateAndVictory() {
            RuleCases.GateAndVictory();
        }
        [Test] public void Simultaneous() {
            RuleCases.Simultaneous();
        }
        [Test] public void TieAndSupport() {
            RuleCases.TieAndSupport();
        }
        [Test] public void Effects() {
            RuleCases.Effects();
        }
        [Test] public void ConstructionDestroyed() {
            RuleCases.ConstructionDestroyed();
        }
        [Test] public void PauseSnapshot() {
            RuleCases.PauseSnapshot();
        }
        [Test] public void PlanValidation() {
            RuleCases.PlanValidation();
        }
        [Test] public void TimeoutLate() {
            RuleCases.TimeoutLate();
        }
        [Test] public void OfflineMatch() {
            RuleCases.OfflineMatch();
        }
        [Test] public void ProviderParser() {
            ProviderCases.Parser();
        }
        [Test] public void ProviderLocalValid() {
            ProviderCases.LocalValid();
        }
        [Test] public void ProviderInvalid200() {
            ProviderCases.Invalid200();
        }
        [Test] public void ProviderHttpFallback() {
            ProviderCases.HttpFallback();
        }
        [Test] public void ProviderWrongRequestFallback() {
            ProviderCases.WrongRequestFallback();
        }
        [Test] public void ProviderIntentionalWait() {
            ProviderCases.IntentionalWait();
        }
        [Test] public void ProviderQueueRevalidate() {
            ProviderCases.QueueRevalidate();
        }
        [Test] public void ProviderStaleRevision() {
            ProviderCases.StaleRevision();
        }
        [Test] public void ProviderOccupiedAndCap() {
            ProviderCases.OccupiedAndCap();
        }
        [Test] public void ProviderPauseRestartPending() {
            ProviderCases.PauseRestartPending();
        }
        [Test] public void ProviderQueueExpiry() {
            ProviderCases.QueueExpiry();
        }
        [Test] public void EmptyZeroResources() {
            RuleCases.EmptyZeroResources();
        }
        [Test] public void FoamAndSupportRetreat() {
            RuleCases.FoamAndSupportRetreat();
        }
        [Test] public void AttackDeadlineAndBoundary() {
            RuleCases.AttackDeadlineAndBoundary();
        }
    }
}
