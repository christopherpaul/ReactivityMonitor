using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;

namespace ReactivityMonitor.ProfilerClient
{
    internal static class ServerCommunication
    {
        public static IObservable<byte[]> CreateRawChannel(string pipeName, IObservable<byte[]> outgoingMessages)
        {
            return Observable.Create((IObserver<byte[]> observer) =>
            {
                Trace.TraceInformation($"Creating pipe: {pipeName}");
                var pipeStream = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous | PipeOptions.WriteThrough);

                Trace.TraceInformation("Connecting to the server");
                pipeStream.Connect();
                pipeStream.ReadMode = PipeTransmissionMode.Message;
                Trace.TraceInformation("Connected to the server");

                return StableCompositeDisposable.Create(
                    outgoingMessages
                        .ObserveOn(NewThreadScheduler.Default)
                        .Subscribe(message => pipeStream.Write(message, 0, message.Length)),

                    ReadIncomingMessages(pipeStream)
                        .ToObservable()
                        .SubscribeOn(NewThreadScheduler.Default)
                        .Subscribe(observer),

                    pipeStream);
            });
        }

        private static IEnumerable<byte[]> ReadIncomingMessages(PipeStream pipeStream)
        {
            byte[] buffer = new byte[64];

            while (pipeStream.IsConnected)
            {
                int bufferOffset = 0;
                do
                {
                    int remainingBufferSize = buffer.Length - bufferOffset;
                    if (remainingBufferSize == 0)
                    {
                        Array.Resize(ref buffer, buffer.Length * 2);
                        remainingBufferSize = buffer.Length - bufferOffset;
                    }

                    int bytesRead = pipeStream.Read(buffer, bufferOffset, remainingBufferSize);
                    bufferOffset += bytesRead;
                }
                while (!IsMessageCompleteSafe());

                if (bufferOffset > 0)
                {
                    var message = new byte[bufferOffset];
                    Array.Copy(buffer, 0, message, 0, bufferOffset);
                    yield return message;
                }
            }

            bool IsMessageCompleteSafe()
            {
                try
                {
                    if (!pipeStream.IsConnected)
                    {
                        return true; // not getting any more message content from a disconnected pipe
                    }

                    return pipeStream.IsMessageComplete;
                }
                catch (ObjectDisposedException)
                {
                    return true;
                }
            }
        }
    }
}
