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
using NetAcuity;
using Xunit;

namespace NetAcuityAPI.Tests
{
    /// <summary>
    /// Tests for NaXMLQuery that exercise pure logic paths requiring no network.
    /// Feature-code validation throws before any socket I/O, so these run fast.
    /// </summary>
    public class NaXMLQueryValidationTests
    {
        private const string Server = "127.0.0.1";
        private const int    ApiId  = 42;
        private const int    Timeout = 2_000_000; // 2 s

        // ── Constructor ──────────────────────────────────────────────────────

        [Fact]
        public void Constructor_StoresServerIp()
        {
            var q = new NaXMLQuery(Server, ApiId, Timeout);
            Assert.Equal(Server, q.ServerIP);
        }

        [Fact]
        public void Constructor_StoresApiId()
        {
            var q = new NaXMLQuery(Server, ApiId, Timeout);
            Assert.Equal(ApiId, q.ApiID);
        }

        [Fact]
        public void Constructor_StoresTimeout()
        {
            var q = new NaXMLQuery(Server, ApiId, Timeout);
            Assert.Equal(Timeout, q.TimeoutMicroseconds);
        }

        [Fact]
        public void Constructor_InitializesEmptyResponseXml()
        {
            var q = new NaXMLQuery(Server, ApiId, Timeout);
            Assert.Equal("", q.ResponseXML);
        }

        // ── Default UDP port ─────────────────────────────────────────────────
        // The three-arg constructor must keep targeting the real NetAcuity Server's
        // standard port. The four-arg, port-accepting overload added for tests must
        // never change what the three-arg constructor (used by every real caller) does.

        [Fact]
        public void DefaultServerUdpPort_Equals5400()
        {
            Assert.Equal(5400, NaXMLQuery.DEFAULT_SERVER_UDP_PORT);
        }

        // ── Initial state of query engine ────────────────────────────────────

        [Fact]
        public void GetFieldValue_BeforeAnyQuery_ReturnsNull()
        {
            var q = new NaXMLQuery(Server, ApiId, Timeout);
            Assert.Null(q.GetFieldValue("geo-country"));
        }

        [Fact]
        public void GetResponseFields_BeforeAnyQuery_IsEmpty()
        {
            var q = new NaXMLQuery(Server, ApiId, Timeout);
            Assert.Empty(q.GetResponseFields());
        }

        // ── Feature-code boundary validation ─────────────────────────────────
        // The method validates each code before opening any socket, so these
        // throw immediately without touching the network.

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void QueryXML_FeatureCodeBelowMinimum_ThrowsNetAcuityException(int code)
        {
            var q = new NaXMLQuery(Server, ApiId, Timeout);
            var ex = Assert.Throws<NetAcuityException>(() => q.QueryXML("203.0.113.1", code.ToString(), "txn"));
            Assert.Contains(code.ToString(), ex.Message);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(101)]
        [InlineData(999)]
        public void QueryXML_FeatureCodeAboveMaximum_ThrowsNetAcuityException(int code)
        {
            var q = new NaXMLQuery(Server, ApiId, Timeout);
            Assert.Throws<NetAcuityException>(() => q.QueryXML("203.0.113.1", code.ToString(), "txn"));
        }

        [Fact]
        public void QueryXML_MultipleCodesFirstInvalid_ThrowsWithoutSendingOtherCodes()
        {
            // "2,3" — 2 is invalid, so the method should bail without ever querying code 3
            var q = new NaXMLQuery(Server, ApiId, Timeout);
            Assert.Throws<NetAcuityException>(() => q.QueryXML("203.0.113.1", "2,3", "txn"));
        }
    }
}
