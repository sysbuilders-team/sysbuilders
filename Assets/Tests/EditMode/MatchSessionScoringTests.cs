using System.Collections.Generic;
using Core.Diagram;
using Core.Evaluation;
using Core.Requirements;
using Core.Session;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class MatchSessionScoringTests
    {
        // Devolve, em ordem, os resultados combinados -- uma chamada por avaliação de turno.
        private class ScriptedEvaluator : IEvaluationService
        {
            private readonly Queue<EvaluationResult> _results;

            public ScriptedEvaluator(params EvaluationResult[] results)
            {
                _results = new Queue<EvaluationResult>(results);
            }

            public EvaluationResult Evaluate(IReadOnlyClassDiagram diagram, IReadOnlyList<Requirement> activeRequirements)
            {
                return _results.Dequeue();
            }
        }

        private static EvaluationResult Result(int raw, int penalty)
        {
            return new EvaluationResult(raw, penalty, new List<string>(), new List<SmellOccurrence>());
        }

        private static List<Requirement> Requirements()
        {
            var list = new List<Requirement>();
            for (int i = 1; i <= 5; i++)
            {
                list.Add(new Requirement($"r{i}", $"Requisito {i}", new List<MethodData>()));
            }
            return list;
        }

        private static MatchSession NewSession(params EvaluationResult[] scripted)
        {
            return new MatchSession("A", "B", Requirements(), PlayerSlot.PlayerOne, new ScriptedEvaluator(scripted));
        }

        // Show -> Build -> Evaluate (a avaliação roda ao entrar em Evaluate)
        private static void PlayTurn(MatchSession session)
        {
            session.TurnManager.ForceAdvancePhase();
            session.TurnManager.ForceAdvancePhase();
        }

        // Evaluate -> Show do turno seguinte
        private static void NextTurn(MatchSession session)
        {
            session.TurnManager.ForceAdvancePhase();
        }

        [Test]
        public void Score_IsZero_BeforeFirstEvaluation()
        {
            var session = NewSession();

            Assert.AreEqual(0, session.PlayerOneScore);
            Assert.AreEqual(0, session.RoundPointsEarned);
        }

        [Test]
        public void Score_DoesNotDoubleCount_AccumulatedRawScore()
        {
            // RawScore já é acumulado: 1 requisito, depois 2, depois 3
            var session = NewSession(Result(100, 0), Result(200, 0), Result(300, 0));

            PlayTurn(session); NextTurn(session);
            PlayTurn(session); NextTurn(session);
            PlayTurn(session);

            Assert.AreEqual(300, session.PlayerOneScore); // e não 100+200+300
        }

        [Test]
        public void Score_SubtractsThePenaltyOfEachTurn()
        {
            var session = NewSession(Result(100, 0), Result(200, 50), Result(300, 50));

            PlayTurn(session); NextTurn(session);
            PlayTurn(session); NextTurn(session);
            PlayTurn(session);

            Assert.AreEqual(200, session.PlayerOneScore); // 300 - (0 + 50 + 50)
        }

        [Test]
        public void RoundPointsEarned_IsTheRawScoreDelta()
        {
            var session = NewSession(Result(100, 0), Result(200, 0));

            PlayTurn(session);
            Assert.AreEqual(100, session.RoundPointsEarned);

            NextTurn(session);
            PlayTurn(session);
            Assert.AreEqual(100, session.RoundPointsEarned); // 200 - 100, não 200
        }

        [Test]
        public void RoundPenaltyPoints_IsThePenaltyOfTheLastEvaluation()
        {
            var session = NewSession(Result(100, 30), Result(200, 10));

            PlayTurn(session); NextTurn(session);
            PlayTurn(session);

            Assert.AreEqual(10, session.RoundPenaltyPoints);
        }

        [Test]
        public void Evaluation_RunsOnlyForTheLocalPlayer()
        {
            var session = NewSession(Result(100, 0));

            PlayTurn(session);

            Assert.AreEqual(1, session.PlayerOneHistory.Count);
            Assert.AreEqual(0, session.PlayerTwoHistory.Count);
        }

        [Test]
        public void ReceiveOpponentResult_UpdatesOpponentHistory_AndRaisesScoreUpdated()
        {
            var session = NewSession();
            int scoreUpdatedCount = 0;
            session.OnScoreUpdated += () => scoreUpdatedCount++;

            session.ReceiveOpponentResult(1, Result(100, 20));

            Assert.AreEqual(1, session.PlayerTwoHistory.Count);
            Assert.AreEqual(1, scoreUpdatedCount);
            Assert.AreEqual(100, session.RoundOpponentPointsEarned);
            Assert.AreEqual(20, session.RoundOpponentPenaltyPoints);
        }

        [Test]
        public void ReceiveOpponentResult_RepeatedOrOutOfOrderTurn_IsIgnored()
        {
            var session = NewSession();

            Assert.IsTrue(session.ReceiveOpponentResult(1, Result(100, 20)));
            Assert.IsFalse(session.ReceiveOpponentResult(1, Result(100, 20)), "turno repetido deve ser ignorado");
            Assert.IsFalse(session.ReceiveOpponentResult(3, Result(300, 0)), "turno fora de ordem deve ser ignorado");
            Assert.IsFalse(session.ReceiveOpponentResult(0, Result(300, 0)), "turno invalido deve ser ignorado");

            Assert.AreEqual(1, session.PlayerTwoHistory.Count);
            Assert.AreEqual(80, session.PlayerTwoScore);   // 100 - 20, sem inflar a soma de penalidades
        }
    }
}