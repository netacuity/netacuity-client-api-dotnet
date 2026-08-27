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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml;

namespace NetAcuity
{
    /// <summary>
    /// Low-level client for querying the NetAcuity Server over the recommended XML UDP protocol.
    /// <see cref="NetAcuityXML"/> is the friendlier facade most callers should use instead.
    /// </summary>
    public class NaXMLQuery
    {
        /// <summary>The standard UDP port that real NetAcuity Servers listen on.</summary>
        public const int DEFAULT_SERVER_UDP_PORT = 5400;

        private const int MAX_RESPONSE_SIZE = 1500;  // Max response size from NA server.

        private readonly int _serverUdpPort;

        /// <summary>The IP address of the NetAcuity Server.</summary>
        public string ServerIP { get; }

        /// <summary>The API ID for this client.</summary>
        public int ApiID { get; }

        /// <summary>The query timeout, in microseconds.</summary>
        public int TimeoutMicroseconds { get; }

        /// <summary>The raw XML response buffer from the most recent query.</summary>
        public string ResponseXML { get; private set; } = "";

        private readonly Dictionary<string, string> _responseFields = new Dictionary<string, string>();

        // Dictionary<TKey,TValue> has no documented iteration-order guarantee, so this
        // separately records field names from the most recent ParseResponse() call in the
        // order XmlReader handed them back (wire order), for callers who need it.
        private readonly List<string> _fieldOrder = new List<string>();
        private string _lastQueryIP;
        private string _lastTransactionID;

        /// <summary>
        /// Creates the UDP socket used by <see cref="QueryXML"/>. Overridable as an
        /// extension point for subclasses that need to customize socket creation.
        /// </summary>
        protected virtual Socket CreateSocket(AddressFamily addressFamily) =>
            new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);

        /// <summary>Creates a client bound to the given NetAcuity Server, API ID, and query timeout.</summary>
        /// <param name="serverIP">The IP address of the NetAcuity Server to query.</param>
        /// <param name="apiID">The API ID assigned by Digital Element for this client.</param>
        /// <param name="timeoutMicroseconds">The query timeout, in microseconds.</param>
        public NaXMLQuery(string serverIP, int apiID = 0, int timeoutMicroseconds = 2_000_000)
            : this(serverIP, apiID, timeoutMicroseconds, DEFAULT_SERVER_UDP_PORT)
        {
        }

        /// <summary>
        /// Creates a client bound to the given NetAcuity Server, API ID, query timeout, and UDP port.
        /// Real NetAcuity Servers only listen on <see cref="DEFAULT_SERVER_UDP_PORT"/>; this overload
        /// exists so tests can point the client at an in-process mock server bound to an OS-assigned
        /// ephemeral port, avoiding the flakiness of every test run competing for the same fixed port.
        /// </summary>
        /// <param name="serverIP">The IP address of the NetAcuity Server to query.</param>
        /// <param name="apiID">The API ID assigned by Digital Element for this client.</param>
        /// <param name="timeoutMicroseconds">The query timeout, in microseconds.</param>
        /// <param name="serverUdpPort">The UDP port to send requests to.</param>
        public NaXMLQuery(string serverIP, int apiID, int timeoutMicroseconds, int serverUdpPort)
        {
            if (apiID < 0 || apiID > 127)
            {
                throw new NetAcuityException("Invalid API ID.");
            }

            ServerIP = serverIP;
            ApiID = apiID;
            TimeoutMicroseconds = timeoutMicroseconds;
            _serverUdpPort = serverUdpPort;
        }

        /// <summary>Queries one or more feature codes for an IP address over the XML UDP protocol.</summary>
        /// <param name="queryIP">The IP address to look up.</param>
        /// <param name="featureCodes">A comma-separated list of feature codes to query, e.g. <c>"3,4"</c>.</param>
        /// <param name="transactionID">A caller-supplied ID echoed back in the response.</param>
        /// <exception cref="NetAcuityException">The query failed, a feature code was invalid, or the server reported an error.</exception>
        public void QueryXML(string queryIP, string featureCodes, string transactionID)
        {
            ResponseXML = "";

            if (!IPAddress.TryParse(queryIP, out _))
            {
                throw new NetAcuityException("Invalid queryIP");
            }
            if (!string.IsNullOrEmpty(transactionID) && transactionID.IndexOfAny(new[] { '"', '<', '>', '&' }) >= 0)
            {
                throw new NetAcuityException("Invalid transactionID");
            }

            _lastQueryIP = queryIP;
            _lastTransactionID = transactionID;

            try
            {
                IPAddress serverNumAddr = IPAddress.Parse(ServerIP);
                IPEndPoint remoteEndPoint = new IPEndPoint(serverNumAddr, _serverUdpPort);

                using (Socket apiSock = CreateSocket(remoteEndPoint.Address.AddressFamily))
                {
                    apiSock.ReceiveTimeout = TimeoutMicroseconds / 1000;
                    // Restricts the OS to only deliver datagrams from remoteEndPoint on this socket,
                    // rejecting spoofed/stray packets from any other source before they reach this code.
                    apiSock.Connect(remoteEndPoint);

                    var queryBuilder = new StringBuilder();
                    queryBuilder.Append($"<request trans-id=\"{transactionID}\" ip=\"{queryIP}\" api-id=\"{ApiID}\" > ");

                    // For each database requested, build an xml tag for that database feature code:
                    // <query db="<feature code>" />, where <feature code> is a valid feature code number.
                    foreach (string stringFeatureCode in featureCodes.Split(','))
                    {
                        if (string.IsNullOrEmpty(stringFeatureCode))
                        {
                            break;
                        }

                        int featureCode = Convert.ToInt32(stringFeatureCode);
                        if (featureCode >= 100 || featureCode < 3)
                        {
                            throw new NetAcuityException($"Request for feature {stringFeatureCode} is invalid.");
                        }

                        queryBuilder.Append($"<query db=\"{stringFeatureCode}\" />");
                    }
                    queryBuilder.Append("</request>");

                    byte[] sendBytes = Encoding.UTF8.GetBytes(queryBuilder.ToString());
                    apiSock.SendTo(sendBytes, sendBytes.Length, SocketFlags.None, remoteEndPoint);

                    var readList = new List<Socket> { apiSock };
                    Socket.Select(readList, null, null, TimeoutMicroseconds);
                    if (readList.Count == 0)
                    {
                        throw new NetAcuityException("Timed out waiting for a response from the NetAcuity Server.");
                    }

                    EndPoint tempRemoteEP = remoteEndPoint;
                    var responseBuffer = new StringBuilder();
                    int lastPacketNumber = 0;
                    bool isDone = false;

                    while (!isDone)
                    {
                        byte[] receiveBytes = new byte[MAX_RESPONSE_SIZE];
                        apiSock.ReceiveFrom(receiveBytes, ref tempRemoteEP);
                        string tempResponse = Encoding.UTF8.GetString(receiveBytes);

                        // The first 2 bytes represent the packet number, the second 2 bytes
                        // represent the total number of packets (i.e. 1 of 2, 2 of 2).
                        int packetNumber = Convert.ToInt32(tempResponse.Substring(0, 2));
                        int totalPacket = Convert.ToInt32(tempResponse.Substring(2, 2));

                        if (packetNumber - 1 != lastPacketNumber)
                        {
                            throw new NetAcuityException("Packets received out of order.");
                        }

                        lastPacketNumber = packetNumber;
                        responseBuffer.Append(tempResponse.Substring(4, tempResponse.IndexOf('\0') - 4));
                        isDone = packetNumber == totalPacket;
                    }

                    ResponseXML = responseBuffer.ToString();
                }
            }
            catch (NetAcuityException)
            {
                throw;
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut)
            {
                throw new NetAcuityException("Timed out waiting for a response from the NetAcuity Server.", e);
            }
            catch (Exception e)
            {
                throw new NetAcuityException($"Error querying NetAcuity Server: {e.Message}", e);
            }
        }

        private void ParseResponse()
        {
            _responseFields.Clear();
            _fieldOrder.Clear();

            try
            {
                using (StringReader stringReader = new StringReader(ResponseXML))
                using (XmlReader xmlReader = XmlReader.Create(stringReader))
                {
                    xmlReader.Read();
                    while (xmlReader.MoveToNextAttribute())
                    {
                        _responseFields[xmlReader.Name] = xmlReader.Value;
                        _fieldOrder.Add(xmlReader.Name);
                    }
                }
            }
            catch (XmlException e)
            {
                throw new NetAcuityException("Error parsing NetAcuity XML response.", e);
            }

            if (_responseFields.TryGetValue("error", out string error) && error.Length > 0)
            {
                throw new NetAcuityException(error);
            }

            // Make sure the response is actually answering this request, not some other
            // (possibly stale or spoofed) query — UDP has no built-in correlation.
            if (!_responseFields.TryGetValue("trans-id", out string responseTransId) || responseTransId != _lastTransactionID)
            {
                throw new NetAcuityException("response transactionID is out of sync with request transactionID");
            }
            if (!_responseFields.TryGetValue("ip", out string responseIp) || !IpsEqual(responseIp, _lastQueryIP))
            {
                throw new NetAcuityException("response address is out of sync with request address");
            }
        }

        /// <summary>
        /// Returns whether two IP address strings denote the same address, comparing parsed
        /// addresses rather than raw text so a differently-formatted-but-equal IPv6 literal
        /// (e.g. compressed vs. expanded) still matches. A malformed value is never treated
        /// as equal to anything.
        /// </summary>
        private static bool IpsEqual(string a, string b) =>
            IPAddress.TryParse(a, out IPAddress parsedA) &&
            IPAddress.TryParse(b, out IPAddress parsedB) &&
            parsedA.Equals(parsedB);

        /// <summary>Queries one or more feature codes and parses the response fields in one call.</summary>
        /// <param name="queryIP">The IP address to look up.</param>
        /// <param name="featureCode">A comma-separated list of feature codes to query, e.g. <c>"3,4"</c>.</param>
        /// <param name="transactionID">A caller-supplied ID echoed back in the response.</param>
        /// <exception cref="NetAcuityException">The query failed, a feature code was invalid, or the server reported an error.</exception>
        public void QueryAndParse(string queryIP, string featureCode, string transactionID)
        {
            QueryXML(queryIP, featureCode, transactionID);
            ParseResponse();
        }

        /// <summary>Gets the key/value pairs parsed from the most recent response.</summary>
        public IReadOnlyDictionary<string, string> GetResponseFields() => _responseFields;

        /// <summary>Gets the value of a specific field from the most recent response.</summary>
        /// <param name="fieldName">The response field to look up.</param>
        public string GetFieldValue(string fieldName) =>
            _responseFields.TryGetValue(fieldName, out string value) ? value : null;

        /// <summary>
        /// Gets the field names from the most recent response in wire order.
        /// <see cref="GetResponseFields"/>'s <see cref="Dictionary{TKey,TValue}"/> has no
        /// documented iteration-order guarantee; use this when the original order matters.
        /// </summary>
        public IReadOnlyList<string> GetFieldOrder() => _fieldOrder;
    }
}
