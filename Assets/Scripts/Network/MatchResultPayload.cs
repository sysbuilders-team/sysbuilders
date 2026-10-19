using System;

namespace Network
{
    /// <summary>
    /// Everything CmdSendEvaluationResult carries for one turn, as a single value.
    ///
    /// The command used to take eight parameters, three of them parallel arrays. Grouping
    /// them keeps the signature stable as the payload grows and lets the validator read
    /// the same shape that crosses the wire.
    ///
    /// Mirror weaves this struct into the command and RPC serialization, so every field
    /// must stay a supported type (primitives and arrays of primitives).
    /// </summary>
    [Serializable]
    public struct MatchResultPayload
    {
        public int TurnNumber;
        public int Phase;
        public int SlotIndex;
        public int RawScore;
        public int PenaltyScore;
        public int[] SmellTypes;
        public int[] SmellSeverities;
        public string[] SmellClasses;
    }
}
