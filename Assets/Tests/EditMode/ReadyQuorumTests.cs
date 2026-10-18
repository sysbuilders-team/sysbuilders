using System;
using Core.Session;
using Network;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class ReadyQuorumTests
    {
        [Test]
        public void Constructor_WithZero_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReadyQuorum(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReadyQuorum(-1));
        }

        [Test]
        public void MarkReady_SingleSlot_CountsOneQuorumNotMet()
        {
            var quorum = new ReadyQuorum(2);

            quorum.MarkReady(PlayerSlot.PlayerOne);

            Assert.AreEqual(1, quorum.ReadyCount);
            Assert.IsFalse(quorum.IsReadyQuorumMet);
        }

        [Test]
        public void MarkReady_TwoSlots_QuorumMet()
        {
            var quorum = new ReadyQuorum(2);

            quorum.MarkReady(PlayerSlot.PlayerOne);
            quorum.MarkReady(PlayerSlot.PlayerTwo);

            Assert.AreEqual(2, quorum.ReadyCount);
            Assert.IsTrue(quorum.IsReadyQuorumMet);
        }

        [Test]
        public void MarkReady_SameSlotTwice_DoesNotDoubleCount()
        {
            var quorum = new ReadyQuorum(2);

            quorum.MarkReady(PlayerSlot.PlayerOne);
            quorum.MarkReady(PlayerSlot.PlayerOne);

            Assert.AreEqual(1, quorum.ReadyCount);
            Assert.IsFalse(quorum.IsReadyQuorumMet);
        }

        [Test]
        public void ClearReady_ResetsReadyButNotEvaluated()
        {
            var quorum = new ReadyQuorum(2);
            quorum.MarkReady(PlayerSlot.PlayerOne);
            quorum.MarkEvaluated(PlayerSlot.PlayerTwo);

            quorum.ClearReady();

            Assert.AreEqual(0, quorum.ReadyCount);
            Assert.AreEqual(1, quorum.EvaluatedCount);
        }

        [Test]
        public void MarkEvaluated_TwoSlots_QuorumMet()
        {
            var quorum = new ReadyQuorum(2);

            quorum.MarkEvaluated(PlayerSlot.PlayerOne);
            quorum.MarkEvaluated(PlayerSlot.PlayerTwo);

            Assert.IsTrue(quorum.IsEvaluationQuorumMet);
        }

        [Test]
        public void ClearAll_ClearsBoth()
        {
            var quorum = new ReadyQuorum(2);
            quorum.MarkReady(PlayerSlot.PlayerOne);
            quorum.MarkEvaluated(PlayerSlot.PlayerTwo);

            quorum.ClearAll();

            Assert.AreEqual(0, quorum.ReadyCount);
            Assert.AreEqual(0, quorum.EvaluatedCount);
        }
    }
}
