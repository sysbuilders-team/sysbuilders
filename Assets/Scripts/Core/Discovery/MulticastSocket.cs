using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Discovery
{
    public readonly struct MulticastDatagram
    {
        public byte[] Payload { get; }
        public IPEndPoint Remote { get; }

        /// <summary>
        /// Interface the packet arrived on, when the OS reports it (IP_PKTINFO is best-effort:
        /// on some platforms it is unavailable and comes back null). We do not rely on it to reply:
        /// the conversational response is always unicast back to the asker.
        /// </summary>
        public IPAddress LocalInterface { get; }

        public MulticastDatagram(byte[] payload, IPEndPoint remote, IPAddress localInterface = null)
        {
            Payload = payload;
            Remote = remote;
            LocalInterface = localInterface;
        }

        public override string ToString() => $"{Payload?.Length ?? 0} bytes de {Remote}";
    }

    public interface IMulticastSocket : IDisposable
    {
        bool IsReceiving { get; }
        byte TimeToLive { get; set; }
        bool MulticastLoopback { get; set; }

        void Bind(int port);
        void JoinGroup(IPAddress group, IPAddress localInterface);
        void LeaveGroup(IPAddress group, IPAddress localInterface);
        void SetOutgoingInterface(IPAddress localInterface);
        void SendMulticast(byte[] payload, IPAddress group, int port);
        void SendUnicast(byte[] payload, IPEndPoint destination);
        void StartReceiving(Action<MulticastDatagram> onDatagram, Action<Exception> onError);
        void StopReceiving();
    }

    /// <summary>
    /// Real multicast UDP socket. The details that matter in practice:
    ///  - SO_REUSEADDR before bind: lets several players/instances share one machine and
    ///    coexist with other listeners on the same port (validated on Windows);
    ///  - explicit per-interface join + IP_MULTICAST_IF when sending: without them the packet
    ///    can leave through the wrong interface (VPN, virtual adapter) and discovery fails silently;
    ///  - receive loop on a background thread; the Unity layer re-queues events on the main thread.
    /// </summary>
    public sealed class MulticastSocket : IMulticastSocket
    {
        private const int ReceiveBufferSize = 8192;

        private readonly Socket _socket;
        private CancellationTokenSource _cancellation;
        private Task _receiveTask;

        public MulticastSocket()
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.ExclusiveAddressUse = false;
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            TimeToLive = 1;
        }

        public bool IsReceiving { get; private set; }
        public byte TimeToLive { get; set; }
        public bool MulticastLoopback { get; set; }

        public void Bind(int port)
        {
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));

            var local = new IPEndPoint(IPAddress.Any, port);
            _socket.Bind(local);
        }

        public void JoinGroup(IPAddress group, IPAddress localInterface)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            var option = localInterface == null ? new MulticastOption(group) : new MulticastOption(group, localInterface);
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, option);
        }

        public void LeaveGroup(IPAddress group, IPAddress localInterface)
        {
            if (group == null) return;

            try
            {
                var option = localInterface == null ? new MulticastOption(group) : new MulticastOption(group, localInterface);
                _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership, option);
            }
            catch (Exception)
            {
                // leaving the group must never break shutdown
            }
        }

        public void SetOutgoingInterface(IPAddress localInterface)
        {
            if (localInterface == null) return;

            // IP_MULTICAST_IF with the address bytes works on both Windows and Unix in .NET.
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,
                localInterface.GetAddressBytes());
        }

        public void SendMulticast(byte[] payload, IPAddress group, int port)
        {
            if (payload == null || payload.Length == 0 || group == null) return;

            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, TimeToLive);
            _socket.MulticastLoopback = MulticastLoopback;
            _socket.SendTo(payload, new IPEndPoint(group, port));
        }

        public void SendUnicast(byte[] payload, IPEndPoint destination)
        {
            if (payload == null || payload.Length == 0 || destination == null) return;

            _socket.SendTo(payload, destination);
        }

        public void StartReceiving(Action<MulticastDatagram> onDatagram, Action<Exception> onError)
        {
            if (IsReceiving) return;

            _cancellation = new CancellationTokenSource();
            IsReceiving = true;
            var token = _cancellation.Token;

            _receiveTask = Task.Factory.StartNew(
                () => ReceiveLoop(onDatagram, onError, token),
                token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void StopReceiving()
        {
            if (!IsReceiving) return;

            IsReceiving = false;
            try
            {
                _cancellation?.Cancel();
                _socket.Close();
            }
            catch (Exception)
            {
                // close socket e best-effort
            }

            _receiveTask = null;
        }

        private void ReceiveLoop(Action<MulticastDatagram> onDatagram, Action<Exception> onError, CancellationToken token)
        {
            var buffer = new byte[ReceiveBufferSize];

            while (!token.IsCancellationRequested)
            {
                try
                {
                    EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    int read = _socket.ReceiveFrom(buffer, ref remote);
                    if (read <= 0) continue;

                    var payload = new byte[read];
                    Buffer.BlockCopy(buffer, 0, payload, 0, read);
                    onDatagram?.Invoke(new MulticastDatagram(payload, (IPEndPoint)remote));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    // an ICMP "port unreachable" from an earlier send arrives as an error on receive:
                    // that is no reason to end the loop.
                    if (token.IsCancellationRequested) break;
                    Thread.Sleep(10);
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested) break;
                    onError?.Invoke(ex);
                    Thread.Sleep(50);
                }
            }
        }

        public void Dispose()
        {
            StopReceiving();
            try
            {
                _socket.Dispose();
            }
            catch (Exception)
            {
                // ignore
            }

            _cancellation?.Dispose();
        }
    }
}
