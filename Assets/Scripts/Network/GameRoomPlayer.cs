using Mirror;
using UnityEngine;

namespace Network
{
    public class GameRoomPlayer : NetworkRoomPlayer
    {
        [SyncVar] public string PlayerName;
        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            string name = PlayerPrefs.GetString("PlayerName", "Jogador");
            CmdSetName(name);
        }
        
        [Command]
        private void CmdSetName(string playerName)
        {
            PlayerName = playerName;
        }
    }
}