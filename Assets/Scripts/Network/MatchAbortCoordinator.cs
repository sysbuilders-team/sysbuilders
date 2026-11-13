namespace Network
{
    /// <summary>
    /// One-shot abort state for a running match, plus the wire bound for the abort reason.
    ///
    /// Pure C# on purpose. MatchSyncBridge keeps the Unity side (returning to the menu,
    /// notifying clients); this class keeps the part that must behave the same on client
    /// and server, which is exactly the part worth testing.
    /// </summary>
    public sealed class MatchAbortCoordinator
    {
        /// <summary>Upper bound for a reason string sent over the network.</summary>
        public const int MaxReasonLength = 120;

        private bool _aborted;

        public bool HasAborted => _aborted;

        /// <summary>Marks the match aborted. Returns false when it had already been aborted.</summary>
        public bool TryBeginAbort()
        {
            if (_aborted) return false;
            _aborted = true;
            return true;
        }

        /// <summary>Re-arms the coordinator when a new match starts.</summary>
        public void Reset() => _aborted = false;

        /// <summary>Bounds a reason that crosses the wire. Null becomes empty.</summary>
        public static string SanitizeReason(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return string.Empty;
            return reason.Length <= MaxReasonLength ? reason : reason.Substring(0, MaxReasonLength) + "...";
        }
    }
}
