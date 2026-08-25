// Copyright 2026 Digital Envoy, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NetAcuityAPI.Tests.Helpers
{
    /// <summary>
    /// A local UDP mock that binds to an OS-assigned ephemeral port and simulates NetAcuity
    /// server responses. Binding to port 0 (rather than the real NetAcuity protocol port,
    /// 5400) means the mock never contends with anything else on the machine that might
    /// already hold that port, so tests never flake due to a busy port. Instantiate in a
    /// using block, read <see cref="Port"/> after construction, and pass it to the
    /// port-accepting constructor/overload of the client under test.
    /// </summary>
    public sealed class MockNetAcuityServer : IDisposable
    {
        /// <summary>The OS-assigned port this mock server is actually listening on.</summary>
        public int Port => ((IPEndPoint)_udp.Client.LocalEndPoint).Port;

        private readonly UdpClient _udp;
        private readonly Thread _thread;
        private volatile bool _running;
        private bool _disposed;

        /// <summary>
        /// Given the raw request bytes, return the bytes to send back.
        /// Return null to drop the packet (simulates server silence / timeout path).
        /// For multi-packet responses, use MultiResponseHandler instead.
        /// </summary>
        public Func<byte[], byte[]> ResponseHandler { get; set; }

        /// <summary>
        /// Like ResponseHandler but allows sending multiple datagrams per request
        /// (needed to test multi-packet XML responses).
        /// When set, this takes precedence over ResponseHandler.
        /// </summary>
        public Func<byte[], IEnumerable<byte[]>> MultiResponseHandler { get; set; }

        public MockNetAcuityServer()
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            _udp.Client.ReceiveTimeout = 200; // poll interval so we can check _running
            _running = true;
            _thread = new Thread(Serve) { IsBackground = true, Name = "MockNetAcuityServer" };
            _thread.Start();
        }

        private void Serve()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udp.Receive(ref remote);

                    if (MultiResponseHandler != null)
                    {
                        foreach (byte[] packet in MultiResponseHandler(data))
                            _udp.Send(packet, packet.Length, remote);
                    }
                    else if (ResponseHandler != null)
                    {
                        byte[] response = ResponseHandler(data);
                        if (response != null)
                            _udp.Send(response, response.Length, remote);
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    // Normal poll timeout — loop back and check _running
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch when (!_running)
                {
                    break;
                }
            }
        }

        // ── XML protocol helpers ─────────────────────────────────────────────

        /// <summary>
        /// Builds a complete single-packet XML response payload.
        /// The client receive buffer (1496 bytes) is pre-zeroed, so the first null
        /// byte after our payload acts as the string terminator.
        /// </summary>
        public static byte[] BuildXmlResponse(string xml)
        {
            // Header: 2-char right-justified packet number + 2-char right-justified total
            string payload = string.Format("{0,2}{1,2}", 1, 1) + xml;
            return Encoding.UTF8.GetBytes(payload);
        }

        /// <summary>
        /// Builds one packet of a multi-packet XML response.
        /// </summary>
        public static byte[] BuildXmlPacket(string xmlChunk, int packetNumber, int totalPackets)
        {
            string payload = string.Format("{0,2}{1,2}", packetNumber, totalPackets) + xmlChunk;
            return Encoding.UTF8.GetBytes(payload);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _running = false;
            try { _udp.Dispose(); } catch { /* suppress */ }
            _thread.Join(600);
        }
    }
}
