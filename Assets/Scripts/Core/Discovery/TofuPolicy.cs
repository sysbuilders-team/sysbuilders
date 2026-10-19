using System;

namespace Core.Discovery
{
    public enum TofuDecision
    {
        /// <summary>Nunca vimos este host: aceita e passa a confiar (trust on first use).</summary>
        Unknown = 0,
        Match = 1,
        Mismatch = 2,
        NoFingerprint = 3
    }

    /// <summary>
    /// Trust on first use for the host public key. Closes the trust hole of the room list:
    /// the client accepts the key on the first connection and then refuses (or shouts) if it
    /// changes later. It is not PKI and does not protect against the first contact.
    /// </summary>
    public static class TofuPolicy
    {
        public static TofuDecision Evaluate(string storedFingerprint, string presentedFingerprint)
        {
            if (string.IsNullOrWhiteSpace(presentedFingerprint)) return TofuDecision.NoFingerprint;
            if (string.IsNullOrWhiteSpace(storedFingerprint)) return TofuDecision.Unknown;

            return string.Equals(storedFingerprint.Trim(), presentedFingerprint.Trim(), StringComparison.OrdinalIgnoreCase)
                ? TofuDecision.Match
                : TofuDecision.Mismatch;
        }

        /// <summary>
        /// Decides whether the connection may proceed. A host that presents no fingerprint is
        /// rejected unless explicitly allowed: "no fingerprint" must never be silently equivalent
        /// to "trusted".
        /// </summary>
        public static bool ShouldAccept(TofuDecision decision, bool rejectOnMismatch, bool acceptNoFingerprint = false)
        {
            switch (decision)
            {
                case TofuDecision.Mismatch:
                    return !rejectOnMismatch;
                case TofuDecision.NoFingerprint:
                    return acceptNoFingerprint;
                default:
                    return true;
            }
        }

        /// <summary>Only stores when the decision is trustworthy (first time or matching what we had).</summary>
        public static bool ShouldStore(TofuDecision decision)
        {
            return decision == TofuDecision.Unknown || decision == TofuDecision.Match;
        }

        public static string Describe(TofuDecision decision, string host)
        {
            switch (decision)
            {
                case TofuDecision.Unknown:
                    return $"Primeira conexao com '{host}': chave registrada (TOFU).";
                case TofuDecision.Match:
                    return $"Chave de '{host}' confere com a registrada.";
                case TofuDecision.Mismatch:
                    return $"ATENCAO: a chave de '{host}' MUDOU desde a ultima partida. Possivel host diferente ou interceptacao.";
                default:
                    return $"Host '{host}' nao apresentou fingerprint; sem verificacao TOFU.";
            }
        }
    }
}
