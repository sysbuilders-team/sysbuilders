using System.Collections.Generic;
using Core.Evaluation;
using Core.Session;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class MatchScoreTrackerTests
    {
        [Test]
        public void MatchScoreTracker_CalculatesScoresAndDeltasCorrectly()
        {
            var p1 = new PlayerState("Alice");
            var p2 = new PlayerState("Bob");
            var tracker = new MatchScoreTracker(p1, p2, PlayerSlot.PlayerOne);

            Assert.AreEqual("Alice", tracker.PlayerOneName);
            Assert.AreEqual("Bob", tracker.PlayerTwoName);
            Assert.AreEqual(0, tracker.PlayerOneScore);
            Assert.AreEqual(0, tracker.PlayerTwoScore);
            Assert.IsTrue(tracker.IsDraw);
            Assert.IsNull(tracker.WinnerName);

            // Turn 1 evaluation for P1: Raw = 100, Penalty = 10 -> Score = 90
            var res1 = new EvaluationResult(100, 10, new List<string>(), new List<SmellOccurrence>());
            tracker.RecordEvaluation(p1, res1);

            Assert.AreEqual(90, tracker.PlayerOneScore);
            Assert.AreEqual(100, tracker.RoundPointsEarned);
            Assert.AreEqual(10, tracker.RoundPenaltyPoints);
            Assert.IsFalse(tracker.IsDraw);
            Assert.AreEqual("Alice", tracker.WinnerName);

            // Turn 1 evaluation for P2 via ReceiveOpponentResult: Raw = 100, Penalty = 0 -> Score = 100
            var resP2 = new EvaluationResult(100, 0, new List<string>(), new List<SmellOccurrence>());
            bool received = tracker.ReceiveOpponentResult(1, resP2);
            Assert.IsTrue(received);
            Assert.AreEqual(100, tracker.PlayerTwoScore);
            Assert.AreEqual(100, tracker.RoundOpponentPointsEarned);
            Assert.AreEqual(0, tracker.RoundOpponentPenaltyPoints);
            Assert.AreEqual("Bob", tracker.WinnerName);

            // Out of order opponent result is rejected
            bool invalidOrder = tracker.ReceiveOpponentResult(3, resP2);
            Assert.IsFalse(invalidOrder);
        }

        [Test]
        public void Score_WhenPenaltiesExceedRaw_FloorsAtZero()
        {
            var p1 = new PlayerState("Alice");
            var p2 = new PlayerState("Bob");
            var tracker = new MatchScoreTracker(p1, p2, PlayerSlot.PlayerOne);

            tracker.RecordEvaluation(p1, new EvaluationResult(
                100, 200, new List<string>(), new List<SmellOccurrence>()));

            Assert.AreEqual(0, tracker.PlayerOneScore,
                "Score must floor at zero when penalties exceed raw.");
        }
    }
}
