using System;
using System.Net.Sockets;

namespace kukuProxy
{
    public class ProxyInfo
    {
        public TcpClient proxy,client;
        public byte[] up_buf;
        public byte[] down_buf;
    }
}
