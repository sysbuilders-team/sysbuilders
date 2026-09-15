using Mirror;
using UnityEngine;

namespace Network
{
    public class GameRoomManager : NetworkRoomManager
    {
        public string ChosenTheme   { get; private set; }
        public string ChosenTrailId { get; private set; }
        
        public override void OnRoomServerPlayersReady()
        {
            // TODO: trocar por RequirementManager.Catalog.SelectTrail(theme, p1Id, p2Id) pós RequirementCatalog expor método que retorna Trail completa.
            ChosenTheme   = PlayerPrefs.GetString("SelectedTheme", "Biblioteca");
            ChosenTrailId = "PLACEHOLDER_TRAIL_ID";

            Debug.Log($"[GameRoomManager] Tema: {ChosenTheme} | Trail: {ChosenTrailId}");

            ServerChangeScene(GameplayScene);
        }
    }
}
