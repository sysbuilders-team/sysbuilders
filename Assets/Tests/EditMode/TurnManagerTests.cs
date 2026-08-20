using NUnit.Framework;
using Core.Turns;

namespace Tests.EditMode
{
    public class TurnManagerTests
    {
        [Test]
        public void Constructor_WhenInitialized_StartsAtTurnOneAndShowPhase()
        {
            // Arrange & Act
            var turnManager = new TurnManager();

            // Assert
            Assert.AreEqual(1, turnManager.CurrentTurn);
            Assert.AreEqual(TurnPhase.Show, turnManager.CurrentPhase);
            Assert.IsEmpty(turnManager.ReadyPlayers);
        }

        [Test]
        public void SetPlayerReady_OnlyOnePlayer_DoesntAdvancePhase()
        {
            // Arrange
            var turnManager = new TurnManager();
            
            // Act
            turnManager.SetPlayerReady(PlayerSlot.PlayerOne);
            
            // Assert
            Assert.AreEqual(TurnPhase.Show, turnManager.CurrentPhase);
            Assert.AreEqual(false, turnManager.IsBothPlayersReady());
            
            
        }

        [Test]
        public void SetPlayerReady_CallingTwoTimesFromSamePlayer_DoesntAdvancePhase()
        {
            // Arrange
            var turnManager = new TurnManager();
            
            // Act
            turnManager.SetPlayerReady(PlayerSlot.PlayerOne);
            turnManager.SetPlayerReady(PlayerSlot.PlayerOne);
            
            // Assert
            Assert.AreEqual(TurnPhase.Show, turnManager.CurrentPhase);
            Assert.IsFalse(turnManager.IsBothPlayersReady());
            Assert.IsEmpty(turnManager.ReadyPlayers);
        }

        [Test]
        public void SetPlayerReady_CallingTwoTimesFromBothPlayers_AdvancePhase()
        {
            // Arrange
            var turnManager = new TurnManager();
            
            // Act
            turnManager.SetPlayerReady(PlayerSlot.PlayerOne);
            turnManager.SetPlayerReady(PlayerSlot.PlayerTwo);
            
            // Assert
            Assert.AreNotEqual(TurnPhase.Show, turnManager.CurrentPhase);
            Assert.AreEqual(TurnPhase.Build, turnManager.CurrentPhase);
        }

        [Test]
        public void AdvancingPhase_CallingOnLastTurn_GoToCompletedGame()
        {
            // Arrange
            var turnManager = new TurnManager();
            
            // Act
            for (int i = 0; i < 15; i++)
            {
                turnManager.ForceAdvancePhase();
            }
            
            // Assert
            Assert.AreEqual(TurnPhase.Completed, turnManager.CurrentPhase);
        }

        [Test]
        public void AdvancingPhase_CallingWhenGameCompleted_DoesntBreakGame()
        {
            // Arrange
            var turnManager = new TurnManager();
            TurnPhase beforePhase;
            TurnPhase afterPhase;
            
            // Act
            for (int i = 0; i < 15; i++)
            {
                turnManager.ForceAdvancePhase();
            }

            beforePhase = turnManager.CurrentPhase;
            
            turnManager.ForceAdvancePhase();

            afterPhase = turnManager.CurrentPhase;
            
            
            // Assert
            Assert.AreEqual(TurnPhase.Completed, turnManager.CurrentPhase);
            Assert.AreEqual(beforePhase, afterPhase);
        }
    }
}