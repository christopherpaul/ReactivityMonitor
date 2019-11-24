using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class InstrumentationStore
    {
        public int GetSize()
        {
            return (int)NativeMethods.GetStoreLength();
        }

        public byte[] GetData()
        {
            return GetData(0, GetSize());
        }

        public byte[] GetData(int offset, int bytesToRead)
        {
            const int cMaxChunk = 1024;
            var buffers = new List<byte[]>();

            while (bytesToRead > 0)
            {
                int chunkSize = Math.Min(bytesToRead, cMaxChunk);
                var chunkBuf = new byte[chunkSize];
                int actuallyRead = NativeMethods.ReadStore(offset, chunkBuf, chunkSize);
                if (actuallyRead < chunkSize)
                {
                    bytesToRead = 0;
                    if (actuallyRead > 0)
                    {
                        var cb2 = new byte[actuallyRead];
                        Array.Copy(chunkBuf, cb2, actuallyRead);
                        buffers.Add(cb2);
                    }
                    break;
                }

                buffers.Add(chunkBuf);
                offset += chunkSize;
                bytesToRead -= chunkSize;
            }

            int total = buffers.Sum(b => b.Length);
            var result = new byte[total];
            int n = 0;
            foreach (var buf in buffers)
            {
                Array.Copy(buf, 0, result, n, buf.Length);
                n += buf.Length;
            }

            return result;
        }
    }
}
