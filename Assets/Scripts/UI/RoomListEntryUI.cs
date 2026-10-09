using System;
using Core.Discovery;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class RoomListEntryUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _roomNameText;
        [SerializeField] private TMP_Text _detailsText;
        [SerializeField] private Button _joinButton;

        private TMP_Text _singleLineLabel;
        private DiscoveredRoom _room;

        public DiscoveredRoom Room => _room;
        public event Action<DiscoveredRoom> OnClicked;

        /// <summary>Usado quando a linha é criada por código (sem prefab): um rótulo único.</summary>
        public void Bind(TMP_Text singleLineLabel, Button joinButton)
        {
            _singleLineLabel = singleLineLabel;
            _joinButton = joinButton;

            if (_joinButton != null)
            {
                _joinButton.onClick.RemoveListener(HandleClick);
                _joinButton.onClick.AddListener(HandleClick);
            }
        }

        private void Awake()
        {
            if (_joinButton == null) _joinButton = GetComponent<Button>();

            if (_joinButton != null)
            {
                _joinButton.onClick.RemoveListener(HandleClick);
                _joinButton.onClick.AddListener(HandleClick);
            }
        }

        private void OnDestroy()
        {
            if (_joinButton != null) _joinButton.onClick.RemoveListener(HandleClick);
        }

        public void Setup(DiscoveredRoom room)
        {
            _room = room;
            if (room == null) return;

            bool full = room.Room.PlayerCount >= room.Room.MaxPlayers;
            string details = BuildDetails(room);

            if (_roomNameText != null) _roomNameText.text = room.Room.RoomName;
            if (_detailsText != null) _detailsText.text = details;
            if (_singleLineLabel != null) _singleLineLabel.text = $"{room.Room.RoomName} — {details}";

            // Sala cheia fica em âmbar e sem poder entrar; vaga fica branca.
            var color = full ? new Color(1f, 0.72f, 0.4f, 1f) : Color.white;
            if (_roomNameText != null) _roomNameText.color = color;
            if (_detailsText != null) _detailsText.color = color;
            if (_singleLineLabel != null) _singleLineLabel.color = color;

            if (_joinButton != null) _joinButton.interactable = !full;
        }

        private static string BuildDetails(DiscoveredRoom room)
        {
            string address = room.Address != null ? room.Address.ToString() : "?";
            string fingerprint = string.IsNullOrEmpty(room.Room.PublicKeyFingerprint)
                ? "não anunciada"
                : Shorten(room.Room.PublicKeyFingerprint);

            bool full = room.Room.PlayerCount >= room.Room.MaxPlayers;
            string occupancy = full ? "CHEIA" : "vaga";

            return $"{room.Room.HostName}  |  {room.Room.PlayerCount}/{room.Room.MaxPlayers} {occupancy}  |  " +
                   $"tema: {room.Room.Theme}  |  {address}:{room.Port}  |  via {room.Tier}  |  chave: {fingerprint}";
        }

        private void HandleClick()
        {
            if (_room != null) OnClicked?.Invoke(_room);
        }

        private static string Shorten(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= 12 ? value : value.Substring(0, 12) + "…";
        }
    }
}
