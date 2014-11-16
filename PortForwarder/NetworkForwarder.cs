using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace PortForwarder
{
    public class NetworkForwarder
    {
        private ForwardEntry entry;
        TcpListener listener;
        List<TcpClientPair> clients = new List<TcpClientPair>();


        public NetworkForwarder(ForwardEntry entry)
        {
            this.entry = entry;
        }

        public void Start()
        {
            listener = new TcpListener(IPAddress.IPv6Any, entry.sourcePort);
            //Listen on both IPv4 and IPv6
            if (Environment.OSVersion.Platform == PlatformID.Win32NT && Socket.OSSupportsIPv6)
            {
                Console.WriteLine("Enabling support for IPv6");
                listener.Server.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, 0);
            }
            listener.Start();
            listener.BeginAcceptTcpClient(AcceptNewConnection, null);
        }

        public void Stop()
        {
            listener.Stop();
        }

        private void AcceptNewConnection(IAsyncResult ar)
        {
            TcpClient newClient = listener.EndAcceptTcpClient(ar);
            listener.BeginAcceptTcpClient(AcceptNewConnection, null);
            Console.WriteLine("New connection from " + newClient.Client.RemoteEndPoint);
            StartClient(newClient);

        }

        private void StartClient(TcpClient client)
        {
            TcpClientPair pair = new TcpClientPair();
            pair.localConnection = client;
            pair.remoteConnection = new TcpClient();
            pair.remoteConnection.BeginConnect(entry.destinationIP, entry.destinationPort, StartClientCallback, pair);
        }

        private void StartClientCallback(IAsyncResult ar)
        {
            TcpClientPair pair = (TcpClientPair)ar.AsyncState;
            if (!ar.AsyncWaitHandle.WaitOne(5000))
            {
                Console.WriteLine("Failed to connect to remote host, closing connections");
                try
                {
                    pair.localConnection.GetStream().Close();
                    pair.localConnection.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error disconnecting client, Exception: " + e.Message);
                }
                return;
            }
            try
            {
                pair.remoteConnection.EndConnect(ar);
                if (pair.localConnection.Connected && pair.remoteConnection.Connected)
                {
                    pair.localEndpoint = pair.remoteConnection.Client.LocalEndPoint;
                    pair.remoteEndpoint = pair.remoteConnection.Client.RemoteEndPoint;
                    Console.WriteLine("Connected " + pair.localEndpoint + " to " + pair.remoteEndpoint);
                    lock (clients)
                    {
                        clients.Add(pair);
                    }
                    StartForwarding(pair);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error connecting to remote host, Exception: " + e);
            }
        }

        private void StartForwarding(TcpClientPair pair)
        {
            pair.localConnection.Client.NoDelay = true;
            pair.remoteConnection.Client.NoDelay = true;
            BeginLocalReceive(pair);
            BeginRemoteReceive(pair);
            Console.WriteLine("Forwarding change " + pair.localEndpoint + " <---> " + pair.remoteEndpoint);
        }

        //Local loop
        private void BeginLocalReceive(TcpClientPair pair)
        {
            try
            {
                pair.localConnection.GetStream().BeginRead(pair.localBuffer, 0, pair.localBuffer.Length, LocalReceiveCallback, pair);
            }
            catch
            {
                DisconnectPair(pair);
            }
        }

        private void LocalReceiveCallback(IAsyncResult ar)
        {
            TcpClientPair pair = (TcpClientPair)ar.AsyncState;
            try
            {
                int readBytes = pair.localConnection.GetStream().EndRead(ar);
                if (readBytes > 0)
                {
                    pair.remoteConnection.GetStream().Write(pair.localBuffer, 0, readBytes);
                }
                pair.localConnection.GetStream().BeginRead(pair.localBuffer, 0, pair.localBuffer.Length, LocalReceiveCallback, pair);
            }
            catch
            {
                DisconnectPair(pair);
            }
        }

        //Remote loop
        private void BeginRemoteReceive(TcpClientPair pair)
        {
            try
            {
                pair.remoteConnection.GetStream().BeginRead(pair.remoteBuffer, 0, pair.remoteBuffer.Length, RemoteReceiveCallback, pair);
            }
            catch
            {
                DisconnectPair(pair);
            }
        }

        private void RemoteReceiveCallback(IAsyncResult ar)
        {
            TcpClientPair pair = (TcpClientPair)ar.AsyncState;
            try
            {
                int readBytes = pair.remoteConnection.GetStream().EndRead(ar);
                if (readBytes > 0)
                {
                    pair.localConnection.GetStream().Write(pair.remoteBuffer, 0, readBytes);
                }
                pair.remoteConnection.GetStream().BeginRead(pair.remoteBuffer, 0, pair.remoteBuffer.Length, RemoteReceiveCallback, pair);
            }
            catch
            {
                DisconnectPair(pair);
            }
        }

        private void DisconnectPair(TcpClientPair pair)
        {
            bool disconnect = false;
            lock (clients)
            {
                if (clients.Contains(pair))
                {
                    disconnect = true;
                    clients.Remove(pair);
                }
            }
            if (disconnect)
            {
                Console.WriteLine("Forwarding change " + pair.localEndpoint + " <-X-> " + pair.remoteEndpoint);
                try
                {
                    pair.localConnection.GetStream().Close();
                    pair.localConnection.Close();
                }
                catch
                {
                    //Don't care
                }
                try
                {
                    pair.remoteConnection.GetStream().Close();
                    pair.remoteConnection.Close();
                }
                catch
                {
                    //Don't care
                }
            }
        }
    }

    public class TcpClientPair
    {
        public TcpClient localConnection;
        public EndPoint localEndpoint;
        public byte[] localBuffer = new byte[1024];
        public TcpClient remoteConnection;
        public byte[] remoteBuffer = new byte[1024];
        public EndPoint remoteEndpoint;
    }
}

