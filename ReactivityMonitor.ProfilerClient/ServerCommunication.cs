﻿using ReactivityMonitor.Utility.Extensions;
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
    public static class ServerCommunication
    {
        public static IObservable<byte[]> CreateRawChannel(string pipeName, IObservable<byte[]> outgoingMessages)
        {
            return Observable.Create((IObserver<byte[]> observer) =>
            {
                var disposables = new CompositeDisposable();

                var setupTask = Observable.FromAsync(async cancellationToken =>
                {
                    Trace.TraceInformation($"Creating pipe: {pipeName}");
                    var pipeStream = new NamedPipeClientStream(
                        ".",
                        pipeName,
                        PipeDirection.InOut,
                        PipeOptions.Asynchronous | PipeOptions.WriteThrough);

                    try
                    {
                        Trace.TraceInformation("Connecting to the server");
                        await pipeStream.ConnectAsync(cancellationToken);
                        pipeStream.ReadMode = PipeTransmissionMode.Message;
                        Trace.TraceInformation("Connected to the server");

                        outgoingMessages
                            .TakeUntilDisposed(disposables)
                            .ObserveOn(NewThreadScheduler.Default)
                            .Finally(pipeStream.Dispose) // disposing here avoids attempting to write after disposal
                            .Subscribe(message => pipeStream.Write(message, 0, message.Length));

                        ReadIncomingMessages(pipeStream)
                            .ToObservable()
                            .TakeUntilDisposed(disposables)
                            .SubscribeOn(NewThreadScheduler.Default)
                            .Subscribe(observer);

                        return Unit.Default;
                    }
                    catch
                    {
                        pipeStream.Dispose();
                        throw;
                    }
                });

                disposables.Add(setupTask.Subscribe());

                return disposables;
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
