using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SniffingProxy.Core
{
    public class HeadersService
    {
        public async Task<byte[]> ReceiveUpToHeaders(Stream sourceStream, int bufferSize, CancellationToken cancellationToken)
        {
            var buffer = new byte[bufferSize];
            var index = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await sourceStream.ReadAsync(buffer, index, 1);
                if (index >= 4 && buffer[index - 3] == 13 && buffer[index - 2] == 10
                    && buffer[index - 1] == 13 && buffer[index] == 10)
                {
                    break;
                }
                index++;
            }

            var allBytesBuffer = buffer.AsSpan(0, index + 1).ToArray();
            var temp = Encoding.UTF8.GetString(allBytesBuffer);
            return allBytesBuffer;
        }

        // public async Task<byte[]> ReceiveUpToHeaders(Stream sourceStream, int bufferSize, CancellationToken cancellationToken)
        // {
        //     var buffer = new byte[bufferSize];
        //     var allBytesEnumerable = Enumerable.Empty<byte>();
        //     int endOfHeadersIndex;
        //     while (!cancellationToken.IsCancellationRequested)
        //     {
        //         var bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken);
        //         allBytesEnumerable = allBytesEnumerable.Concat(buffer.AsSpan(0, bytesRead).ToArray());

        //         // check for \r\n\r\n
        //         if (bytesRead < 4) continue;
        //         endOfHeadersIndex = buffer.AsSpan(0, bytesRead).IndexOf(_doubleCRLFBuffer);
        //         if (endOfHeadersIndex >= 0) break;
        //     }

        //     var allBytesBuffer = allBytesEnumerable.ToArray();
        //     return allBytesBuffer;
        // }
    }
}
