using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace kukuProxy
{
    class Server
    {
        public delegate void OutputMethod(string str); //主程序给过来的用于输出字符串的方法

        private TcpListener Tcp;
        private int BufferLen = 1024;
        public int Port { get; set; }
        private bool IsStop = false;

        public OutputMethod OutputFunc = null; //在主程序设置这个， 就有输出，不设置就没输出

        public Server(int port = 8080, OutputMethod output = null)
        {
            this.Port = port;
            this.OutputFunc = output;
        }
        public void Output(string str)
        {
            if (this.OutputFunc != null)
            {
                this.OutputFunc(str);
            }
        }
        public void Debug(byte[] arr,int len=-1,string title="")
        {
            //调试的时候用来输出调试信息，调试完就可以直接return掉
            return;
            if (len == 0) return;

            string str = "";
            int count = len;
            if (len == -1) count = arr.Length;
            byte b;
            for (int i=0;i<len;i++)
            {
                b = arr[i];
                str += String.Format("{0:X2} ", b);
            }
            Output(title+":"+str);
        }
        public void Start()
        {
            this.IsStop = false;
            Output("服务启动，端口"+Port.ToString());
            this.Tcp = new TcpListener(IPAddress.Any, this.Port);
            this.Tcp.Start();
            this.Tcp.BeginAcceptTcpClient(OnAccept, this.Tcp);
        }
        public void Stop()
        {
            this.IsStop = true;
            Output("停止服务");
            Tcp.Stop();
        }
        //accept到一个连接，就丢给OnClientData处理，然后再开始等待下一个连接
        private void OnAccept(IAsyncResult async)
        {
            try {
                if (IsStop || async == null) return;
                TcpClient client = (async.AsyncState as TcpListener).EndAcceptTcpClient(async);
                Thread myThread = new Thread(new ParameterizedThreadStart(OnConnect));

                Output("有客户端连接" + client.Client.RemoteEndPoint.ToString());
                myThread.Start(client);
                this.Tcp.BeginAcceptTcpClient(OnAccept, this.Tcp);
            } finally{

            }
        }
        private async void OnConnect(object _client)
        {
            if (IsStop) return;
            TcpClient client = _client as TcpClient;
            try
            {
                byte[] up_buf = new byte[BufferLen];
                byte[] down_buf = new byte[BufferLen];
                NetworkStream stream = client.GetStream();

                //====================================================================
                //连上的第一件事情就是先处理握手包，我刚意识到这不需要丢到循环里面啊
                int num = await stream.ReadAsync(up_buf, 0, 2); //读2字节客户端发上来的东西，第一字节是协议，第二字节是下个包的长度
                //Debug(up_buf, num, "1");
                if (num != 2) return;

                int packlen = 0;
                packlen = up_buf[1];
                num = await stream.ReadAsync(up_buf, 0, packlen);
                //Debug(up_buf, num, "2");

                //回应握手包
                down_buf[0] = 0x05;
                down_buf[1] = 0x00;//回复说不需要认证

                await stream.WriteAsync(down_buf, 0, 2);
                stream.Flush();
                //====================================================================


                //====================================================================
                //然后就是收目标请求信息并回复
                //先收个4字节的包头
                IPAddress ip = null;
                num = stream.Read(up_buf, 0, 4);
                //Debug(up_buf, num, "3");
                if (num <= 0)
                {
                    return;
                }
                //如果目标是个ip
                if (up_buf[3] == 1 || up_buf[3] == 4)
                {
                    //是个IPV6有16字节，ipv4是4字节
                    int iplen = (up_buf[3] == 1) ? 4 : 16;
                    byte[] iparr = new byte[iplen];
                    num = stream.Read(iparr, 0, iplen);
                    if (num <= 0)
                    {
                        stream.Close();
                        client.Close();
                        return;
                    }
                    ip = new IPAddress(iparr);
                    Output(client.Client.RemoteEndPoint.ToString() + "请求ip:" + ip.ToString());
                }
                //是个域名
                else if (up_buf[3] == 3)
                {
                    num = stream.Read(up_buf, 0, 1);
                    int urllen = up_buf[0];
                    num = stream.Read(up_buf, 0, urllen);
                    string url = Encoding.ASCII.GetString(up_buf);
                    //域名反查ip
                    IPAddress[] iplist = Dns.GetHostAddresses(url);
                    if (iplist.Length == 0)
                    {
                        stream.Close();
                        client.Close();
                        return;
                    }
                    ip = iplist[0];
                    Output(client.Client.RemoteEndPoint.ToString() + "请求域名:" + url);
                }
                //协议之外的就不知道是个什么鬼了
                else
                {
                    stream.Close();
                    client.Close();
                    return;
                }
                //收端口号
                int port = 0;
                num = stream.Read(up_buf, 0, 2);
                byte[] portArr = { up_buf[1], up_buf[0] };//反转字节序
                port = BitConverter.ToUInt16(portArr, 0);

                Output("目标:" + ip.ToString() + ":" + port.ToString());

                //回复客户端,完成握手
                byte[] myip = ((IPEndPoint)this.Tcp.LocalEndpoint).Address.GetAddressBytes();
                byte[] myport = BitConverter.GetBytes((ushort)this.Port);
                down_buf[0] = 5;
                down_buf[1] = 0;
                down_buf[2] = 0;
                down_buf[3] = 1;
                myip.CopyTo(down_buf, 4);
                myport.CopyTo(down_buf, 8);
                await stream.WriteAsync(down_buf, 0, 10);
                //====================================================================


                //====================================================================
                //握手完毕就是正常的收发数据包了
                TcpClient proxy = new TcpClient();
                proxy.Connect(ip, port);//连上真实目标（握手包里得到的ip和端口）

                //用事件来做比较我之前写的while更科学点
                //此处需要写个结构体放proxy和client，因为我希望同时传递proxy和client的socket对象，数据接收事件却只能传递一个参数
                ProxyInfo pi = new ProxyInfo();
                pi.proxy = proxy;
                pi.client = client;
                pi.up_buf = new byte[BufferLen];
                pi.down_buf = new byte[BufferLen];

                proxy.Client.BeginReceive(pi.down_buf, 0, pi.down_buf.Length, SocketFlags.None, this.OnProxyData, pi);
                client.Client.BeginReceive(pi.up_buf, 0, pi.up_buf.Length, SocketFlags.None, this.OnClientData, pi);
            }catch (Exception ex) {
                Output(ex.ToString());
            }
        }


        //下面俩方法就是双向转发了
        //真实服务器有数据来就触发这里
        private void OnProxyData(IAsyncResult result)
        {
            if (IsStop) return;
            try
            {
                ProxyInfo pi = result.AsyncState as ProxyInfo;
                SocketError error;
                int size = pi.proxy.Client.EndReceive(result, out error);
                if (size > 0)
                {
                    //转发给客户端
                    NetworkStream stream = pi.client.GetStream();
                    stream.Write(pi.down_buf,0,size);
                    //继续等待下一个数据包
                    pi.proxy.Client.BeginReceive(pi.down_buf, 0, pi.down_buf.Length, SocketFlags.None, this.OnProxyData, pi);

                }
            }
            catch
            {
            }
        }
        //客户机有数据来就触发这里,其实就是跟上面的一样套路，只是反过来
        private void OnClientData(IAsyncResult result)
        {
            if (IsStop) return;
            try
            {
                ProxyInfo pi = result.AsyncState as ProxyInfo;
                SocketError error;
                int size = pi.client.Client.EndReceive(result, out error);
                if (size > 0)
                {
                    //转发给远程真实服务器
                    NetworkStream stream = pi.proxy.GetStream();
                    stream.Write(pi.up_buf, 0, size);
                    //继续等待下一个数据包
                    pi.client.Client.BeginReceive(pi.up_buf, 0, pi.up_buf.Length, SocketFlags.None, this.OnClientData, pi);
                }
            }
            catch
            {
            }
        }
    }
}
