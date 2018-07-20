using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace SniffingProxy.Core
{
    public class ClientInfo : IDisposable
    {
        public int Id { get; set; }
        public Stream Stream { get; set; }
        public TcpClient TcpClient { get; set; }
        public IPEndPoint Local => (IPEndPoint)TcpClient.Client.LocalEndPoint;
        public IPEndPoint Remote => (IPEndPoint)TcpClient.Client.RemoteEndPoint;

        public void Dispose()
        {
            TcpClient?.Dispose();
            Stream?.Dispose();
        }
    }

    public static class IdStreamExtension
    {
        public static ClientInfo AsClientInfo(this Stream stream, int id)
        {
            return new ClientInfo
            {
                Id = id,
                Stream = stream
            };
        }

        public static ClientInfo AsClientInfo(this TcpClient tcpClient, int id = -1)
        {
            return new ClientInfo
            {
                Id = id,
                Stream = tcpClient.GetStream(),
                TcpClient = tcpClient
            };
        }
    }
}
