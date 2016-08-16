/**
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements. See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership. The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied. See the License for the
 * specific language governing permissions and limitations
 * under the License.
 *
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Thrift.Transport
{

    public class THttpClient : TTransport
    {
        private readonly Uri _uri;
        private readonly X509Certificate[] _certificates;
        private Stream _inputStream;
        private MemoryStream _outputStream = new MemoryStream();

        // Timeouts in milliseconds
        private int _connectTimeout = 30000;

        private int _readTimeout = 30000;

        private readonly IDictionary<string, string> _customHeaders = new Dictionary<string, string>();

        private IWebProxy _proxy = WebRequest.DefaultWebProxy;

        public THttpClient(Uri u)
            : this(u, Enumerable.Empty<X509Certificate>())
        {
        }

        public THttpClient(Uri u, IEnumerable<X509Certificate> certificates)
        {
            _uri = u;
            this._certificates = (certificates ?? Enumerable.Empty<X509Certificate>()).ToArray();
        }

        public int ConnectTimeout
        {
            set
            {
               _connectTimeout = value;
            }
        }

        public int ReadTimeout
        {
            set
            {
                _readTimeout = value;
            }
        }

        public IDictionary<string, string> CustomHeaders
        {
            get
            {
                return _customHeaders;
            }
        }

        public IWebProxy Proxy
        {
            set
            {
                _proxy = value;
            }
        }

        public override bool IsOpen
        {
            get
            {
                return true;
            }
        }

        public override void Open()
        {
        }

        public override void Close()
        {
            if (_inputStream != null)
            {
                _inputStream.Dispose();
                _inputStream = null;
            }
            if (_outputStream != null)
            {
                _outputStream.Dispose();
                _outputStream = null;
            }
        }

        public override int Read(byte[] buf, int off, int len)
        {
            if (_inputStream == null)
            {
                throw new TTransportException(TTransportException.ExceptionType.NotOpen, "No request has been sent");
            }

            try
            {
                int ret = _inputStream.Read(buf, off, len);

                if (ret == -1)
                {
                    throw new TTransportException(TTransportException.ExceptionType.EndOfFile, "No more data available");
                }

                return ret;
            }
            catch (IOException iox)
            {
                throw new TTransportException(TTransportException.ExceptionType.Unknown, iox.ToString());
            }
        }

        public override void Write(byte[] buf, int off, int len)
        {
            _outputStream.Write(buf, off, len);
        }

        public override void Flush()
        {
            try
            {
                SendRequest();
            }
            finally
            {
                _outputStream = new MemoryStream();
            }
        }

        private void SendRequest()
        {
            try
            {
                HttpWebRequest connection = CreateRequest();

                byte[] data = _outputStream.ToArray();

#if NET_CORE
                connection.Headers[HttpRequestHeader.ContentLength] = data.Length.ToString();
#else
                connection.ContentLength = data.Length;
#endif

#if NET_CORE
                using (Stream requestStream = connection.GetRequestStreamAsync().Result)
#else
                using (Stream requestStream = connection.GetRequestStream())
#endif
                {
                    requestStream.Write(data, 0, data.Length);

                    // Resolve HTTP hang that can happens after successive calls by making sure
                    // that we release the response and response stream. To support this, we copy
                    // the response to a memory stream.
#if NET_CORE
                    using (var response = connection.GetResponseAsync().Result)
#else
                    using (var response = connection.GetResponse())
#endif
                    {
                        using (var responseStream = response.GetResponseStream())
                        {
                            // Copy the response to a memory stream so that we can
                            // cleanly close the response and response stream.
                            _inputStream = new MemoryStream();
                            byte[] buffer = new byte[8096];
                            int bytesRead;
                            while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                _inputStream.Write(buffer, 0, bytesRead);
                            }
                            _inputStream.Seek(0, 0);
                        }
                    }
                }
            }
            catch (IOException iox)
            {
                throw new TTransportException(TTransportException.ExceptionType.Unknown, iox.ToString());
            }
            catch (WebException wx)
            {
                throw new TTransportException(TTransportException.ExceptionType.Unknown, "Couldn't connect to server: " + wx);
            }
        }

        private HttpWebRequest CreateRequest()
        {
            HttpWebRequest connection = (HttpWebRequest)WebRequest.Create(_uri);


#if !NET_CORE
            connection.ClientCertificates.AddRange(_certificates);

            if (_connectTimeout > 0)
            {
                connection.Timeout = _connectTimeout;
            }
            if (_readTimeout > 0)
            {
                connection.ReadWriteTimeout = _readTimeout;
            }
#endif
            // Make the request
            connection.ContentType = "application/x-thrift";
            connection.Accept = "application/x-thrift";
#if NET_CORE
            connection.Headers[HttpRequestHeader.UserAgent] = "C#/THttpClient";
#else
            connection.UserAgent = "C#/THttpClient";
#endif
            connection.Method = "POST";
#if !NET_CORE
            connection.ProtocolVersion = HttpVersion.Version10;
#endif

            //add custom headers here
            foreach (KeyValuePair<string, string> item in _customHeaders)
            {
#if !NET_CORE
                connection.Headers.Add(item.Key, item.Value);
#else
                connection.Headers[item.Key] = item.Value;
#endif
            }

            connection.Proxy = _proxy;

            return connection;
        }

        public override IAsyncResult BeginFlush(AsyncCallback callback, object state)
        {
            // Extract request and reset buffer
            var data = _outputStream.ToArray();

            //requestBuffer_ = new MemoryStream();

            try
            {
                // Create connection object
                var flushAsyncResult = new FlushAsyncResult(callback, state);
                flushAsyncResult.Connection = CreateRequest();

                flushAsyncResult.Data = data;


                flushAsyncResult.Connection.BeginGetRequestStream(GetRequestStreamCallback, flushAsyncResult);
                return flushAsyncResult;

            }
            catch (IOException iox)
            {
                throw new TTransportException(iox.ToString());
            }
        }

        public override void EndFlush(IAsyncResult asyncResult)
        {
            try
            {
                var flushAsyncResult = (FlushAsyncResult) asyncResult;

                if (!flushAsyncResult.IsCompleted)
                {
                    var waitHandle = flushAsyncResult.AsyncWaitHandle;
                    waitHandle.WaitOne();  // blocking INFINITEly
#if NET_CORE
                    waitHandle.Dispose();
#else
                    waitHandle.Close();
#endif
                }

                if (flushAsyncResult.AsyncException != null)
                {
                    throw flushAsyncResult.AsyncException;
                }
            } finally
            {
                _outputStream = new MemoryStream();
            }

        }

        private void GetRequestStreamCallback(IAsyncResult asynchronousResult)
        {
            var flushAsyncResult = (FlushAsyncResult)asynchronousResult.AsyncState;
            try
            {
                var reqStream = flushAsyncResult.Connection.EndGetRequestStream(asynchronousResult);
                reqStream.Write(flushAsyncResult.Data, 0, flushAsyncResult.Data.Length);
                reqStream.Flush();
                reqStream.Dispose();

                // Start the asynchronous operation to get the response
                flushAsyncResult.Connection.BeginGetResponse(GetResponseCallback, flushAsyncResult);
            }
            catch (Exception exception)
            {
                flushAsyncResult.AsyncException = new TTransportException(exception.ToString());
                flushAsyncResult.UpdateStatusToComplete();
                flushAsyncResult.NotifyCallbackWhenAvailable();
            }
        }

        private void GetResponseCallback(IAsyncResult asynchronousResult)
        {
            var flushAsyncResult = (FlushAsyncResult)asynchronousResult.AsyncState;
            try
            {
                _inputStream = flushAsyncResult.Connection.EndGetResponse(asynchronousResult).GetResponseStream();
            }
            catch (Exception exception)
            {
                flushAsyncResult.AsyncException = new TTransportException(exception.ToString());
            }
            flushAsyncResult.UpdateStatusToComplete();
            flushAsyncResult.NotifyCallbackWhenAvailable();
        }

        // Based on http://msmvps.com/blogs/luisabreu/archive/2009/06/15/multithreading-implementing-the-iasyncresult-interface.aspx
        class FlushAsyncResult : IAsyncResult
        {
            private volatile bool _isCompleted;
            private ManualResetEvent _evt;
            private readonly AsyncCallback _cbMethod;
            private readonly object _state;

            public FlushAsyncResult(AsyncCallback cbMethod, object state)
            {
                _cbMethod = cbMethod;
                _state = state;
            }

            internal byte[] Data { get; set; }
            internal HttpWebRequest Connection { get; set; }
            internal TTransportException AsyncException { get; set; }

            public object AsyncState
            {
                get { return _state; }
            }
            public WaitHandle AsyncWaitHandle
            {
                get { return GetEvtHandle(); }
            }
            public bool CompletedSynchronously
            {
                get { return false; }
            }
            public bool IsCompleted
            {
                get { return _isCompleted; }
            }
            private readonly object _locker = new object();
            private ManualResetEvent GetEvtHandle()
            {
                lock (_locker)
                {
                    if (_evt == null)
                    {
                        _evt = new ManualResetEvent(false);
                    }
                    if (_isCompleted)
                    {
                        _evt.Set();
                    }
                }
                return _evt;
            }
            internal void UpdateStatusToComplete()
            {
                _isCompleted = true; //1. set _iscompleted to true
                lock (_locker)
                {
                    if (_evt != null)
                    {
                        _evt.Set(); //2. set the event, when it exists
                    }
                }
            }

            internal void NotifyCallbackWhenAvailable()
            {
                if (_cbMethod != null)
                {
                    _cbMethod(this);
                }
            }
        }

#region " IDisposable Support "
        private bool _IsDisposed;

        // IDisposable
        protected override void Dispose(bool disposing)
        {
            if (!_IsDisposed)
            {
                if (disposing)
                {
                    if (_inputStream != null)
                        _inputStream.Dispose();
                    if (_outputStream != null)
                        _outputStream.Dispose();
                }
            }
            _IsDisposed = true;
        }
#endregion
    }
}
