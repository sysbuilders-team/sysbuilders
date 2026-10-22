using System;
using Core.Evaluation;

namespace Network
{
    /// <summary>
    /// Bounds and shape validation for the payload of CmdSendEvaluationResult.
    /// Extracted from MatchSyncBridge so the rules can be tested without a
    /// NetworkBehaviour. The Cmd attribute stays on the bridge; only the
    /// decision logic lives here.
    ///
    /// Validates shape, not values. A client that stays within the bounds can
    /// still report a score it did not earn; server-side recompute is a
    /// separate concern.
    /// </summary>
    public static class EvaluationPayloadValidator
    {
        /// <summary>
        /// Per-turn raw score ceiling. Each revealed requirement awards 100,
        /// and at most one requirement is revealed per turn, so rawScore
        /// cannot legitimately exceed 100 * turnNumber.
        /// </summary>
        public const int MaxRawScorePerTurn = 100;

        /// <summary>
        /// Per-turn penalty ceiling. Penalties scale with diagram size and
        /// smell severity; observed maximum in a normal game is well under
        /// 2000. The cap keeps legitimate players from being rejected while
        /// bounding the arithmetic on the server side.
        /// </summary>
        public const int MaxPenaltyPerTurn = 5000;

        /// <summary>Lower/upper bounds for a smell severity. Real severities observed in a
        /// normal game are 1-5; 20 leaves headroom for future smells without accepting
        /// arbitrary ints.</summary>
        public const int MinSmellSeverity = 1;
        public const int MaxSmellSeverity = 20;

        /// <summary>Per-field cap for a smell's class list. A single class name is at most
        /// ClassNameRules.MaxLength (32) chars plus a unit separator; 512 leaves room for
        /// ~15 affected classes in one DuplicateCode smell before the cap bites.</summary>
        public const int MaxSmellClassFieldLength = 512;

        /// <summary>Validates the payload exactly as it arrives from the network.</summary>
        public static bool TryValidate(MatchResultPayload payload, out string reason)
        {
            return TryValidate(
                payload.TurnNumber,
                payload.RawScore,
                payload.PenaltyScore,
                payload.SmellTypes,
                payload.SmellSeverities,
                payload.SmellClasses,
                out reason);
        }

        public static bool TryValidate(
            int turnNumber,
            int rawScore,
            int penaltyScore,
            int[] smellTypes,
            int[] smellSeverities,
            string[] smellClasses,
            out string reason)
        {
            if (rawScore < 0 || (long)rawScore > (long)MaxRawScorePerTurn * turnNumber)
            {
                reason = $"raw {rawScore} out of range for turn {turnNumber}";
                return false;
            }

            if (penaltyScore < 0 || penaltyScore > MaxPenaltyPerTurn)
            {
                reason = $"penalty {penaltyScore} out of range";
                return false;
            }

            if (smellTypes == null || smellSeverities == null || smellClasses == null)
            {
                reason = "null smell arrays";
                return false;
            }

            if (smellTypes.Length != smellSeverities.Length ||
                smellTypes.Length != smellClasses.Length)
            {
                reason = $"array length mismatch ({smellTypes.Length}, {smellSeverities.Length}, {smellClasses.Length})";
                return false;
            }

            if (smellTypes.Length > SmellWireCodec.MaxSmellsPerResult)
            {
                reason = $"{smellTypes.Length} smells exceeds cap of {SmellWireCodec.MaxSmellsPerResult}";
                return false;
            }

            for (int i = 0; i < smellTypes.Length; i++)
            {
                if (!Enum.IsDefined(typeof(CodeSmellType), smellTypes[i]))
                {
                    reason = $"unknown smell type {smellTypes[i]}";
                    return false;
                }

                if (smellSeverities[i] < MinSmellSeverity || smellSeverities[i] > MaxSmellSeverity)
                {
                    reason = $"severity {smellSeverities[i]} out of range [{MinSmellSeverity}, {MaxSmellSeverity}]";
                    return false;
                }

                if (smellClasses[i] != null && smellClasses[i].Length > MaxSmellClassFieldLength)
                {
                    reason = $"smell class field {i} length {smellClasses[i].Length} exceeds cap {MaxSmellClassFieldLength}";
                    return false;
                }
            }

            reason = null;
            return true;
        }
    }
}
