using System;
using System.Collections.Generic;
using System.Linq;
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
            // if host is ip, parse it and connect
            if (IPAddress.TryParse(host, out ip)) 
            {
                await tcpClient.ConnectAsync(ip, port);
            }
            else
            {
                // not ip, resolve it and get IP(s)
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(host);
                // tcpClient is not compatible for ipv6(in InitSocket()), so filter it
                // or consider changing InitSocket() to:
                // client = new TcpClient(AddressFamily.InterNetworkV6);
                // client.Client.DualMode = true;
                // it can support both ipv4 and ipv6, but it will cause delaying the time to establish the connection if the host is not listening on the IPv6 address
                // https://technet.microsoft.com/en/library/hh138320(v=vs.110)
                addresses = addresses.Where(p => p.AddressFamily == AddressFamily.InterNetwork).ToArray();

                if (addresses.Length > 0)
                {
                    // if dns port to multiple IPs, random an ip to connect
                    int rand = (int)(DateTime.UtcNow.Ticks % addresses.Length); 
                    IPAddress address = addresses[rand];
                    await tcpClient.ConnectAsync(address, port);
                }
                else
                    throw new TTransportException(TTransportException.ExceptionType.NotOpen, $"Can't resolve host({host}) to a ipv4's ip");
            }
#else
            await client.ConnectAsync(host, port);
#endif
        }
    }
}
