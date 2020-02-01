using ReactivityMonitor.Model;
using ReactivityProfiler.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.ProfilerClient
{
    public static class DataFile
    {
        public static IModelUpdateSource CreateModelUpdateSource(string path)
        {
            return new ModelUpdateSource(GetDataFileStream(path));
        }

        private static IObservable<EventMessage> GetDataFileStream(string path)
        {
            return Observable.Using(() => File.OpenRead(path), stream =>
            {
                byte[] lengthBytes = new byte[4];
                return Observable.FromAsync(async () =>
                {
                    bool ok = await ReadBytes(lengthBytes);
                    if (!ok)
                    {
                        return null; // signal EOF
                    }
                    int length = BitConverter.ToInt32(lengthBytes, 0);

                    byte[] messageBytes = new byte[length];
                    ok = await ReadBytes(messageBytes);
                    if (!ok)
                    {
                        throw new InvalidDataException($"{path} contains invalid data.");
                    }

                    return EventMessage.Parser.ParseFrom(messageBytes);
                }).Repeat().TakeWhile(m => !(m is null));

                async Task<bool> ReadBytes(byte[] buf)
                {
                    int offset = 0;
                    while (offset < buf.Length)
                    {
                        int count = await stream.ReadAsync(buf, offset, buf.Length - offset);
                        if (count == 0)
                        {
                            return false;
                        }

                        offset += count;
                    }

                    return true;
                }
            });
        }
    }
}
