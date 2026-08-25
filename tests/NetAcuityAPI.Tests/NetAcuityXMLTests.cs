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
using NetAcuity;
using NetAcuityAPI.Tests.Helpers;
using Xunit;

namespace NetAcuityAPI.Tests
{
    /// <summary>
    /// Tests for the public-facing NetAcuityXML wrapper class.
    /// </summary>
    [Collection("NetworkTests")]
    public class NetAcuityXMLTests
    {
        private const string Server  = "127.0.0.1";
        private const string QueryIp = "203.0.113.1";
        private const string TxnId   = "wrapper-txn";
        private const int    ApiId   = 74;
        private const int    Timeout = 2_000_000;

        private static string SimpleXml(string extraAttrs = "") =>
            "<response trans-id=\"" + TxnId + "\" ip=\"" + QueryIp + "\" " + extraAttrs + "/>";

        // ── Preconditions ────────────────────────────────────────────────────

        [Fact]
        public void QueryXML_CalledBeforeCreate_ThrowsNullReferenceException()
        {
            var naxml = new NetAcuityXML();
            Assert.Throws<NullReferenceException>(() =>
                naxml.QueryXML(QueryIp, "3", TxnId));
        }

        // ── Create + QueryXML ─────────────────────────────────────────────────

        [Fact]
        public void Create_Then_QueryXML_DoesNotThrow()
        {
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(SimpleXml("geo-country=\"usa\" "));

                var naxml = new NetAcuityXML();
                naxml.Create(Server, ApiId, Timeout, server.Port);
                naxml.QueryXML(QueryIp, "3", TxnId);
            }
        }

        [Fact]
        public void QueryXML_ErrorResponse_ThrowsNetAcuityExceptionWithMessage()
        {
            using (var server = new MockNetAcuityServer())
            {
                string errorXml = "<response trans-id=\"" + TxnId + "\" ip=\"" + QueryIp
                                + "\" error=\"DB Not Loaded\" />";
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(errorXml);

                var naxml = new NetAcuityXML();
                naxml.Create(Server, ApiId, Timeout, server.Port);

                var ex = Assert.Throws<NetAcuityException>(() => naxml.QueryXML(QueryIp, "3", TxnId));
                Assert.Equal("DB Not Loaded", ex.Message);
            }
        }

        // ── FieldValue ────────────────────────────────────────────────────────

        [Fact]
        public void FieldValue_AfterSuccessfulQuery_ReturnsAttributeValue()
        {
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(
                    SimpleXml("geo-country=\"can\" "));

                var naxml = new NetAcuityXML();
                naxml.Create(Server, ApiId, Timeout, server.Port);
                naxml.QueryXML(QueryIp, "3", TxnId);

                Assert.Equal("can", naxml.FieldValue("geo-country"));
            }
        }

        [Fact]
        public void FieldValue_ForMissingField_ReturnsNull()
        {
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(SimpleXml());

                var naxml = new NetAcuityXML();
                naxml.Create(Server, ApiId, Timeout, server.Port);
                naxml.QueryXML(QueryIp, "3", TxnId);

                Assert.Null(naxml.FieldValue("no-such-field"));
            }
        }

        // ── ResponseFields ────────────────────────────────────────────────────

        [Fact]
        public void ResponseFields_AfterQuery_ContainsAllAttributes()
        {
            string xml = SimpleXml("isp-name=\"verizon\" isp-org=\"verizon business\" ");
            using (var server = new MockNetAcuityServer())
            {
                server.ResponseHandler = _ => MockNetAcuityServer.BuildXmlResponse(xml);

                var naxml = new NetAcuityXML();
                naxml.Create(Server, ApiId, Timeout, server.Port);
                naxml.QueryXML(QueryIp, "8", TxnId);

                var fields = naxml.ResponseFields();
                Assert.True(fields.ContainsKey("isp-name"));
                Assert.Equal("verizon", fields["isp-name"]);
            }
        }
    }
}
