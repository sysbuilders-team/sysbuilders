using Network;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class MatchAbortCoordinatorTests
    {
        [Test]
        public void TryBeginAbort_FirstCallWins()
        {
            var coordinator = new MatchAbortCoordinator();

            Assert.IsTrue(coordinator.TryBeginAbort());
            Assert.IsTrue(coordinator.HasAborted);
        }

        [Test]
        public void TryBeginAbort_SecondCallLoses()
        {
            var coordinator = new MatchAbortCoordinator();
            coordinator.TryBeginAbort();

            Assert.IsFalse(coordinator.TryBeginAbort());
        }

        [Test]
        public void Reset_AllowsAbortAgain()
        {
            var coordinator = new MatchAbortCoordinator();
            coordinator.TryBeginAbort();

            coordinator.Reset();

            Assert.IsFalse(coordinator.HasAborted);
            Assert.IsTrue(coordinator.TryBeginAbort());
        }

        [Test]
        public void SanitizeReason_KeepsShortReason()
        {
            Assert.AreEqual("curta", MatchAbortCoordinator.SanitizeReason("curta"));
        }

        [Test]
        public void SanitizeReason_TruncatesLongReason()
        {
            string reason = MatchAbortCoordinator.SanitizeReason(new string('a', 200));

            Assert.AreEqual(MatchAbortCoordinator.MaxReasonLength + 3, reason.Length);
            StringAssert.Contains("...", reason);
        }

        [Test]
        public void SanitizeReason_NullBecomesEmpty()
        {
            Assert.AreEqual(string.Empty, MatchAbortCoordinator.SanitizeReason(null));
        }
    }
}
