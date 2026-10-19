using System;
using Network;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class MatchStartCoordinatorTests
    {
        [Test]
        public void Resolve_WithThemeAndTrail_Succeeds()
        {
            MatchStartPlan plan = MatchStartCoordinator.Resolve("Biblioteca", theme => theme + "-trail");

            Assert.IsTrue(plan.Succeeded);
            Assert.AreEqual("Biblioteca", plan.Theme);
            Assert.AreEqual("Biblioteca-trail", plan.TrailId);
            Assert.IsNull(plan.FailureReason);
        }

        [Test]
        public void Resolve_WithoutTheme_Fails()
        {
            MatchStartPlan plan = MatchStartCoordinator.Resolve("  ", _ => "trail");

            Assert.IsFalse(plan.Succeeded);
            StringAssert.Contains("Tema", plan.FailureReason);
        }

        [Test]
        public void Resolve_WithoutTrailDrawer_Fails()
        {
            MatchStartPlan plan = MatchStartCoordinator.Resolve("Biblioteca", null);

            Assert.IsFalse(plan.Succeeded);
            StringAssert.Contains("RequirementManager", plan.FailureReason);
        }

        [Test]
        public void Resolve_WithEmptyTrail_Fails()
        {
            MatchStartPlan plan = MatchStartCoordinator.Resolve("Biblioteca", _ => string.Empty);

            Assert.IsFalse(plan.Succeeded);
            StringAssert.Contains("Nenhuma trilha", plan.FailureReason);
        }

        [Test]
        public void Resolve_WhenDrawerThrows_FailsWithMessage()
        {
            MatchStartPlan plan = MatchStartCoordinator.Resolve("Biblioteca",
                _ => throw new InvalidOperationException("trilha quebrada"));

            Assert.IsFalse(plan.Succeeded);
            Assert.AreEqual("trilha quebrada", plan.FailureReason);
        }
    }
}
