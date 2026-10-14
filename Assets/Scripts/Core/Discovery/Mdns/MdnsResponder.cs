using System;
using System.Collections.Generic;
using System.Net;

namespace Core.Discovery.Mdns
{
	/// <summary>
	/// Host mDNS responder: announces the room on 224.0.0.251:5353 and answers queries
	/// (unicast when the client asks with the QU bit, multicast otherwise, as the RFC requires).
	/// Deliberately lazy cadence: announce on open, spaced re-announcements and goodbye on exit.
	/// No probing/renaming: the SRV target name includes a unique session suffix, so there is no
	/// name conflict to resolve (a conscious simplification of RFC 6762).
	/// </summary>
	public sealed class MdnsResponder : IRoomAdvertiser
	{
		public static readonly TimeSpan DefaultReannounceInterval = TimeSpan.FromSeconds(45);

		private readonly IMulticastSocket _socket;
		private readonly IPAddress _group;
		private readonly int _port;
		private readonly IPAddress _localAddress;
		private readonly TimeSpan _ttl;
		private readonly TimeSpan _reannounceInterval;

		private RoomAdvertisement _current;
		private DateTime _nextAnnounceUtc;

		public MdnsResponder(IMulticastSocket socket, IPAddress group, int port, IPAddress localAddress,
				TimeSpan? ttl = null, TimeSpan? reannounceInterval = null)
		{
			_socket = socket ?? throw new ArgumentNullException(nameof(socket));
			_group = group ?? throw new ArgumentNullException(nameof(group));
			if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));

			_port = port;
			_localAddress = localAddress;
			_ttl = ttl ?? MdnsRecordBuilder.DefaultTtl;
			_reannounceInterval = reannounceInterval ?? DefaultReannounceInterval;
		}

		public bool IsAdvertising => _current != null;

		public RoomAdvertisement Current => _current;

		public void StartAdvertising(RoomAdvertisement advertisement, DateTime utcNow)
		{
			_current = advertisement ?? throw new ArgumentNullException(nameof(advertisement));

			SendAnnouncement();
			_nextAnnounceUtc = utcNow + _reannounceInterval;
		}

		public void UpdateAdvertisement(RoomAdvertisement advertisement, DateTime utcNow)
		{
			if (advertisement == null) throw new ArgumentNullException(nameof(advertisement));

			if (_current == null || !string.Equals(_current.SessionId, advertisement.SessionId, StringComparison.Ordinal))
			{
				StartAdvertising(advertisement, utcNow);
				return;
			}

			_current = advertisement;
			SendAnnouncement();
			_nextAnnounceUtc = utcNow + _reannounceInterval;
		}

		public void StopAdvertising()
		{
			if (_current == null) return;

			byte[] goodbye = MdnsCodec.EncodeResponse(0, authoritative: true, MdnsRecordBuilder.GoodbyeRecords(_current));
			_socket.SendMulticast(goodbye, _group, _port);
			_socket.SendMulticast(goodbye, _group, _port);

			_current = null;
		}

		public void HandleDatagram(MulticastDatagram datagram, DateTime utcNow)
		{
			if (_current == null) return;
			if (!MdnsCodec.TryDecode(datagram.Payload, out MdnsMessage message)) return;
			if (message.IsResponse || message.IsTruncated) return;
			if (message.Questions.Count == 0) return;

			IReadOnlyList<MdnsRecord> answers = BuildAnswers(message);
			if (answers == null || answers.Count == 0) return;

			byte[] payload = MdnsCodec.EncodeResponse(0, authoritative: true, answers);

			if (message.AnyUnicastResponseRequested())
			{
				_socket.SendUnicast(payload, datagram.Remote);
			}
			else
			{
				_socket.SendMulticast(payload, _group, _port);
			}
		}

		public void Tick(DateTime utcNow)
		{
			if (_current == null) return;
			if (utcNow < _nextAnnounceUtc) return;

			// Advance the timer BEFORE sending: if SendAnnouncement throws (network down), the next
			// attempt waits for the full reannounce interval instead of retrying every frame and
			// throwing the same exception in a loop.
			_nextAnnounceUtc = utcNow + _reannounceInterval;
			SendAnnouncement();
		}

		public void Dispose()
		{
			StopAdvertising();
		}

		private void SendAnnouncement()
		{
			var records = MdnsRecordBuilder.AnnouncementRecords(_current, _localAddress, _ttl, cacheFlush: true);
			byte[] payload = MdnsCodec.EncodeResponse(0, authoritative: true, records);
			_socket.SendMulticast(payload, _group, _port);
		}

		/// <summary>Builds only the answers relevant to the received questions (or null if it is not for us).</summary>
		private IReadOnlyList<MdnsRecord> BuildAnswers(MdnsMessage message)
		{
			string instance = MdnsRecordBuilder.InstanceFqdn(_current);
			string target = MdnsRecordBuilder.TargetNameFor(_current);

			bool wantsMeta = message.HasQuestion(MdnsRecordBuilder.MetaServiceName, MdnsRecordType.PTR);
			bool wantsService = message.HasQuestion(MdnsRecordBuilder.ServiceType, MdnsRecordType.PTR);
			bool wantsInstance = message.HasQuestion(instance, MdnsRecordType.ANY)
				|| message.HasQuestion(instance, MdnsRecordType.SRV)
				|| message.HasQuestion(instance, MdnsRecordType.TXT);
			bool wantsAddress = message.HasQuestion(target, MdnsRecordType.A)
				|| message.HasQuestion(target, MdnsRecordType.ANY);

			if (!wantsMeta && !wantsService && !wantsInstance && !wantsAddress) return null;

			// Answers are unicast or multicast depending on the QU bit; cache-flush only on multicast.
			bool cacheFlush = !message.AnyUnicastResponseRequested();

			var answers = new List<MdnsRecord>(4);

			if (wantsMeta)
			{
				answers.Add(MdnsRecord.Ptr(MdnsRecordBuilder.MetaServiceName, MdnsRecordBuilder.ServiceType,
							_ttl, cacheFlush));
			}

			if (wantsService || wantsInstance)
			{
				answers.Add(MdnsRecord.Ptr(MdnsRecordBuilder.ServiceType, instance, _ttl, cacheFlush));
				answers.Add(MdnsRecord.Srv(instance, _current.Port, target, _ttl, cacheFlush: cacheFlush));
				answers.Add(MdnsRecord.Txt(instance, MdnsRecordBuilder.BuildTxtStrings(_current), _ttl, cacheFlush));
			}

			if (_localAddress != null && (wantsAddress || wantsService))
			{
				answers.Add(MdnsRecord.AddressA(target, _localAddress, _ttl, cacheFlush));
			}

			return answers;
		}
	}
}
