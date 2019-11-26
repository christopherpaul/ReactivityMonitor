using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace ReactivityMonitor.ProfilerClient
{
    internal sealed class Channel : IDisposable
    {
        private readonly Action mConnectedCallback;
        private readonly Action<Stream> mMessageReceivedCallback;
        private readonly NamedPipeClientStream mPipeStream;
        private readonly Thread mReaderThread;
        private bool mIsDisposed;

        public Channel(string pipeName, Action connectedCallback, Action<Stream> messageReceivedCallback)
        {
            mConnectedCallback = connectedCallback;
            mMessageReceivedCallback = messageReceivedCallback;

            Trace.WriteLine($"Creating pipe: {pipeName}");
            mPipeStream = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);

            var readerThread = new Thread(ReceiveMessages);
            readerThread.IsBackground = true;
            readerThread.Name = GetType().FullName + "#Read";

            mReaderThread = readerThread;
        }

        public void Start()
        {
            if ((mReaderThread.ThreadState & System.Threading.ThreadState.Unstarted) != 0)
            {
                mReaderThread.Start();
            }
        }

        public void SendMessage(byte[] message)
        {
            if (mPipeStream.IsConnected)
            {
                mPipeStream.Write(message, 0, message.Length);
            }
        }

        public bool IsConnected => mPipeStream.IsConnected;

        private void ReceiveMessages()
        {
            byte[] buffer = new byte[64];
            while (!mIsDisposed)
            {
                try
                {
                    if (!mPipeStream.IsConnected)
                    {
                        Trace.WriteLine("Connecting to the server");
                        mPipeStream.Connect();
                        mPipeStream.ReadMode = PipeTransmissionMode.Message;
                        Trace.WriteLine("Connected to the server");
                        OnConnected();
                    }

                    int bufferOffset = 0;
                    do
                    {
                        int remainingBufferSize = buffer.Length - bufferOffset;
                        if (remainingBufferSize == 0)
                        {
                            Array.Resize(ref buffer, buffer.Length * 2);
                            remainingBufferSize = buffer.Length - bufferOffset;
                        }

                        Trace.WriteLine("Waiting for data from pipe");
                        int bytesRead = mPipeStream.Read(buffer, bufferOffset, remainingBufferSize);
                        bufferOffset += bytesRead;
                    }
                    while (!mPipeStream.IsMessageComplete);
                    Trace.WriteLine("Message received from pipe");

                    OnMessageReceived(buffer, bufferOffset);
                }
                catch (Exception ex)
                {
                    if (!mIsDisposed)
                    {
                        Trace.TraceError("Error reading pipe: {0}", ex);

                        // Pause and keep trying
                        Thread.Sleep(1000);
                    }
                }
            }
        }

        private void OnConnected()
        {
            try
            {
                mConnectedCallback();
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error executing connected callback: {0}", ex);
            }
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
            mIsDisposed = true;
            mPipeStream.Dispose();
        }
    }
}
