using Network;
using NUnit.Framework;

namespace Tests.EditMode
{
    [TestFixture]
    public class EvaluationPayloadValidatorTests
    {
        private static int[] ValidTypes() => new int[] { 0 };
        private static int[] ValidSeverities() => new int[] { 1 };
        private static string[] ValidClasses() => new string[] { "A" };

        [Test]
        public void ValidPayload_Passes()
        {
            bool ok = EvaluationPayloadValidator.TryValidate(
                turnNumber: 1, rawScore: 100, penaltyScore: 50,
                smellTypes: ValidTypes(), smellSeverities: ValidSeverities(), smellClasses: ValidClasses(),
                out string reason);

            Assert.IsTrue(ok);
            Assert.IsNull(reason);
        }

        [Test]
        public void NegativeRawScore_Rejected()
        {
            bool ok = EvaluationPayloadValidator.TryValidate(
                1, -1, 0, ValidTypes(), ValidSeverities(), ValidClasses(), out string reason);

            Assert.IsFalse(ok);
            StringAssert.Contains("raw", reason);
        }

        [Test]
        public void RawScoreAboveTurnCap_Rejected()
        {
            // MaxRawScorePerTurn = 150: para o turno 2, o teto é 300; 301 deve ser rejeitado
            bool ok = EvaluationPayloadValidator.TryValidate(
                2, 301, 0, ValidTypes(), ValidSeverities(), ValidClasses(), out string reason);

            Assert.IsFalse(ok);
            StringAssert.Contains("raw 301 out of range for turn 2", reason);
        }

        [Test]
        public void RawScoreAtExactTurnCap_Passes()
        {
            // MaxRawScorePerTurn = 150: para o turno 3, o teto é 450; 450 deve ser aceito
            bool ok = EvaluationPayloadValidator.TryValidate(
                3, 450, 0, ValidTypes(), ValidSeverities(), ValidClasses(), out _);

            Assert.IsTrue(ok);
        }

        [Test]
        public void NegativePenalty_Rejected()
        {
            bool ok = EvaluationPayloadValidator.TryValidate(
                1, 0, -1, ValidTypes(), ValidSeverities(), ValidClasses(), out string reason);

            Assert.IsFalse(ok);
            StringAssert.Contains("penalty", reason);
        }

        [Test]
        public void PenaltyAboveCap_Rejected()
        {
            bool ok = EvaluationPayloadValidator.TryValidate(
                1, 0, 5001, ValidTypes(), ValidSeverities(), ValidClasses(), out string reason);

            Assert.IsFalse(ok);
            StringAssert.Contains("penalty 5001 out of range", reason);
        }

        [Test]
        public void PenaltyAtExactCap_Passes()
        {
            bool ok = EvaluationPayloadValidator.TryValidate(
                1, 0, 5000, ValidTypes(), ValidSeverities(), ValidClasses(), out _);

            Assert.IsTrue(ok);
        }

        [Test]
        public void NullTypes_Rejected()
        {
            bool ok = EvaluationPayloadValidator.TryValidate(
                1, 0, 0, null, ValidSeverities(), ValidClasses(), out string reason);

            Assert.IsFalse(ok);
            StringAssert.Contains("null smell arrays", reason);
        }

        [Test]
        public void MismatchedArrayLengths_Rejected()
        {
            bool ok = EvaluationPayloadValidator.TryValidate(
                1, 0, 0,
                smellTypes: new int[] { 0, 0 },
                smellSeverities: new int[] { 1 },
                smellClasses: new string[] { "A" },
                out string reason);

            Assert.IsFalse(ok);
            StringAssert.Contains("array length mismatch", reason);
        }

        [Test]
        public void SmellCountAboveCap_Rejected()
        {
            int cap = SmellWireCodec.MaxSmellsPerResult;
            var oversized = new int[cap + 1];
            var sev = new int[cap + 1];
            var cls = new string[cap + 1];

            bool ok = EvaluationPayloadValidator.TryValidate(
                1, 0, 0, oversized, sev, cls, out string reason);

            Assert.IsFalse(ok);
            StringAssert.Contains("exceeds cap", reason);
        }

        [Test]
        public void SmellCountAtExactCap_Passes()
        {
            int cap = SmellWireCodec.MaxSmellsPerResult;
            var types = new int[cap];
            var sev = new int[cap];
            var cls = new string[cap];

            // Severity 0 is below MinSmellSeverity, so fill with the minimum valid value.
            for (int i = 0; i < cap; i++) sev[i] = EvaluationPayloadValidator.MinSmellSeverity;

            bool ok = EvaluationPayloadValidator.TryValidate(
                1, 0, 0, types, sev, cls, out _);

            Assert.IsTrue(ok);
        }

        [Test]
        public void PayloadOverload_UsesTheSameRulesAsTheFieldSignature()
        {
            var payload = new MatchResultPayload
            {
                TurnNumber = 1,
                RawScore = 100,
                PenaltyScore = 50,
                SmellTypes = ValidTypes(),
                SmellSeverities = ValidSeverities(),
                SmellClasses = ValidClasses()
            };

            Assert.IsTrue(EvaluationPayloadValidator.TryValidate(payload, out _));

            payload.RawScore = -1;
            Assert.IsFalse(EvaluationPayloadValidator.TryValidate(payload, out string reason));
            StringAssert.Contains("raw", reason);
        }

        [Test]
        public void UnknownSmellType_Rejected()
        {
            bool ok = EvaluationPayloadValidator.TryValidate(
                1, 0, 0,
                smellTypes: new int[] { 99 },
                smellSeverities: new int[] { 1 },
                smellClasses: new string[] { "A" },
                out string reason);
            Assert.IsFalse(ok);
            StringAssert.Contains("unknown smell type", reason);
        }

        [Test]
        public void SeverityBelowMin_Rejected()
        {
            bool ok = EvaluationPayloadValidator.TryValidate(
                1, 0, 0, ValidTypes(), new int[] { 0 }, ValidClasses(), out string reason);
            Assert.IsFalse(ok);
            StringAssert.Contains("severity", reason);
        }

        [Test]
        public void SeverityAboveMax_Rejected()
        {
            bool ok = EvaluationPayloadValidator.TryValidate(
                1, 0, 0, ValidTypes(), new int[] { 21 }, ValidClasses(), out string reason);
            Assert.IsFalse(ok);
            StringAssert.Contains("severity", reason);
        }

        [Test]
        public void OversizedClassField_Rejected()
        {
            bool ok = EvaluationPayloadValidator.TryValidate(
                1, 0, 0, ValidTypes(), ValidSeverities(),
                new string[] { new string('a', EvaluationPayloadValidator.MaxSmellClassFieldLength + 1) },
                out string reason);
            Assert.IsFalse(ok);
            StringAssert.Contains("smell class field", reason);
        }

        [Test]
        public void Encode_ClampsToMaxSmellsPerResult()
        {
            var smells = new System.Collections.Generic.List<Core.Evaluation.SmellOccurrence>();
            for (int i = 0; i < SmellWireCodec.MaxSmellsPerResult + 10; i++)
            {
                smells.Add(new Core.Evaluation.SmellOccurrence(
                    Core.Evaluation.CodeSmellType.LargeClass,
                    new System.Collections.Generic.List<string> { "A" + i }));
            }

            SmellWireCodec.Encode(smells, out int[] types, out _, out _);
            Assert.AreEqual(SmellWireCodec.MaxSmellsPerResult, types.Length);
        }

        [Test]
        public void Decode_RejectsUnknownSmellType()
        {
            int[] types = new int[] { 99 };
            int[] severities = new int[] { 1 };
            string[] classes = new string[] { "A" };

            var decoded = SmellWireCodec.Decode(types, severities, classes);
            Assert.AreEqual(0, decoded.Count);
        }

        [Test]
        public void Decode_SanitizesInjectedClassNames()
        {
            int[] types = new int[] { (int)Core.Evaluation.CodeSmellType.LargeClass };
            int[] severities = new int[] { 1 };
            string[] classes = new string[] { "<b>huge</b>#Injected" };

            var decoded = SmellWireCodec.Decode(types, severities, classes);

            Assert.AreEqual(1, decoded.Count);
            StringAssert.DoesNotContain("<", decoded[0].AffectedClassNames[0]);
            StringAssert.DoesNotContain(">", decoded[0].AffectedClassNames[0]);
        }
    }
}
