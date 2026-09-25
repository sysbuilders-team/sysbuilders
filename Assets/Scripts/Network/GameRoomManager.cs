using Mirror;
using Mirror.Transports.Encryption;
using UnityEngine;

namespace Network
{
    public class GameRoomManager : NetworkRoomManager
    {
        public string ChosenTheme   { get; private set; }
        public string ChosenTrailId { get; private set; }

        public override void Start()
        {
            base.Start();
            SetupEncryption();
        }

        private void SetupEncryption()
        {
            var activeTransport = Transport.active;
            if (activeTransport is EncryptionTransport encryptionTransport)
            {
                encryptionTransport.ClientValidateServerPubKey = EncryptionTransport.ValidationMode.Callback;
                encryptionTransport.OnClientValidateServerPubKey = pubKeyInfo =>
                {
                    Debug.Log($"[GameRoomManager] Chave pública do servidor aceita (Fingerprint: {pubKeyInfo.Fingerprint})");
                    return true;
                };

                Debug.Log("[GameRoomManager] Criptografia AES256-GCM ativada via EncryptionTransport.");
            }
            else
            {
                Debug.LogWarning("[GameRoomManager] O transporte ativo não é EncryptionTransport. A conexão trafegará sem criptografia.");
            }
        }

        public override void OnRoomServerPlayersReady()
        {
            ChosenTheme = PlayerPrefs.GetString("SelectedTheme", "Biblioteca");
            Debug.Log($"[GameRoomManager] Tema da sala: {ChosenTheme}");

            ServerChangeScene(GameplayScene);
        }
    }
}

