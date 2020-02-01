using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Server
{
    internal sealed class Channel : IDisposable
    {
        private readonly Action<Stream> mMessageReceivedCallback;
        private readonly Action mConnectedCallback;
        private readonly Action mDisconnectedCallback;
        private readonly NamedPipeServerStream mPipeStream;
        private readonly Thread mReaderThread;
        private readonly Thread mWriterThread;
        private readonly BlockingCollection<byte[]> mWriteQueue;
        private readonly string mPipeName;
        private readonly bool mRegisterPipeName;
        private bool mIsDisposed;

        public Channel(Action<Stream> messageReceivedCallback, Action connectedCallback, Action disconnectedCallback)
        {
            mMessageReceivedCallback = messageReceivedCallback;
            mConnectedCallback = connectedCallback;
            mDisconnectedCallback = disconnectedCallback;
            (mPipeName, mRegisterPipeName) = GetPipeName();
            Trace.TraceInformation($"Opening pipe: {mPipeName}");
            mPipeStream = new NamedPipeServerStream(
                mPipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                transmissionMode: PipeTransmissionMode.Message,
                options: PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            mPipeStream.ReadMode = PipeTransmissionMode.Message;

            var readerThread = new Thread(ReceiveMessages);
            readerThread.IsBackground = true;
            readerThread.Name = GetType().FullName + "#Read";

            mReaderThread = readerThread;

            var writerThread = new Thread(SendMessages);
            writerThread.IsBackground = true;
            writerThread.Name = GetType().FullName + "#Write";

            mWriterThread = writerThread;

            mWriteQueue = new BlockingCollection<byte[]>();
        }

        private static (string, bool) GetPipeName()
        {
            string pipeName = ProfilerOptions.PipeName;
            if (!string.IsNullOrWhiteSpace(pipeName))
            {
                return (pipeName, false);
            }

            return ($"{nameof(ReactivityProfiler)}.{Process.GetCurrentProcess().Id}.{Guid.NewGuid():N}", true);
        }

        public void Start()
        {
            mReaderThread.Start();
        }

        /// <summary>
        /// Queues an outgoing message. The caller must not modify <paramref name="message"/>
        /// after the call returns.
        /// </summary>
        public void SendMessage(byte[] message)
        {
            try
            {
                mWriteQueue.Add(message);
            }
            catch (InvalidOperationException)
            {
                // queue has been closed
            }
        }

        public bool IsConnected => mPipeStream.IsConnected;

        private void SendMessages()
        {
            Trace.TraceInformation("SendMessages thread started");
            foreach (byte[] message in mWriteQueue.GetConsumingEnumerable())
            {
                try
                {
                    if (mPipeStream.IsConnected)
                    {
                        mPipeStream.Write(message, 0, message.Length);
                    }
                    else
                    {
                        Trace.TraceWarning("Not connected; message dropped.");
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Error sending message: {0}", ex);
                }
            }
        }

        private void ReceiveMessages()
        {
            try
            {
                if (mRegisterPipeName)
                {
                    Registry.SetChannelPipeName(mPipeName);
                }

                Trace.TraceInformation("Waiting for client to connect");
                mPipeStream.WaitForConnection();
                Trace.TraceInformation("Client has connected");

                mConnectedCallback?.Invoke();

                if (mRegisterPipeName)
                {
                    Registry.ClearChannelPipeName();
                }

                mWriterThread.Start();

                byte[] buffer = new byte[64];
                while (mPipeStream.IsConnected && !mIsDisposed)
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

                        int bytesRead = mPipeStream.Read(buffer, bufferOffset, remainingBufferSize);
                        bufferOffset += bytesRead;
                    }
                    while (!mPipeStream.IsMessageComplete);

                    if (bufferOffset > 0 && !mIsDisposed)
                    {
                        OnMessageReceived(buffer, bufferOffset);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error reading pipe: {0}", ex);
            }

            mDisconnectedCallback?.Invoke();
        }

        private void OnMessageReceived(byte[] buffer, int length)
        {
            try
            {
                mMessageReceivedCallback(new MemoryStream(buffer, 0, length));
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error processing message: {0}", ex);
            }
        }

        public void Dispose()
        {
            if (!mIsDisposed)
            {
                mIsDisposed = true;
                mWriteQueue.CompleteAdding();
                if (mWriterThread.IsAlive)
                {
                    mWriterThread.Join();
                }
                mPipeStream.Dispose();
            }
        }
    }
}
