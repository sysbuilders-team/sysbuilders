using System;
using System.Collections.Generic;
using System.Net;

namespace Core.Discovery
{
    /// <summary>Where a discovered room came from.</summary>
    public enum DiscoveryTier
    {
        /// <summary>Standard mDNS advertisement (_sysbuilders-v1._udp.local). The only protocol in use.</summary>
        Mdns = 0,

        /// <summary>Reserved: room added manually by IP (never shows up in the discovery list).</summary>
        ManualIp = 1
    }

    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new SystemClock();
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>Room seen on the network: the advertisement plus where it came from.</summary>
    public sealed class DiscoveredRoom
    {
        public RoomAdvertisement Room { get; }
        public IPAddress Address { get; }
        public DiscoveryTier Tier { get; }
        public DateTime LastSeenUtc { get; }

        public DiscoveredRoom(RoomAdvertisement room, IPAddress address, DiscoveryTier tier, DateTime lastSeenUtc)
        {
            Room = room ?? throw new ArgumentNullException(nameof(room));
            Address = address;
            Tier = tier;
            LastSeenUtc = lastSeenUtc;
        }

        public string SessionId => Room.SessionId;
        public int Port => Room.Port;

        public DiscoveredRoom SeenAgain(DateTime utcNow) => new DiscoveredRoom(Room, Address, Tier, utcNow);

        public override string ToString() => $"{Room} via {Tier} em {Address}";
    }

    /// <summary>Side that publishes the room (host).</summary>
    public interface IRoomAdvertiser : IDisposable
    {
        bool IsAdvertising { get; }
        void StartAdvertising(RoomAdvertisement advertisement, DateTime utcNow);
        void UpdateAdvertisement(RoomAdvertisement advertisement, DateTime utcNow);
        void StopAdvertising();

        /// <summary>Handles one received datagram (a client query, for example).</summary>
        void HandleDatagram(MulticastDatagram datagram, DateTime utcNow);

        /// <summary>Called periodically (by the Unity layer, in Update) to re-announce.</summary>
        void Tick(DateTime utcNow);
    }

    /// <summary>Side that looks for rooms (client).</summary>
    public interface IRoomBrowser : IDisposable
    {
        bool IsBrowsing { get; }
        IReadOnlyCollection<DiscoveredRoom> Rooms { get; }

        void StartBrowsing(DateTime utcNow);
        void StopBrowsing();

        /// <summary>Immediate query (the "Buscar Partidas" button) that keeps the current list.</summary>
        void Refresh(DateTime utcNow);

        void HandleDatagram(MulticastDatagram datagram, DateTime utcNow);
        void Tick(DateTime utcNow);

        event Action<DiscoveredRoom> OnRoomFound;
        event Action<DiscoveredRoom> OnRoomUpdated;
        event Action<string> OnRoomLost;

        /// <summary>Diagnostic messages (incompatible version, invalid packet). Never throws.</summary>
        event Action<string> OnDiagnostic;
    }
}
