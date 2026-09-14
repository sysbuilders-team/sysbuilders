using Mirror;
using UnityEngine.Serialization;

namespace Network
{
    // Representa o jogador ENQUANT ainda está na sala de espera (antes da partida começar) não confundir com PlayerState do Core.
    public class GameRoomPlayer : NetworkRoomPlayer
    {
        [FormerlySerializedAs("PlayerName")] [SyncVar] public string playerName;

        // TODO: expor estado de "pronto" pro GameRoomManager consultar
    }
}