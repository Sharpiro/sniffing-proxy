using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SniffingProxy
{
    public class EncodingService
    {
        private static byte[] _doubleCRLFBuffer = new byte[] { 13, 10, 13, 10 };
        private static byte[] _CRLFBuffer = new byte[] { 13, 10 };

        public async Task<object> TransferEncodingOld(Stream sourceStream, int bufferSize, Memory<byte> startOfBody = default)
        {
            if (startOfBody.Length != 0) throw new NotImplementedException("data with partial body not yet supported...");

            var buffer = new byte[bufferSize];
            var allBytes = Enumerable.Empty<byte>();
            var allBytesRead = 0;

            (int chunkStart, int chunkSize) = await PeekChunkSize();
            while (allBytesRead < chunkSize)
            {
                var bytesRead = await sourceStream.ReadAsync(buffer);
                allBytesRead += bytesRead;
                var slice = buffer.AsMemory(0, bytesRead);
                var tempText = Encoding.UTF8.GetString(slice.Span);
                allBytes = allBytes.Concat(slice.Span.ToArray());
            }

            var allBytesBuffer = allBytes.ToArray();
            var subslice = allBytesBuffer.AsMemory(chunkStart + chunkSize, 10);
            var subText = Encoding.UTF8.GetString(subslice.Span);
            var allTempText = Encoding.UTF8.GetString(allBytesBuffer);

            throw new NotImplementedException();

            async Task<(int start, int length)> PeekChunkSize()
            {
                var bytesRead = await sourceStream.ReadAsync(buffer);
                allBytesRead += bytesRead;
                var slice = buffer.AsMemory(0, bytesRead);
                allBytes = allBytes.Concat(slice.ToArray());
                var endOfNumberIndex = slice.Span.IndexOf(_CRLFBuffer);
                var chunkSlice = slice.Slice(0, endOfNumberIndex);
                var result = Utf8Parser.TryParse(chunkSlice.Span, out int length, out int bytesConsumed, 'x');
                return (bytesConsumed + _CRLFBuffer.Length * 2, length);
            }
        }

        public async Task<byte[]> TransferEncodingHard(Stream sourceStream, int bufferSize, Memory<byte> startOfBody = default)
        {
            const int zeroDataChunkPostfix = 5;
            var buffer = new byte[bufferSize];
            // IEnumerable<byte> allBytes;
            var backupStartOfBody = Encoding.UTF8.GetString(startOfBody.Span);

            if (startOfBody.Length == 0)
            {
                var bytesRead = await sourceStream.ReadAsync(buffer);
                startOfBody = buffer.AsMemory(0, bytesRead);
                var initalText = Encoding.UTF8.GetString(startOfBody.Span);

            }

            (var chunkStart, var chunkLength, var throwawayBytes) = PeekChunk(startOfBody.Span);
            var totalBytesRead = startOfBody.Length - chunkStart;
            // var totalBytesRead = startOfBody.Length;
            // var totalBytesToRead = (chunkLength - totalBytesRead) + 500;

            IEnumerable<byte> allBytes = startOfBody.ToArray();
            // IEnumerable<byte> contentBytes = startOfBody.Slice(chunkStart).ToArray();
            // int remainingBytes;
            // while ((remainingBytes = chunkLength + throwawayBytes - totalChunkRead) > 0)
            // while (totalChunkRead < chunkLength + throwawayBytes)
            var count = 0;
            while (true)
            {
                // var remainingBytes = chunkLength + throwawayBytes - totalBytesRead;
                var bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length);
                totalBytesRead += bytesRead;
                Debug.WriteLine($"Received {bytesRead} bytes: '{totalBytesRead}/{chunkLength}' @ count '{++count}'");
                //if (count == 9 && bytesRead >= 8190 && bytesRead <= 8299)
                //{
                //    var tempText = Encoding.UTF8.GetString(buffer.AsSpan(0, bytesRead));
                //}
                //if (bytesRead >= 5450 && bytesRead <= 5459)
                //{

                //}
                if (totalBytesRead >= chunkLength + throwawayBytes)
                {
                    var remainingBytes = chunkLength + throwawayBytes - (totalBytesRead - bytesRead);
                    var slice = buffer.AsMemory(0, remainingBytes);
                    var temp = Encoding.UTF8.GetString(slice.Span);
                    allBytes = allBytes.Concat(slice.ToArray());
                    // contentBytes = contentBytes.Concat(slice.ToArray());

                    var slice2 = buffer.AsMemory(remainingBytes, bytesRead - remainingBytes);
                    var temp2 = Encoding.UTF8.GetString(slice2.Span);

                    (chunkStart, chunkLength, throwawayBytes) = PeekChunk(slice2.Span);
                    allBytes = allBytes.Concat(slice2.ToArray());
                    totalBytesRead = slice2.Length - chunkStart;
                    // totalBytesRead = slice.Length;
                    if (chunkLength == 0) break;
                    if (chunkLength == totalBytesRead - throwawayBytes - zeroDataChunkPostfix) break; // last chunk had zero data chunk as well
                }
                else
                {
                    var slice = buffer.AsMemory(0, bytesRead);
                    allBytes = allBytes.Concat(slice.ToArray());
                    // contentBytes = contentBytes.Concat(slice.ToArray());
                }
            }
            // contentBytes = contentBytes;
            var allText = Encoding.UTF8.GetString(allBytes.ToArray());
            // var allContentText = Encoding.UTF8.GetString(contentBytes.ToArray());
            return allBytes.ToArray();
            //throw new NotImplementedException();
        }

        private (int start, int length, int throwawayBytes) PeekChunk(Span<byte> span)
        {
            var endOfNumberIndex = span.IndexOf(_CRLFBuffer);
            var chunkSlice = span.Slice(0, endOfNumberIndex);
            var result = Utf8Parser.TryParse(chunkSlice, out int length, out int bytesConsumed, 'x');
            return (_CRLFBuffer.Length + bytesConsumed, length, _CRLFBuffer.Length);
        }

        public async Task<byte[]> TransferEncoding(Stream sourceStream, int bufferSize, Memory<byte> startOfBody = default)
        {
            var buffer = new byte[bufferSize];
            var allBytes = Enumerable.Empty<byte>();
            var isFinalChunk = false;
            while (!isFinalChunk)
            {
                var chunkLength = await ReadChunkLength();
                if (chunkLength == 0) isFinalChunk = true;
                var chunkBytesRead = 0;
                while (chunkBytesRead < chunkLength + 2)
                {
                    var remaining = chunkLength + 2 - chunkBytesRead;
                    var bytesRead = await sourceStream.ReadAsync(buffer, 0, remaining);
                    chunkBytesRead += bytesRead;

                    var bytesReadSlice = buffer.AsMemory(0, bytesRead);
                    var sliceTemp = Encoding.UTF8.GetString(bytesReadSlice.Span);
                    allBytes = allBytes.Concat(bytesReadSlice.ToArray());
                }
            }

            var tempAllText = Encoding.UTF8.GetString(allBytes.ToArray());
            return allBytes.ToArray();

            async Task<int> ReadChunkLength()
            {
                var index = 0;
                //while (buffer[index] != 13)
                while (true)
                {
                    await sourceStream.ReadAsync(buffer, index, 1);
                    if (buffer[index] == 10) break;
                    index++;
                }
                var chunkSlice = buffer.AsMemory(0, index - 1);
                var result = Utf8Parser.TryParse(chunkSlice.Span, out int length, out int bytesConsumed, 'x');
                var allSlice = buffer.AsMemory(0, index + 1);
                allBytes = allBytes.Concat(allSlice.ToArray());
                return length;
            }
        }
    }
}
