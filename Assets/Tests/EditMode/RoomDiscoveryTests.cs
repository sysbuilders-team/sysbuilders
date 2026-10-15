using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Core.Discovery;
using Core.Discovery.Mdns;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Testes da descoberta de salas (mDNS + multicast) com socket e relogio FALSOS: nenhum
    /// pacote real, nenhuma porta real, nenhuma dependencia de Unity/Mirror. Espelha a bateria
    /// que roda fora do Editor em .sbdiag/discovery-tests.
    /// </summary>
    [TestFixture]
    public class RoomDiscoveryTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private static RoomAdvertisement Room(string id, string name = "Sala do Bruno", int players = 1,
            int port = 7777, string theme = "Biblioteca", RoomState state = RoomState.Lobby, int version = 1)
            => new RoomAdvertisement(id, name, "Bruno", theme, port, players, 2, true, state, null, version);

        // ---------------------------------------------------------------- modelo

        [Test]
        public void RoomAdvertisement_SanitizesRoomNameAndClampsPlayers()
        {
            var room = new RoomAdvertisement("id-1", "  Sala   do  Bruno!!! <script>  ", "Bruno", "Biblioteca", 7777, 5, 2);

            Assert.AreEqual("Sala do Bruno script", room.RoomName);
            Assert.AreEqual(2, room.PlayerCount);
        }

        [Test]
        public void RoomAdvertisement_WithoutUsableCharacters_FallsBackToSala()
        {
            Assert.AreEqual("Sala", new RoomAdvertisement("id-1", "###", "Host", "T", 7777, 0).RoomName);
        }

        [Test]
        public void RoomAdvertisement_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentException>(() => new RoomAdvertisement(" ", "x", "y", "z", 7777, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoomAdvertisement("id", "x", "y", "z", 0, 0));
        }

        [Test]
        public void RoomAdvertisement_HasSameContent_DetectsPlayerChange()
        {
            var room = Room("id-1");
            Assert.IsTrue(room.HasSameContent(room.WithPlayers(1, RoomState.Lobby)));
            Assert.IsFalse(room.HasSameContent(room.WithPlayers(2, RoomState.Lobby)));
        }

        // ---------------------------------------------------------------- mDNS (tier C)

        [Test]
        public void MdnsCodec_RoundTripsQueryWithUnicastResponseBit()
        {
            byte[] bytes = MdnsCodec.EncodeQuery(0, MdnsRecordBuilder.ServiceType, MdnsRecordType.PTR, true);

            Assert.IsTrue(MdnsCodec.TryDecode(bytes, out var message));
            Assert.AreEqual(1, message.Questions.Count);
            Assert.IsTrue(MdnsCodec.NamesEqual(message.Questions[0].Name, MdnsRecordBuilder.ServiceType));
            Assert.AreEqual(MdnsRecordType.PTR, message.Questions[0].Type);
            Assert.IsTrue(message.Questions[0].UnicastResponse);
            Assert.IsFalse(message.IsResponse);
        }

        [Test]
        public void MdnsRecordBuilder_Announcement_TravelsInNetworkByteOrder()
        {
            var room = Room("sessao-mdns", "Sala do Bruno");
            var records = MdnsRecordBuilder.AnnouncementRecords(room, IPAddress.Parse("192.168.1.10"),
                MdnsRecordBuilder.DefaultTtl, cacheFlush: true);

            byte[] bytes = MdnsCodec.EncodeResponse(0, true, records);

            // Ordem de rede: sem isso nenhum responder do mercado (dns-sd/avahi) entende o anuncio.
            Assert.AreEqual(0x00, bytes[4]);
            Assert.AreEqual(0x04, bytes[5], "ANCOUNT deveria estar em big-endian");

            Assert.IsTrue(MdnsCodec.TryDecode(bytes, out var message));
            Assert.AreEqual(4, message.Answers.Count);
            Assert.IsTrue(MdnsRecordBuilder.TryParseRoom(message, null, out var parsed, out int ttl));
            Assert.AreEqual("sessao-mdns", parsed.SessionId);
            Assert.AreEqual("Sala do Bruno", parsed.RoomName);
            Assert.AreEqual(7777, parsed.Port);
            Assert.AreEqual(120, ttl);
        }

        [Test]
        public void MdnsCodec_DecodesCompressedNames_AndRefusesPointerLoop()
        {
            var room = Room("sessao-c", "Sala C");
            byte[] compressed = BuildCompressedResponse(room);

            Assert.IsTrue(MdnsCodec.TryDecode(compressed, out var message));
            Assert.AreEqual(2, message.Answers.Count);
            Assert.AreEqual(MdnsRecordType.SRV, message.Answers[1].Type);
            Assert.IsTrue(MdnsCodec.NamesEqual(message.Answers[1].Name, MdnsRecordBuilder.InstanceFqdn(room)));

            Assert.IsFalse(MdnsCodec.TryDecode(BuildSelfPointerMessage(), out _), "ponteiro para si mesmo nao pode entrar em loop");
        }

        [Test]
        public void MdnsCodec_RandomAndTruncatedPayloads_NeverThrow()
        {
            var random = new Random(1234);

            for (int i = 0; i < 300; i++)
            {
                var junk = new byte[random.Next(14, 260)];
                random.NextBytes(junk);
                junk[2] = 0x84;   // parece resposta, para exercitar o parser

                Assert.DoesNotThrow(() => MdnsCodec.TryDecode(junk, out _));
            }

            byte[] valid = MdnsCodec.EncodeResponse(0, true,
                MdnsRecordBuilder.AnnouncementRecords(Room("x"), IPAddress.Parse("10.0.0.1"), TimeSpan.FromSeconds(120), false));

            Assert.IsFalse(MdnsCodec.TryDecode(valid.Take(20).ToArray(), out _));
            Assert.IsFalse(MdnsCodec.TryDecode(Array.Empty<byte>(), out _));
            Assert.IsFalse(MdnsCodec.TryDecode(null, out _));
        }

        [Test]
        public void MdnsTier_Announces_AnswersQuery_AndRemovesOnGoodbye()
        {
            var network = new FakeNetwork();
            var host = new FakeMulticastSocket(network, "192.168.1.10");
            var client = new FakeMulticastSocket(network, "192.168.1.20");
            var group = IPAddress.Parse("224.0.0.251");
            const int port = 5353;

            host.Bind(port);
            host.JoinGroup(group, host.Address);
            client.Bind(port);
            client.JoinGroup(group, client.Address);

            var responder = new MdnsResponder(host, group, port, host.Address);
            var browser = new MdnsBrowser(client, group, port);

            var found = new List<DiscoveredRoom>();
            var lost = new List<string>();
            browser.OnRoomFound += r => found.Add(r);
            browser.OnRoomLost += l => lost.Add(l);

            DateTime now = T0;
            host.StartReceiving(d => responder.HandleDatagram(d, now), _ => { });
            client.StartReceiving(d => browser.HandleDatagram(d, now), _ => { });

            responder.StartAdvertising(Room("sessao-C", "Sala C", 1, 7777, "E-Commerce"), now);

            Assert.AreEqual(1, found.Count);
            Assert.AreEqual(7777, found[0].Port);
            Assert.AreEqual("E-Commerce", found[0].Room.Theme);
            Assert.AreEqual(DiscoveryTier.Mdns, found[0].Tier);

            // Resposta no grupo (sem bit QU): evita a ambiguidade de dois sockets na mesma
            // porta quando Editor e build rodam na mesma maquina.
            int multicastBefore = network.MulticastDeliveries;
            browser.StartBrowsing(now);
            Assert.Greater(network.MulticastDeliveries, multicastBefore);

            browser.Tick(T0 + TimeSpan.FromSeconds(119));
            Assert.AreEqual(1, browser.Rooms.Count, "TTL do PTR (120s) ainda nao estourou");

            responder.StopAdvertising();
            Assert.AreEqual(0, browser.Rooms.Count);
            Assert.AreEqual(1, lost.Count);
        }

        // ---------------------------------------------------------------- expiracao e versao

        [Test]
        public void MdnsBrowser_ExpiresRoomThatStoppedAnnouncing()
        {
            var network = new FakeNetwork();
            var host = new FakeMulticastSocket(network, "192.168.1.10");
            var client = new FakeMulticastSocket(network, "192.168.1.20");
            var group = IPAddress.Parse("224.0.0.251");
            const int port = 5353;

            host.Bind(port);
            host.JoinGroup(group, host.Address);
            client.Bind(port);
            client.JoinGroup(group, client.Address);

            // TTL curto e re-anuncio a cada 10s: o browser expira pelo TTL do record PTR.
            var responder = new MdnsResponder(host, group, port, host.Address,
                ttl: TimeSpan.FromSeconds(20), reannounceInterval: TimeSpan.FromSeconds(10));
            var browser = new MdnsBrowser(client, group, port);

            var lost = new List<string>();
            browser.OnRoomLost += l => lost.Add(l);

            DateTime now = T0;
            host.StartReceiving(d => responder.HandleDatagram(d, now), _ => { });
            client.StartReceiving(d => browser.HandleDatagram(d, now), _ => { });

            responder.StartAdvertising(Room("sessao-T"), T0);
            Assert.AreEqual(1, browser.Rooms.Count);

            now = T0 + TimeSpan.FromSeconds(10);
            browser.Tick(now);
            Assert.AreEqual(1, browser.Rooms.Count, "dentro do TTL a sala permanece");

            responder.Tick(now);
            Assert.AreEqual(1, browser.Rooms.Count, "re-anuncio aceito pelo browser");

            browser.Tick(T0 + TimeSpan.FromSeconds(21));
            Assert.AreEqual(1, browser.Rooms.Count, "o re-anuncio salvou a sala do TTL");
            Assert.AreEqual(0, lost.Count);

            browser.Tick(T0 + TimeSpan.FromSeconds(40));
            Assert.AreEqual(0, browser.Rooms.Count, "sem re-anuncio o TTL remove a sala");
            Assert.AreEqual(1, lost.Count);
        }

        [Test]
        public void MdnsBrowser_IgnoresOtherProtocolVersion_AndWarnsOnce()
        {
            var network = new FakeNetwork();
            var host = new FakeMulticastSocket(network, "192.168.1.10");
            var client = new FakeMulticastSocket(network, "192.168.1.20");
            var group = IPAddress.Parse("224.0.0.251");
            const int port = 5353;

            host.Bind(port);
            host.JoinGroup(group, host.Address);
            client.Bind(port);
            client.JoinGroup(group, client.Address);

            var responder = new MdnsResponder(host, group, port, host.Address);
            var browser = new MdnsBrowser(client, group, port);

            var diagnostics = new List<string>();
            browser.OnDiagnostic += d => diagnostics.Add(d);

            DateTime now = T0;
            host.StartReceiving(d => responder.HandleDatagram(d, now), _ => { });
            client.StartReceiving(d => browser.HandleDatagram(d, now), _ => { });

            responder.StartAdvertising(Room("sessao-V", version: 2), T0);

            Assert.AreEqual(0, browser.Rooms.Count, "sala de versao diferente nao entra na lista");
            Assert.AreEqual(1, diagnostics.Count, "aviso de versao emitido uma unica vez");

            responder.UpdateAdvertisement(Room("sessao-V", "Sala V2", version: 2), T0);
            Assert.AreEqual(1, diagnostics.Count, "segundo anuncio incompativel nao repete o aviso");
        }

        // ---------------------------------------------------------------- TOFU e interface

        [Test]
        public void TofuPolicy_EvaluatesFirstUseMatchAndMismatch()
        {
            Assert.AreEqual(TofuDecision.Unknown, TofuPolicy.Evaluate(null, "AB:CD"));
            Assert.AreEqual(TofuDecision.Match, TofuPolicy.Evaluate("ab:cd", "AB:CD"));
            Assert.AreEqual(TofuDecision.Mismatch, TofuPolicy.Evaluate("ab:cd", "99:99"));
            Assert.AreEqual(TofuDecision.NoFingerprint, TofuPolicy.Evaluate("ab:cd", "  "));

            Assert.IsFalse(TofuPolicy.ShouldAccept(TofuDecision.Mismatch, rejectOnMismatch: true));
            Assert.IsTrue(TofuPolicy.ShouldAccept(TofuDecision.Mismatch, rejectOnMismatch: false));
            Assert.IsTrue(TofuPolicy.ShouldAccept(TofuDecision.Unknown, rejectOnMismatch: true));

            Assert.IsTrue(TofuPolicy.ShouldStore(TofuDecision.Unknown));
            Assert.IsFalse(TofuPolicy.ShouldStore(TofuDecision.Mismatch));
        }

        [Test]
        public void NetworkInterfaceSelector_PrefersPhysicalInterfaceWithGateway()
        {
            var lan = new InterfaceCandidate("Ethernet", "Realtek", IPAddress.Parse("192.168.1.37"), true, true, true, false);
            var virtualBox = new InterfaceCandidate("Ethernet 2", "VirtualBox Host-Only", IPAddress.Parse("192.168.56.1"), true, true, false, true);
            var tailscale = new InterfaceCandidate("Tailscale", "Tailscale Tunnel", IPAddress.Parse("100.102.100.91"), true, true, false, true);

            Assert.IsTrue(NetworkInterfaceSelector.TrySelectPrimary(new[] { virtualBox, tailscale, lan }, out var selected, out _));
            Assert.AreEqual("192.168.1.37", selected.Address.ToString());

            Assert.IsTrue(NetworkInterfaceSelector.TryFindByAddress(new[] { lan, virtualBox }, IPAddress.Parse("192.168.56.1"), out var forced));
            Assert.AreEqual("Ethernet 2", forced.Name);

            Assert.IsFalse(NetworkInterfaceSelector.TrySelectPrimary(Array.Empty<InterfaceCandidate>(), out _, out string reason));
            Assert.IsNotEmpty(reason);
        }

        // ---------------------------------------------------------------- PR-1 regressions

        [Test]
        public void MdnsTier_WhitespaceSessionId_IsRejectedNotThrown()
        {
            // C1 regression. TryParseRoom promises never to throw, but the old
            // IsNullOrEmpty check let "id= " reach RoomAdvertisement's constructor,
            // which uses IsNullOrWhiteSpace and throws.
            byte[] payload = BuildResponseWithSessionId(" ");
            Assert.IsTrue(MdnsCodec.TryDecode(payload, out var message));

            Assert.IsFalse(
                MdnsRecordBuilder.TryParseRoom(message, null, out _, out _),
                "whitespace session id must be rejected, not passed to the constructor");
        }

        [Test]
        public void MdnsTier_OversizedSessionId_IsRejected()
        {
            // A 200-char id passes IsNullOrEmpty. Without the cap, an unbounded
            // string would follow every DiscoveredRoom for the rest of the session.
            byte[] payload = BuildResponseWithSessionId(new string('a', 200));
            Assert.IsTrue(MdnsCodec.TryDecode(payload, out var message));

            Assert.IsFalse(
                MdnsRecordBuilder.TryParseRoom(message, null, out _, out _),
                "session id above 64 chars must be rejected");
        }

        [Test]
        public void MdnsTier_TtlAboveMax_IsClampedTo120()
        {
            // A hostile host could send ttl=2^31-1 and pin a ghost entry for years.
            var room = Room("sessao-ttl");
            var records = MdnsRecordBuilder.AnnouncementRecords(
                room, IPAddress.Parse("192.168.1.10"), TimeSpan.FromSeconds(99999), cacheFlush: false);
            byte[] payload = MdnsCodec.EncodeResponse(0, true, records);
            Assert.IsTrue(MdnsCodec.TryDecode(payload, out var message));

            Assert.IsTrue(MdnsRecordBuilder.TryParseRoom(message, null, out _, out int ttl));
            Assert.AreEqual(120, ttl);
        }

        [Test]
        public void MdnsBrowser_AfterStopBrowsing_IgnoresNewAnnouncements()
        {
            // H4 regression. HandleDatagram used to process every response
            // regardless of IsBrowsing, so a re-announcement from any host would
            // silently repopulate a list the player just dismissed.
            var network = new FakeNetwork();
            var host = new FakeMulticastSocket(network, "192.168.1.10");
            var client = new FakeMulticastSocket(network, "192.168.1.20");
            var group = IPAddress.Parse("224.0.0.251");
            const int port = 5353;

            host.Bind(port);
            host.JoinGroup(group, host.Address);
            client.Bind(port);
            client.JoinGroup(group, client.Address);

            var responder = new MdnsResponder(host, group, port, host.Address);
            var browser = new MdnsBrowser(client, group, port);

            DateTime now = T0;
            host.StartReceiving(d => responder.HandleDatagram(d, now), _ => { });
            client.StartReceiving(d => browser.HandleDatagram(d, now), _ => { });

            browser.StartBrowsing(now);
            responder.StartAdvertising(Room("sessao-A"), now);
            Assert.AreEqual(1, browser.Rooms.Count);

            browser.StopBrowsing();
            Assert.AreEqual(0, browser.Rooms.Count);

            // A fresh announcement from the same host must not resurrect the room.
            responder.UpdateAdvertisement(Room("sessao-A", "Sala A v2"), now + TimeSpan.FromSeconds(1));
            Assert.AreEqual(0, browser.Rooms.Count,
                "stopped browser must not process new announcements");
        }

        private static byte[] BuildResponseWithSessionId(string sessionId, int ttlSeconds = 120)
        {
            // Hand-built because RoomAdvertisement rejects the very values these
            // tests need to send through the wire.
            string instance = $"Instance.{MdnsRecordBuilder.ServiceType}";
            string target = "sb-test.local";
            var ttl = TimeSpan.FromSeconds(ttlSeconds);

            var records = new List<MdnsRecord>
            {
                MdnsRecord.Ptr(MdnsRecordBuilder.ServiceType, instance, ttl),
                MdnsRecord.Srv(instance, 7777, target, ttl),
                MdnsRecord.Txt(instance, new List<string>
                {
                    "v=1",
                    $"id={sessionId}",
                    "room=Sala",
                    "host=Host",
                    "players=1",
                    "max=2",
                    "theme=Tema",
                    "enc=1",
                    "state=lobby"
                }, ttl)
            };

            return MdnsCodec.EncodeResponse(0, true, records);
        }

        // ---------------------------------------------------------------- helpers de pacote

        private static byte[] BuildCompressedResponse(RoomAdvertisement room)
        {
            var bytes = new List<byte>();
            bytes.AddRange(new byte[] { 0x00, 0x00, 0x84, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00 });

            bytes.AddRange(MdnsCodec.EncodeName(MdnsRecordBuilder.ServiceType));
            bytes.AddRange(new byte[] { 0x00, 0x0C, 0x00, 0x01, 0x00, 0x00, 0x00, 0x78 });

            byte[] instance = MdnsCodec.EncodeName(MdnsRecordBuilder.InstanceFqdn(room));
            bytes.AddRange(new byte[] { (byte)(instance.Length >> 8), (byte)instance.Length });

            int instanceOffset = bytes.Count;
            bytes.AddRange(instance);

            // Nome do SRV comprimido: ponteiro para tras, apontando o nome da instancia
            bytes.Add((byte)(0xC0 | ((instanceOffset >> 8) & 0x3F)));
            bytes.Add((byte)(instanceOffset & 0xFF));
            bytes.AddRange(new byte[] { 0x00, 0x21, 0x00, 0x01, 0x00, 0x00, 0x00, 0x78 });

            byte[] target = MdnsCodec.EncodeName(MdnsRecordBuilder.TargetNameFor(room));
            var rdata = new List<byte> { 0, 0, 0, 0, (byte)(room.Port >> 8), (byte)room.Port };
            rdata.AddRange(target);

            bytes.AddRange(new byte[] { (byte)(rdata.Count >> 8), (byte)rdata.Count });
            bytes.AddRange(rdata);

            return bytes.ToArray();
        }

        private static byte[] BuildSelfPointerMessage()
        {
            return new byte[]
            {
                0x00, 0x00, 0x84, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
                0xC0, 0x0C,
                0x00, 0x0C, 0x00, 0x01, 0x00, 0x00, 0x00, 0x78,
                0x00, 0x02, 0x00, 0x00
            };
        }

        // ---------------------------------------------------------------- rede falsa

        private sealed class FakeNetwork
        {
            private readonly List<FakeMulticastSocket> _sockets = new List<FakeMulticastSocket>();

            public int MulticastDeliveries { get; private set; }

            public int UnicastDeliveries { get; private set; }

            public void Register(FakeMulticastSocket socket) => _sockets.Add(socket);

            public void Publish(IPAddress group, FakeMulticastSocket sender, byte[] payload)
            {
                if (payload == null) return;

                foreach (var socket in _sockets.ToList())
                {
                    if (ReferenceEquals(socket, sender)) continue;
                    if (!socket.IsReceiving) continue;
                    if (!socket.JoinedGroups.Contains(group)) continue;

                    MulticastDeliveries++;
                    socket.Deliver(new MulticastDatagram(payload, new IPEndPoint(sender.Address, sender.BoundPort)));
                }
            }

            public void SendUnicast(IPEndPoint destination, byte[] payload)
            {
                foreach (var socket in _sockets.ToList())
                {
                    if (!socket.IsReceiving) continue;
                    if (socket.BoundPort != destination.Port) continue;
                    if (!socket.Address.Equals(destination.Address)) continue;

                    UnicastDeliveries++;
                    socket.Deliver(new MulticastDatagram(payload, destination));
                    return;
                }
            }
        }

        private sealed class FakeMulticastSocket : IMulticastSocket
        {
            private readonly FakeNetwork _network;
            private Action<MulticastDatagram> _onDatagram;

            public FakeMulticastSocket(FakeNetwork network, string address)
            {
                _network = network;
                Address = IPAddress.Parse(address);
                network.Register(this);
            }

            public IPAddress Address { get; }
            public int BoundPort { get; private set; }
            public HashSet<IPAddress> JoinedGroups { get; } = new HashSet<IPAddress>();

            public bool IsReceiving { get; private set; }
            public byte TimeToLive { get; set; } = 1;
            public bool MulticastLoopback { get; set; }

            public void Bind(int port) => BoundPort = port;

            public void JoinGroup(IPAddress group, IPAddress localInterface) => JoinedGroups.Add(group);

            public void LeaveGroup(IPAddress group, IPAddress localInterface) => JoinedGroups.Remove(group);

            public void SetOutgoingInterface(IPAddress localInterface) { }

            public void SendMulticast(byte[] payload, IPAddress group, int port) => _network.Publish(group, this, payload);

            public void SendUnicast(byte[] payload, IPEndPoint destination) => _network.SendUnicast(destination, payload);

            public void StartReceiving(Action<MulticastDatagram> onDatagram, Action<Exception> onError)
            {
                _onDatagram = onDatagram;
                IsReceiving = true;
            }

            public void StopReceiving() => IsReceiving = false;

            public void Deliver(MulticastDatagram datagram) => _onDatagram?.Invoke(datagram);

            public void Dispose() => StopReceiving();
        }
    }
}
