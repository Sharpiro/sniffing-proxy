using System.IO;
using System.Net.Sockets;

namespace SniffingProxy.Core
{
    public class IdStream
    {
        public int Id { get; set; }
        public Stream Stream { get; set; }
    }

    public static class IdStreamExtension
    {
        public static IdStream AsIdStream(this Stream networkStream, int id)
        {
            return new IdStream
            {
                Id = id,
                Stream = networkStream
            };
        }

        public static IdStream GetIdStream(this TcpClient tcpClient, int id) => tcpClient.GetStream().AsIdStream(id);
    }
}
