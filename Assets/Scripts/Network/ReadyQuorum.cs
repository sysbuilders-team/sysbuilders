using System;
using System.Collections.Generic;
using Core.Session;

namespace Network
{
		/// <summary>
		/// Tracks which players have marked ready and evaluated for the current
		/// phase. Used by MatchSyncBridge to decide when to advance.
		///
		/// Extracted from MatchSyncBridge so the quorum logic can be tested without
		/// a NetworkBehaviour or a live connection. Pure state, no Unity or Mirror
		/// dependency.
		/// </summary>
		public sealed class ReadyQuorum
		{
			private readonly HashSet<PlayerSlot> _ready = new HashSet<PlayerSlot>();
			private readonly HashSet<PlayerSlot> _evaluated = new HashSet<PlayerSlot>();
			private readonly int _requiredPlayers;

			public ReadyQuorum(int requiredPlayers)
			{
				if (requiredPlayers < 1)
				{
					throw new ArgumentOutOfRangeException(nameof(requiredPlayers), requiredPlayers, "At least one player is required.");
				}
				_requiredPlayers = requiredPlayers;
			}

			public int RequiredPlayers => _requiredPlayers;

			public int ReadyCount => _ready.Count;
			public int EvaluatedCount => _evaluated.Count;

			public bool IsReadyQuorumMet => _ready.Count >= _requiredPlayers;
			public bool IsEvaluationQuorumMet => _evaluated.Count >= _requiredPlayers;

			public void MarkReady(PlayerSlot slot) => _ready.Add(slot);
			public void MarkEvaluated(PlayerSlot slot) => _evaluated.Add(slot);

			public void ClearReady() => _ready.Clear();
			public void ClearEvaluated() => _evaluated.Clear();

			public void ClearAll()
			{
				_ready.Clear();
				_evaluated.Clear();
			}
		}
}
