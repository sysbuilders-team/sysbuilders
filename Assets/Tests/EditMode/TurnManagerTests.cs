using NUnit.Framework;
using Core.Turns;

namespace Tests.EditMode
{
    public class TurnManagerTests
    {
        [Test]
        public void Constructor_WhenInitialized_StartsAtTurnOneAndShowPhase()
        {
            var turnManager = new TurnManager();

            Assert.AreEqual(1, turnManager.CurrentTurn);
            Assert.AreEqual(TurnPhase.Show, turnManager.CurrentPhase);
        }

        [Test]
        public void SetPlayerReady_OnlyOnePlayer_DoesntAdvancePhase()
        {
            var turnManager = new TurnManager();

            turnManager.SetPlayerReady(PlayerSlot.PlayerOne);

            Assert.AreEqual(TurnPhase.Show, turnManager.CurrentPhase);
            Assert.AreEqual(false, turnManager.IsBothPlayersReady());
        }

        [Test]
        public void SetPlayerReady_CallingTwoTimesFromSamePlayer_DoesntAdvancePhase()
        {
            var turnManager = new TurnManager();

            turnManager.SetPlayerReady(PlayerSlot.PlayerOne);
            turnManager.SetPlayerReady(PlayerSlot.PlayerOne);

            Assert.AreEqual(TurnPhase.Show, turnManager.CurrentPhase);
            Assert.IsFalse(turnManager.IsBothPlayersReady());
        }

        [Test]
        public void SetPlayerReady_CallingTwoTimesFromBothPlayers_AdvancePhase()
        {
            var turnManager = new TurnManager();

            turnManager.SetPlayerReady(PlayerSlot.PlayerOne);
            turnManager.SetPlayerReady(PlayerSlot.PlayerTwo);

            Assert.AreNotEqual(TurnPhase.Show, turnManager.CurrentPhase);
            Assert.AreEqual(TurnPhase.Build, turnManager.CurrentPhase);
        }

        [Test]
        public void AdvancingPhase_CallingOnLastTurn_GoToCompletedGame()
        {
            var turnManager = new TurnManager();

            for (int i = 0; i < 15; i++)
            {
                turnManager.ForceAdvancePhase();
            }

            Assert.AreEqual(TurnPhase.Completed, turnManager.CurrentPhase);
        }

        [Test]
        public void AdvancingPhase_CallingWhenGameCompleted_DoesntBreakGame()
        {
            var turnManager = new TurnManager();
            TurnPhase beforePhase;
            TurnPhase afterPhase;

            for (int i = 0; i < 15; i++)
            {
                turnManager.ForceAdvancePhase();
            }

            beforePhase = turnManager.CurrentPhase;

            turnManager.ForceAdvancePhase();

            afterPhase = turnManager.CurrentPhase;

            Assert.AreEqual(TurnPhase.Completed, turnManager.CurrentPhase);
            Assert.AreEqual(beforePhase, afterPhase);
        }

        [Test]
        public void SetPlayerReady_FirstCallForPlayer_ReturnsSuccess()
        {
            var turnManager = new TurnManager();

            var result = turnManager.SetPlayerReady(PlayerSlot.PlayerOne);

            Assert.AreEqual(SetPlayerReadyResult.Success, result);
        }

        [Test]
        public void SetPlayerReady_CallingTwoTimesFromSamePlayer_ReturnsAlreadyReadyOnSecondCall()
        {
            var turnManager = new TurnManager();

            var firstResult = turnManager.SetPlayerReady(PlayerSlot.PlayerOne);
            var secondResult = turnManager.SetPlayerReady(PlayerSlot.PlayerOne);

            Assert.AreEqual(SetPlayerReadyResult.Success, firstResult);
            Assert.AreEqual(SetPlayerReadyResult.AlreadyReady, secondResult);
        }

        [Test]
        public void SetPlayerReady_WhenMatchAlreadyCompleted_ReturnsMatchAlreadyCompleted()
        {
            var turnManager = new TurnManager();
            for (int i = 0; i < 15; i++)
            {
                turnManager.ForceAdvancePhase();
            }

            var result = turnManager.SetPlayerReady(PlayerSlot.PlayerOne);

            Assert.AreEqual(TurnPhase.Completed, turnManager.CurrentPhase);
            Assert.AreEqual(SetPlayerReadyResult.MatchAlreadyCompleted, result);
        }

        [Test]
        public void SetPlayerReady_AfterPhaseAdvances_ReadyPlayersResetsSoSamePlayerCanBeReadyAgain()
        {
            var turnManager = new TurnManager();
            turnManager.SetPlayerReady(PlayerSlot.PlayerOne);
            turnManager.SetPlayerReady(PlayerSlot.PlayerTwo);

            var result = turnManager.SetPlayerReady(PlayerSlot.PlayerOne);

            Assert.AreEqual(TurnPhase.Build, turnManager.CurrentPhase);
            Assert.AreEqual(SetPlayerReadyResult.Success, result);
        }
    }
}
