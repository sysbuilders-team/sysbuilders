using Mirror;
using Mirror.Transports.Encryption;
using UnityEngine;

namespace Network
{
    public class GameRoomPlayer : NetworkRoomPlayer
    {
        [SyncVar] public string playerName;
        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            string localname = PlayerPrefs.GetString("PlayerName", "Jogador");
            CmdSetName(localname);
        }
        
        [Command]
        private void CmdSetName(string playName)
        {
            playerName = playName;
        }
    }
}