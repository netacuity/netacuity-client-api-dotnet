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
using System.Security.Cryptography;

namespace NetAcuity
{
    class Program
    {
        private static void PrintUsage()
        {
            Console.WriteLine("Usage: dotnet run --project examples/XmlQuery -- <serverIP> <queryIP> <featureCodes>");
            Console.WriteLine("  - serverIP: IP address of the NetAcuity Server");
            Console.WriteLine("  - queryIP: IP address to query");
            Console.WriteLine("  - featureCodes: comma-separated list of NetAcuity database feature-codes");
            Console.WriteLine("e.g.: dotnet run --project examples/XmlQuery -- 127.0.0.1 203.0.113.5 26,33,35,93");
            return;
        }

        public static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                PrintUsage();
                return 1;
            }

            string serverIp = args[0];
            string queryIp = args[1];
            string featureCodes = args[2];
            int exampleApiID = 74;
            int timeoutMicroseconds = 3000000;  // 3 seconds

            // A predictable transaction ID would let an attacker forge a response that
            // passes the transaction-id echo check, so this uses a CSPRNG rather than
            // System.Random.
            byte[] randomBytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            int transactionIdValue = (BitConverter.ToInt32(randomBytes, 0) & int.MaxValue) % 1000000000;
            string transactionId = transactionIdValue.ToString();

            try
            {
                NaXMLQuery xmlQuery = new NaXMLQuery(serverIp, exampleApiID, timeoutMicroseconds);
                xmlQuery.QueryAndParse(queryIp, featureCodes, transactionId);

                var fields = xmlQuery.GetResponseFields();
                Console.WriteLine("ip = " + fields["ip"]);
                Console.WriteLine("trans-id = " + fields["trans-id"]);
                foreach (var field in fields)
                {
                    if (field.Key == "ip" || field.Key == "trans-id")
                    {
                        continue;
                    }
                    Console.WriteLine(field.Key + " = " + field.Value);
                }
                Console.WriteLine("raw-response = " + xmlQuery.ResponseXML);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
                return 1;
            }
            return 0;
        }

    }
}
