using NUnit.Framework;
namespace Forester.Tests {
    public sealed class DomainTests {
        [Test]public void OfflineRules() {
            RuleCases.Run();
            Assert.That(RuleCases.Passed,Is.EqualTo(52));
        }
    }
}
