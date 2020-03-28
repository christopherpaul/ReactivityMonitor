using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;

namespace ReactivityMonitor.ProfilerClient
{
    public static class ProfilerCommunication
    {
        /// <summary>
        /// Creates an observable that emits a raw message stream for each connection made
        /// to the specified pipe.
        /// </summary>
        /// <param name="pipeName">Name of the pipe to receive connections on</param>
        /// <param name="outgoingMessages">Outgoing messages to send through connected pipes</param>
        public static IObservable<IObservable<byte[]>> CreateRawChannel(string pipeName, IObservable<byte[]> outgoingMessages)
        {
            return Observable.FromAsync(async cancellationToken =>
            {
                var disposables = new CompositeDisposable();

                Trace.TraceInformation($"Creating pipe: {pipeName}");
                var pipeStream = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                    transmissionMode: PipeTransmissionMode.Message,
                    options: PipeOptions.Asynchronous | PipeOptions.WriteThrough);
                pipeStream.ReadMode = PipeTransmissionMode.Message;

                disposables.Add(pipeStream);

                Trace.TraceInformation("Waiting for connection");
                await pipeStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                Trace.TraceInformation("Profiler has connected");

                return Observable.Create((IObserver<byte[]> observer) =>
                {
                    disposables.Add(outgoingMessages
                        .ObserveOn(NewThreadScheduler.Default)
                        .Subscribe(message => pipeStream.Write(message, 0, message.Length)));

                    disposables.Add(ReadIncomingMessages(pipeStream)
                        .ToObservable()
                        .SubscribeOn(NewThreadScheduler.Default)
                        .Subscribe(observer));

                    return disposables;
                });
            })
            .Repeat();
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
