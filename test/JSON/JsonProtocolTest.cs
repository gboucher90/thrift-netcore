/**
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements. See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership. The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied. See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using System;
using System.IO;
using NUnit.Framework;
using Thrift.Protocol;
using Thrift.Transport;

namespace JSONTest
{
    [TestFixture]
    internal class JsonProtocolTest
    {
        [Test]
        [Description("JSON binary decodes too much data")]
        public void TestThrift2365()
        {
            var rnd = new Random();
            for (var len = 0; len < 10; ++len)
            {
                var dataWritten = new byte[len];
                rnd.NextBytes(dataWritten);

                Stream stm = new MemoryStream();
                TTransport trans = new TStreamTransport(null, stm);
                TProtocol prot = new TJSONProtocol(trans);
                prot.WriteBinary(dataWritten);

                stm.Position = 0;
                trans = new TStreamTransport(stm, null);
                prot = new TJSONProtocol(trans);
                byte[] dataRead = prot.ReadBinary();

                CollectionAssert.AreEqual(dataWritten, dataRead);
            }
        }

        [Test]
        [Description(@"hex encoding using \uXXXX where 0xXXXX > 0xFF")]
        public void TestThrift2336()
        {
            const string russianText = "\u0420\u0443\u0441\u0441\u043a\u043e\u0435 \u041d\u0430\u0437\u0432\u0430\u043d\u0438\u0435";
            const string russianJson = "\"\\u0420\\u0443\\u0441\\u0441\\u043a\\u043e\\u0435 \\u041d\\u0430\\u0437\\u0432\\u0430\\u043d\\u0438\\u0435\"";

            // prepare buffer with JSON data
            var rawBytes = new byte[russianJson.Length];
            for (var i = 0; i < russianJson.Length; ++i)
                rawBytes[i] = (byte)(russianJson[i] & (char)0xFF);  // only low bytes

            // parse and check
            var stm = new MemoryStream(rawBytes);
            var trans = new TStreamTransport(stm, null);
            var prot = new TJSONProtocol(trans);
            Assert.AreEqual(russianText, prot.ReadString(), "reading JSON with hex-encoded chars > 8 bit");
        }
    }
}
