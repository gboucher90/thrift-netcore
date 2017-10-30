using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Thrift.Transport
{
    public static class TcpClientExtensions
    {
        public static async Task ClientConnectAsync(this TcpClient tcpClient, string host, int port)
        {
#if NETSTANDARD1_4 || NETSTANDARD1_5
            IPAddress ip;
            if (IPAddress.TryParse(host, out ip)) // if host is ip, parse it and connect
            {
                await tcpClient.ConnectAsync(ip, port);
            }
            else
            {
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(host);// not ip, resolve it and get IP(s)
                
                if (addresses.Length > 0)
                {
                    int rand = (int)(DateTime.Now.Ticks % addresses.Length); // if dns port to multiple IPs, random an ip to connect
                    IPAddress address = addresses[rand];
                    if (address.ToString() == "::1")
                        address = IPAddress.Parse("127.0.0.1");
                    await tcpClient.ConnectAsync(address, port);
                }
                else
                    throw new TTransportException(TTransportException.ExceptionType.NotOpen, $"Can't resolve host({host})");
            }
#else
            await client.ConnectAsync(host, port);
#endif
        }
    }
}
