using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SniffingProxy.Core
{
    public class InitialHttpData
    {
        public byte[] Buffer { get; set; }
        public Memory<byte> Headers { get; set; }
        public Memory<byte> Data { get; set; }
    }
}
