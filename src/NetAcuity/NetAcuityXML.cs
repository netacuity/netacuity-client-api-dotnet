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
using System.Collections.Generic;

namespace NetAcuity
{
    /// <summary>
    /// A client for querying the NetAcuity Server over the recommended XML UDP protocol,
    /// which supports querying multiple feature codes in a single call.
    /// </summary>
    public interface INetAcuityXML
    {
        /// <summary>Initializes the connection parameters for subsequent queries.</summary>
        /// <param name="serverIP">The IP address of the NetAcuity Server to query.</param>
        /// <param name="apiID">The API ID assigned by Digital Element for this client.</param>
        /// <param name="timeoutMicroseconds">The query timeout, in microseconds.</param>
        void Create(string serverIP, int apiID = 0, int timeoutMicroseconds = 2_000_000);

        /// <summary>
        /// Initializes the connection parameters for subsequent queries, targeting a non-standard
        /// UDP port. Real NetAcuity Servers only listen on the standard port; this overload exists
        /// so tests can point the client at an in-process mock server bound to an OS-assigned
        /// ephemeral port.
        /// </summary>
        /// <param name="serverIP">The IP address of the NetAcuity Server to query.</param>
        /// <param name="apiID">The API ID assigned by Digital Element for this client.</param>
        /// <param name="timeoutMicroseconds">The query timeout, in microseconds.</param>
        /// <param name="serverUdpPort">The UDP port to send requests to.</param>
        void Create(string serverIP, int apiID, int timeoutMicroseconds, int serverUdpPort);

        /// <summary>Queries one or more feature codes for an IP address.</summary>
        /// <param name="queryIP">The IP address to look up.</param>
        /// <param name="featureCodes">A comma-separated list of feature codes to query, e.g. <c>"3,4"</c>.</param>
        /// <param name="transactionID">A caller-supplied ID echoed back in the response.</param>
        /// <exception cref="NetAcuityException">The query failed, a feature code was invalid, or the server reported an error.</exception>
        void QueryXML(string queryIP, string featureCodes, string transactionID);

        /// <summary>Retrieves a parsed response field by name after a successful query.</summary>
        /// <param name="fieldName">The response field to look up, e.g. <c>"country"</c>.</param>
        string FieldValue(string fieldName);

        /// <summary>Gets the key/value pairs parsed from the most recent response.</summary>
        IReadOnlyDictionary<string, string> ResponseFields();

        /// <summary>
        /// Gets the field names from the most recent response in wire order.
        /// <see cref="ResponseFields"/>'s dictionary has no documented iteration-order
        /// guarantee; use this when the original order matters.
        /// </summary>
        IReadOnlyList<string> FieldOrder();
    }

    /// <inheritdoc cref="INetAcuityXML"/>
    public class NetAcuityXML : INetAcuityXML
    {
        private NaXMLQuery _xmlQuery;

        /// <inheritdoc/>
        public void Create(string serverIP, int apiID = 0, int timeoutMicroseconds = 2_000_000)
        {
            _xmlQuery = new NaXMLQuery(serverIP, apiID, timeoutMicroseconds);
        }

        /// <inheritdoc/>
        public void Create(string serverIP, int apiID, int timeoutMicroseconds, int serverUdpPort)
        {
            _xmlQuery = new NaXMLQuery(serverIP, apiID, timeoutMicroseconds, serverUdpPort);
        }

        /// <inheritdoc/>
        public void QueryXML(string queryIP, string featureCodes, string transactionID)
        {
            _xmlQuery.QueryAndParse(queryIP, featureCodes, transactionID);
        }

        /// <inheritdoc/>
        public string FieldValue(string fieldName)
        {
            return _xmlQuery.GetFieldValue(fieldName);
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> ResponseFields()
        {
            return _xmlQuery.GetResponseFields();
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> FieldOrder()
        {
            return _xmlQuery.GetFieldOrder();
        }
    }
}
