using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SniffingProxy
{
    public class ContentLengthService
    {
        public async Task<byte[]> ParseByContentLength(Stream sourceStream, int bufferSize, int remainingBytes)
        {
            var buffer = new byte[bufferSize];
            var allBytes = Enumerable.Empty<byte>();
            var allBytesRead = 0;
            while (allBytesRead < remainingBytes)
            {
                var bytesRead = await sourceStream.ReadAsync(buffer);
                allBytesRead += bytesRead;
                allBytes = allBytes.Concat(buffer.AsSpan(0, bytesRead).ToArray());
            }

            return allBytes.ToArray();
        }
    }
}