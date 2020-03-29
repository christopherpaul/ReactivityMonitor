using ReactivityMonitor.Utility.Extensions;
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
        /// <remarks>
        /// <para>The resources for each emitted observable message stream remain allocated until
        /// either the other end of the pipe disconnects or <paramref name="outgoingMessages"/>
        /// terminates. Unsubscribing from the message stream does not release resources.</para>
        /// <para>Each emitted message stream is hot once it has been subscribed to for the first
        /// time.</para>
        /// </remarks>
        public static IObservable<IObservable<byte[]>> CreateRawChannel(string pipeName, IObservable<byte[]> outgoingMessages)
        {
            return Observable.FromAsync(async cancellationToken =>
            {
                Trace.TraceInformation($"Creating pipe: {pipeName}");
                var pipeStream = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                    transmissionMode: PipeTransmissionMode.Message,
                    options: PipeOptions.Asynchronous | PipeOptions.WriteThrough);
                pipeStream.ReadMode = PipeTransmissionMode.Message;

                try
                {
                    Trace.TraceInformation("Waiting for connection");
                    await pipeStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    Trace.TraceInformation("Profiler has connected");

                    var incomingMessageStream = Observable.Create((IObserver<byte[]> observer) =>
                    {
                        var writer = new CompositeDisposable();

                        outgoingMessages
                            .TakeUntilDisposed(writer)
                            .ObserveOn(NewThreadScheduler.Default)
                            .Finally(pipeStream.Dispose) // after finishing writing, dispose pipe (which will terminate the reader too)
                            .Subscribe(message => pipeStream.Write(message, 0, message.Length));

                        ReadIncomingMessages(pipeStream)
                            .ToObservable()
                            .Finally(writer.Dispose) // If other end disconnects, stop the writer (which will then dispose the pipe)
                            .SubscribeOn(NewThreadScheduler.Default)
                            .Subscribe(observer);

                        return Disposable.Empty;
                    }).Publish().AutoConnect();

                    return incomingMessageStream;
                }
                catch
                {
                    pipeStream.Dispose();
                    throw;
                }
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
