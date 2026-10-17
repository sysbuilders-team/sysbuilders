using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Core.Discovery;
using Core.Discovery.Mdns;
using UnityEngine;

namespace Network.Discovery
{
	/// <summary>
	/// Discovery channel = one multicast socket + a pair (responder, browser) from Core.
	/// The socket receives on a background thread and only ENQUEUES: all processing happens on the
	/// main thread (Update), because the browsers state and the Unity API are not thread-safe.
	/// </summary>
	internal sealed class DiscoveryChannel : IDisposable
	{
		private const int MaxPendingDatagrams = 256;
		private readonly MulticastSocket _socket;

		public DiscoveryChannel(string name, IPAddress group, int port, IPAddress localAddress)
		{
			Name = name;
			Group = group;
			Port = port;

			_socket = new MulticastSocket();

			try
			{
				_socket.Bind(port);
				_socket.JoinGroup(group, localAddress);
				_socket.SetOutgoingInterface(localAddress);
				_socket.TimeToLive = 1;

				// Loopback on purpose: it allows testing two instances on the SAME machine
				// (Editor + build). The self-announcement it causes is filtered by SessionId in the service.
				_socket.MulticastLoopback = true;

				Browser = new MdnsBrowser(_socket, group, port);
				Advertiser = new MdnsResponder(_socket, group, port, localAddress);

				_socket.StartReceiving(
		 datagram =>
					 {
						 // Under flood (or a stalled main thread) the queue
						 // would otherwise grow without bound. Drop the
						 // oldest so a later drain sees fresh packets rather
						 // than a historical backlog.
						 if (PendingDatagrams.Count >= MaxPendingDatagrams)
						 {
							 PendingDatagrams.TryDequeue(out _);
						 }
						 PendingDatagrams.Enqueue(datagram);
					 },
		   error => PendingErrors.Enqueue(error));
      }
				catch
			{
				//if any step of the setup fails the socket must not leak.
				//Dispose here and rethrow so TryCreateChannel can report it.
				_socket.Dispose();
				throw;
			}
		}

		public string LastSocketError { get; set; }
		public string Name { get; }
		public IPAddress Group { get; }
		public int Port { get; }
		public IRoomBrowser Browser { get; }
		public IRoomAdvertiser Advertiser { get; }
		public ConcurrentQueue<MulticastDatagram> PendingDatagrams { get; } = new ConcurrentQueue<MulticastDatagram>();
		public ConcurrentQueue<Exception> PendingErrors { get; } = new ConcurrentQueue<Exception>();

		public void Dispose()
		{
			try
			{
				Browser.Dispose();
			}
			catch (Exception)
			{
				// shutdown must never break the scene
			}

			try
			{
				Advertiser.Dispose();
			}
			catch (Exception)
			{
			}

			_socket.Dispose();
		}
	}

	/// <summary>
	/// Room discovery facade for the UI (implements the Core IRoomDirectory port).
	///
	/// A single protocol: standard mDNS on 224.0.0.251:5353 (RFC 6762/6763), advertising
	/// _sysbuilders-v1._udp.local. Two design decisions recorded here:
	///  - there is NO broadcast: the game sends nothing to 255.255.255.255 (noisy traffic, and
	///    subject to being blocked on academic/corporate networks);
	///  - the "custom multicast with a binary payload" alternative was dropped: standard mDNS
	///    already covers the case and is the traffic most accepted by firewalls/APs (it looks like
	///    Avahi), so keeping two protocols would be dead code. If 5353 is filtered, a direct IP connection remains.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("SysBuilders/Network/Room Discovery Service")]
	public sealed class RoomDiscoveryService : MonoBehaviour, IRoomDirectory
	{
		[Header("mDNS")]
		[SerializeField] private string _mdnsGroupAddress = "224.0.0.251";
		[SerializeField] private int _mdnsPort = 5353;

		[Header("Rede")]
		[Tooltip("Deixe vazio para escolher a interface principal (com gateway) automaticamente.")]
		[SerializeField] private string _preferredInterfaceAddress = string.Empty;

		[Tooltip("Loga cada erro/diagnostico de descoberta no console.")]
		[SerializeField] private bool _logDiagnostics = true;

		private readonly List<DiscoveryChannel> _channels = new List<DiscoveryChannel>();
		private readonly Dictionary<string, DiscoveredRoom> _rooms = new Dictionary<string, DiscoveredRoom>(StringComparer.Ordinal);

		private bool _channelsReady;
		private bool _browsing;
		private RoomAdvertisement _advertisement;

		private static readonly TimeSpan ChannelRetryInterval = TimeSpan.FromSeconds(5);
		private DateTime _nextChannelRetryUtc;
		private const int MaxDatagramsPerFrame = 64;
		private bool _roomsDirty;

		public IReadOnlyCollection<DiscoveredRoom> Rooms => _rooms.Values.ToList();
		public bool IsBrowsing => _browsing;
		public bool IsAdvertising => _advertisement != null;
		public RoomAdvertisement CurrentAdvertisement => _advertisement;

		/// <summary>Chosen interface (to show in the menu and to blame something when nothing shows up).</summary>
		public string ActiveInterface { get; private set; } = "nenhuma";

		public event Action OnRoomsChanged;
		public event Action<string> OnDiagnostic;

		// advertise

		public void StartAdvertising(RoomAdvertisement advertisement)
		{
			if (advertisement == null) throw new ArgumentNullException(nameof(advertisement));

			EnsureChannels();
			_advertisement = advertisement;
			DateTime now = DateTime.UtcNow;

			foreach (var channel in _channels)
			{
				try
				{
					channel.Advertiser.StartAdvertising(advertisement, now);
				}
				catch (Exception ex)
				{
					Diag($"[{channel.Name}] falha ao anunciar a sala: {ex.Message}");
				}
			}
		}

		public void UpdateAdvertisement(RoomAdvertisement advertisement)
		{
			if (advertisement == null) return;

			if (!IsAdvertising)
			{
				StartAdvertising(advertisement);
				return;
			}

			_advertisement = advertisement;
			DateTime now = DateTime.UtcNow;

			foreach (var channel in _channels)
			{
				try
				{
					channel.Advertiser.UpdateAdvertisement(advertisement, now);
				}
				catch (Exception ex)
				{
					Diag($"[{channel.Name}] falha ao atualizar o anuncio: {ex.Message}");
				}
			}
		}

		public void StopAdvertising()
		{
			foreach (var channel in _channels)
			{
				try
				{
					channel.Advertiser.StopAdvertising();
				}
				catch (Exception)
				{
					// o goodbye e best-effort
				}
			}

			_advertisement = null;
		}

		// browse

		public void StartBrowsing()
		{
			EnsureChannels();

			if (_browsing)
			{
				Refresh();
				return;
			}

			_browsing = true;
			DateTime now = DateTime.UtcNow;

			foreach (var channel in _channels)
			{
				try
				{
					channel.Browser.StartBrowsing(now);
				}
				catch (Exception ex)
				{
					Diag($"[{channel.Name}] falha ao iniciar a busca: {ex.Message}");
				}
			}
		}

		public void Refresh()
		{
			EnsureChannels();
			_browsing = true;
			DateTime now = DateTime.UtcNow;

			foreach (var channel in _channels)
			{
				try
				{
					channel.Browser.Refresh(now);
				}
				catch (Exception ex)
				{
					Diag($"[{channel.Name}] falha ao consultar salas: {ex.Message}");
				}
			}
		}

		public void StopBrowsing()
		{
			_browsing = false;

			foreach (var channel in _channels)
			{
				channel.Browser.StopBrowsing();
			}

			_rooms.Clear();
			OnRoomsChanged?.Invoke();
		}

		// life cycle

		private void Update()
		{
			if (!_channelsReady)
			{
				// Automatic retry: a failed setup (busy port, rejected interface) used to leave
				// discovery disabled for the whole session. If the user asked to advertise or browse,
				// keep trying every few seconds until a channel is created.
				if (_browsing || IsAdvertising)
				{
					DateTime retryNow = DateTime.UtcNow;
					if (retryNow >= _nextChannelRetryUtc)
					{
						_nextChannelRetryUtc = retryNow + ChannelRetryInterval;
						EnsureChannels();
						if (_channelsReady) ReplayPendingStart();
					}
				}

				if (!_channelsReady) return;
			}

			// When neither advertising nor browsing is active there is no reason
			// to tick the responder or browser, and no reason to grow the pending
			// queue. Drain and discard so a later StartBrowsing begins from a
			// clean slate instead of replaying the idle-period backlog.
			if (!_browsing && !IsAdvertising)
			{
				foreach (var channel in _channels)
				{
					while (channel.PendingDatagrams.TryDequeue(out _)) { }
					while (channel.PendingErrors.TryDequeue(out _)) { }
				}
				_roomsDirty = false;
				return;
			}


			DateTime now = DateTime.UtcNow;

			foreach (var channel in _channels)
			{
				try
				{
					// Datagrams arrive from a background thread: processed here, on the main thread.
					// Cap the drain so a flood cannot stall the frame; the rest stays queued
					// (with drop-oldest in the receive callback) for the next Update.
					int processed = 0;
					while (processed < MaxDatagramsPerFrame && channel.PendingDatagrams.TryDequeue(out MulticastDatagram datagram))
					{
						processed++;
						// A single malformed packet must never kill the discovery
						// loop. TryParseRoom now validates aggressively, but
						// "should reject" is not "does reject" for data arriving
						// from the network — contain the blast radius per datagram.
						try
						{
							channel.Advertiser.HandleDatagram(datagram, now);
							channel.Browser.HandleDatagram(datagram, now);
						}
						catch (Exception ex)
						{
							Diag($"[{channel.Name}] datagram rejected: {ex.GetType().Name}: {ex.Message}");
						}
					}

					while (channel.PendingErrors.TryDequeue(out Exception error))
					{
						Diag($"[{channel.Name}] erro de socket: {error.Message}");
					}

					channel.Advertiser.Tick(now);
					channel.Browser.Tick(now);

					channel.LastSocketError = null;   // recovery
				}
				catch (System.Net.Sockets.SocketException ex)
				{
					if (channel.LastSocketError == null)
					{
						channel.LastSocketError = ex.Message;
						Diag($"[{channel.Name}] rede indisponível: {ex.Message}");
					}
				}
				catch (System.ObjectDisposedException)
				{
					// Closed channel during update 
				}
			}
			if (_roomsDirty)
			{
				_roomsDirty = false;
				MergeRooms();
			}
		}

		private void OnApplicationQuit() => Shutdown();

		private void OnDestroy() => Shutdown();

		private void Shutdown()
		{
			if (!_channelsReady) return;

			StopAdvertising();
			_browsing = false;

			foreach (var channel in _channels)
			{
				channel.Dispose();
			}

			_channels.Clear();
			_rooms.Clear();
			_channelsReady = false;
		}

		// intetrn

		private void EnsureChannels()
		{
			if (_channelsReady) return;

			var candidates = NetworkInterfaceSelector.Enumerate();
			NetworkInterfaceSelector.TryFindByAddress(candidates, ParseAddress(_preferredInterfaceAddress), out var forced);

			InterfaceCandidate selected = forced;
			string reason;

			if (selected != null)
			{
				reason = "override do Inspector";
			}
			else if (!NetworkInterfaceSelector.TrySelectPrimary(candidates, out selected, out reason))
			{
				Diag($"Descoberta desabilitada: {reason}. Mostrando apenas conexao por IP.");
				return;
			}

			ActiveInterface = $"{selected.Name} ({selected.Address})";
			Diag($"Interface de descoberta: {ActiveInterface} [{reason}]");

			TryCreateChannel("mDNS", _mdnsGroupAddress, _mdnsPort, selected.Address);

			if (_channels.Count > 0)
			{
				_channelsReady = true;
			}
			else
			{
				Diag("Descoberta indisponivel: verifique a porta 5353 e o firewall/perfil de rede. " +
						"A conexao por IP direto continua funcionando. Nova tentativa em alguns segundos.");
			}
		}

		/// <summary>
		/// Called once the channels finally exist, to apply advertising and browsing that were
		/// requested while setup was still failing. Without this, StartAdvertising and StartBrowsing
		/// set their state but no packet is sent until the caller tries again.
		/// </summary>
		private void ReplayPendingStart()
		{
			DateTime now = DateTime.UtcNow;

			if (IsAdvertising)
			{
				foreach (var channel in _channels)
				{
					try { channel.Advertiser.StartAdvertising(_advertisement, now); }
					catch (Exception ex) { Diag($"[{channel.Name}] falha ao reaplicar o anuncio: {ex.Message}"); }
				}
			}

			if (_browsing)
			{
				foreach (var channel in _channels)
				{
					try { channel.Browser.StartBrowsing(now); }
					catch (Exception ex) { Diag($"[{channel.Name}] falha ao reaplicar a busca: {ex.Message}"); }
				}
			}
		}

		private void TryCreateChannel(string name, string groupAddress, int port, IPAddress localAddress)
		{
			if (!IPAddress.TryParse(groupAddress, out var group))
			{
				Diag($"[{name}] endereco de grupo invalido: '{groupAddress}'.");
				return;
			}

			try
			{
				var channel = new DiscoveryChannel(name, group, port, localAddress);
				// Browser events fire on the main thread (inside Tick or
				// HandleDatagram, both driven from Update). Use them as the
				// signal to run MergeRooms next frame.
				channel.Browser.OnRoomFound += _ => _roomsDirty = true;
				channel.Browser.OnRoomUpdated += _ => _roomsDirty = true;
				channel.Browser.OnRoomLost += _ => _roomsDirty = true;

				_channels.Add(channel);
				Diag($"[{name}] ativo em {group}:{port}.");
			}
			catch (Exception ex)
			{
				// Port busy without SO_REUSEADDR, firewall or a platform without multicast: the channel
				// fails, but a direct IP connection always works.
				Diag($"[{name}] indisponivel ({ex.GetType().Name}: {ex.Message}).");
			}
		}

		/// <summary>Merges the channel lists: dedupe by SessionId and priority to mDNS.</summary>
		private void MergeRooms()
		{
			bool changed = false;
			var live = new HashSet<string>(StringComparer.Ordinal);

			foreach (var channel in _channels)
			{
				foreach (var room in channel.Browser.Rooms)
				{
					// Multicast loopback: the room advertised by the host itself does not enter the list.
					if (_advertisement != null &&
							string.Equals(room.SessionId, _advertisement.SessionId, StringComparison.Ordinal)) continue;

					live.Add(room.SessionId);

					if (_rooms.TryGetValue(room.SessionId, out var existing))
					{
						if (existing.Room.HasSameContent(room.Room) && existing.Tier >= room.Tier) continue;

						_rooms[room.SessionId] = room;
						changed = true;
					}
					else
					{
						_rooms[room.SessionId] = room;
						changed = true;
					}
				}
			}

			foreach (string sessionId in _rooms.Keys.ToList())
			{
				if (live.Contains(sessionId)) continue;

				_rooms.Remove(sessionId);
				changed = true;
			}

			if (changed) OnRoomsChanged?.Invoke();
		}

		private static IPAddress ParseAddress(string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return null;
			return IPAddress.TryParse(value.Trim(), out var address) ? address : null;
		}

		/// <summary>
		/// Publishes a message on the diagnostic channel (the lobby UI shows it in the status). Also
		/// used by GameRoomManager to warn about the host public key (TOFU).
		/// </summary>
		public void ReportDiagnostic(string message) => Diag(message);

		private void Diag(string message)
		{
			if (_logDiagnostics) Debug.Log($"[RoomDiscovery] {message}");
			OnDiagnostic?.Invoke(message);
		}
	}
}
