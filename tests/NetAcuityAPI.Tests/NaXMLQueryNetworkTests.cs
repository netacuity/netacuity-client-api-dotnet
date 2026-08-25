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
using System.Text;
using NetAcuity;
using NetAcuityAPI.Tests.Helpers;
using Xunit;

namespace NetAcuityAPI.Tests
{
    /// <summary>
    /// Tests for NaXMLQuery that require the mock UDP server (bound to an OS-assigned ephemeral port).
    /// All tests in this class are serialised by [Collection("NetworkTests")].
    /// </summary>
    [Collection("NetworkTests")]
    public class NaXMLQueryNetworkTests
    {
        private const string Server  = "127.0.0.1";
        private const string QueryIp = "203.0.113.1";
        private const string TxnId   = "test-txn-42";
        private const int    ApiId   = 0;
        private const int    Timeout = 2_000_000; // 2 s

        // ── QueryXML ─────────────────────────────────────────────────────────

        [Fact]
        public void QueryXML_WithValidSingleFeatureCode_DoesNotThrow()
        {
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ =>
                    MockNetAcuityServer.BuildXmlResponse(
                        "<response trans-id=\"" + TxnId + "\" ip=\"" + QueryIp + "\" geo-country=\"usa\" />");

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryXML(QueryIp, "3", TxnId);
            }
        }

        [Fact]
        public void QueryXML_ValidResponse_PopulatesResponseXml()
        {
            string xml = "<response trans-id=\"" + TxnId + "\" ip=\"" + QueryIp + "\" geo-country=\"deu\" />";
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(xml);

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryXML(QueryIp, "3", TxnId);

                Assert.Contains("geo-country", q.ResponseXML);
                Assert.Contains("deu", q.ResponseXML);
            }
        }

        [Fact]
        public void QueryXML_SendsRequestContainingTransactionId()
        {
            byte[] captured = null;
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = req =>
                {
                    captured = req;
                    return MockNetAcuityServer.BuildXmlResponse(
                        "<response trans-id=\"" + TxnId + "\" ip=\"" + QueryIp + "\" />");
                };

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryXML(QueryIp, "3", TxnId);
            }

            string requestXml = Encoding.UTF8.GetString(captured);
            Assert.Contains("trans-id=\"" + TxnId + "\"", requestXml);
        }

        [Fact]
        public void QueryXML_SendsRequestContainingQueryIp()
        {
            byte[] captured = null;
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = req =>
                {
                    captured = req;
                    return MockNetAcuityServer.BuildXmlResponse(
                        "<response trans-id=\"" + TxnId + "\" ip=\"" + QueryIp + "\" />");
                };

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryXML(QueryIp, "3", TxnId);
            }

            string requestXml = Encoding.UTF8.GetString(captured);
            Assert.Contains("ip=\"" + QueryIp + "\"", requestXml);
        }

        [Fact]
        public void QueryXML_SendsRequestContainingAllFeatureCodes()
        {
            byte[] captured = null;
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = req =>
                {
                    captured = req;
                    return MockNetAcuityServer.BuildXmlResponse(
                        "<response trans-id=\"" + TxnId + "\" ip=\"" + QueryIp + "\" />");
                };

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryXML(QueryIp, "3,8,33", TxnId);
            }

            string requestXml = Encoding.UTF8.GetString(captured);
            Assert.Contains("db=\"3\"", requestXml);
            Assert.Contains("db=\"8\"", requestXml);
            Assert.Contains("db=\"33\"", requestXml);
        }

        // ── QueryAndParse ─────────────────────────────────────────────────────

        [Fact]
        public void QueryAndParse_WithValidResponse_DoesNotThrow()
        {
            string xml = "<response trans-id=\"" + TxnId + "\" ip=\"" + QueryIp + "\" geo-country=\"usa\" />";
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(xml);

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryAndParse(QueryIp, "3", TxnId);
            }
        }

        [Fact]
        public void QueryAndParse_WithValidResponse_PopulatesFieldTable()
        {
            string xml = "<response trans-id=\"" + TxnId + "\" ip=\"" + QueryIp
                       + "\" geo-country=\"usa\" geo-city=\"atlanta\" />";
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(xml);

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryAndParse(QueryIp, "3", TxnId);

                Assert.Equal("usa", q.GetFieldValue("geo-country"));
                Assert.Equal("atlanta", q.GetFieldValue("geo-city"));
            }
        }

        [Fact]
        public void QueryAndParse_ResponseWithErrorAttribute_ThrowsNetAcuityException()
        {
            string xml = "<response trans-id=\"" + TxnId + "\" ip=\"" + QueryIp
                       + "\" error=\"DB Not Loaded\" />";
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(xml);

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                var ex = Assert.Throws<NetAcuityException>(() => q.QueryAndParse(QueryIp, "3", TxnId));

                Assert.Equal("DB Not Loaded", ex.Message);
            }
        }

        [Fact]
        public void QueryAndParse_ResponseIpMismatch_ThrowsNetAcuityException()
        {
            string xml = "<response trans-id=\"" + TxnId + "\" ip=\"203.0.113.99\" geo-country=\"usa\" />";
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(xml);

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                var ex = Assert.Throws<NetAcuityException>(() => q.QueryAndParse(QueryIp, "3", TxnId));

                Assert.Contains("out of sync", ex.Message);
            }
        }

        [Fact]
        public void QueryAndParse_DifferentlyFormattedButEqualIPv6ResponseIp_DoesNotThrow()
        {
            // The server may echo back a compressed form of the same IPv6 address that was
            // queried in expanded form -- this must not be treated as a mismatch.
            const string expandedIpv6 = "2001:0db8:0000:0000:0000:0000:0000:0001";
            const string compressedIpv6 = "2001:db8::1";
            string xml = "<response trans-id=\"" + TxnId + "\" ip=\"" + compressedIpv6 + "\" geo-country=\"usa\" />";
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(xml);

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryAndParse(expandedIpv6, "3", TxnId);

                Assert.Equal("usa", q.GetFieldValue("geo-country"));
            }
        }

        [Fact]
        public void QueryAndParse_CalledTwiceOnSameInstance_BothCallsSucceed()
        {
            // The response-field table is cleared at the start of each parse, so
            // repeated calls on the same instance reflect only the latest response.
            string xml = "<response trans-id=\"" + TxnId + "\" ip=\"203.0.113.1\" geo-country=\"usa\" />";
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(xml);

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryAndParse(QueryIp, "3", TxnId);
                q.QueryAndParse(QueryIp, "3", TxnId);

                Assert.Equal("usa", q.GetFieldValue("geo-country"));
            }
        }

        // ── GetFieldValue / GetResponseFields ─────────────────────────────────

        [Fact]
        public void GetFieldValue_AfterSuccessfulQuery_ReturnsValue()
        {
            string xml = "<response trans-id=\"" + TxnId + "\" ip=\"203.0.113.1\" isp-name=\"comcast\" />";
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(xml);

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryAndParse(QueryIp, "8", TxnId);

                Assert.Equal("comcast", q.GetFieldValue("isp-name"));
            }
        }

        [Fact]
        public void GetFieldValue_ForUnknownField_ReturnsNull()
        {
            string xml = "<response trans-id=\"" + TxnId + "\" ip=\"203.0.113.1\" geo-country=\"usa\" />";
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(xml);

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryAndParse(QueryIp, "3", TxnId);

                Assert.Null(q.GetFieldValue("no-such-field"));
            }
        }

        [Fact]
        public void GetResponseFields_AfterSuccessfulQuery_ContainsAllAttributes()
        {
            string xml = "<response trans-id=\"" + TxnId + "\" ip=\"203.0.113.1\" geo-country=\"fra\" geo-region=\"ile-de-france\" />";
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(xml);

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryAndParse(QueryIp, "3", TxnId);

                var fields = q.GetResponseFields();

                Assert.True(fields.ContainsKey("geo-country"));
                Assert.Equal("fra", fields["geo-country"]);
                Assert.True(fields.ContainsKey("geo-region"));
                Assert.Equal("ile-de-france", fields["geo-region"]);
            }
        }

        [Fact]
        public void GetFieldOrder_MatchesWireOrderNotDictionaryOrder()
        {
            // geo-region appears before geo-country on the wire, the opposite of their
            // alphabetical order — GetFieldOrder() must reflect the wire order.
            string xml = "<response trans-id=\"" + TxnId + "\" ip=\"203.0.113.1\" geo-region=\"ile-de-france\" geo-country=\"fra\" />";
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(xml);

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryAndParse(QueryIp, "3", TxnId);

                Assert.Equal(new[] { "trans-id", "ip", "geo-region", "geo-country" }, q.GetFieldOrder());
            }
        }

        // ── Multi-packet assembly ─────────────────────────────────────────────

        [Fact]
        public void QueryXML_TwoPacketResponse_AssemblesCompleteXml()
        {
            // Split the XML across two UDP datagrams and verify the client reassembles them.
            string part1 = "<response trans-id=\"t\" ip=\"203.0.113.1\" ";
            string part2 = "geo-country=\"jpn\" />";

            using (var server = new MockNetAcuityServer())
            {
                server.MultiResponseHandler = _ => new[]
                {
                    MockNetAcuityServer.BuildXmlPacket(part1, 1, 2),
                    MockNetAcuityServer.BuildXmlPacket(part2, 2, 2),
                };

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                q.QueryXML(QueryIp, "3", TxnId);

                Assert.Contains("geo-country", q.ResponseXML);
                Assert.Contains("jpn", q.ResponseXML);
            }
        }

        [Fact]
        public void QueryXML_PacketsOutOfOrder_ThrowsNetAcuityException()
        {
            // Send packet 2 before packet 1 — the client detects the ordering violation.
            string part = "geo-country=\"kor\" ";

            using (var server = new MockNetAcuityServer())
            {
                server.MultiResponseHandler = _ => new[]
                {
                    MockNetAcuityServer.BuildXmlPacket(part, 2, 2), // wrong: 2 arrives first
                    MockNetAcuityServer.BuildXmlPacket(part, 1, 2),
                };

                var q = new NaXMLQuery(Server, ApiId, Timeout, server.Port);
                var ex = Assert.Throws<NetAcuityException>(() => q.QueryXML(QueryIp, "3", TxnId));

                Assert.Contains("out of order", ex.Message);
            }
        }
    }
}
