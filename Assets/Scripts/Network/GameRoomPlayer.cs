using System;
using Mirror;
using UnityEngine;

namespace Network
{
	public class GameRoomPlayer : NetworkRoomPlayer
	{
		[SyncVar(hook = nameof(OnPlayerNameChanged))] public string playerName;

		public static event Action<GameRoomPlayer, string> OnRoomPlayerNameChanged;

		public override void OnStartLocalPlayer()
		{
			base.OnStartLocalPlayer();
			string localname = PlayerPrefs.GetString("PlayerName", "Jogador");
			CmdSetName(localname);
		}

		private void OnPlayerNameChanged(string oldName, string newName)
		{
			OnRoomPlayerNameChanged?.Invoke(this, newName);
		}

		[Command]
		private void CmdSetName(string rawName)
		{
			playerName = Core.Discovery.RoomAdvertisement.SanitizeShortText(rawName, "Jogador");
		}
	}
}
