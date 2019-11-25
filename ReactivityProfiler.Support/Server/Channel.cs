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
    internal sealed class Channel
    {
        private readonly Action<Stream> mMessageReceivedCallback;
        private readonly NamedPipeServerStream mPipeStream;
        private readonly Thread mReaderThread;
        private readonly Thread mWriterThread;
        private readonly BlockingCollection<byte[]> mWriteQueue;

        public Channel(Action<Stream> messageReceivedCallback)
        {
            mMessageReceivedCallback = messageReceivedCallback;

            string pipeName = $"{nameof(ReactivityProfiler)}.{Process.GetCurrentProcess().Id}";
            Trace.WriteLine($"Opening pipe: {pipeName}");
            mPipeStream = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                transmissionMode: PipeTransmissionMode.Message);
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

        public void Start()
        {
            mReaderThread.Start();
            mWriterThread.Start();
        }

        /// <summary>
        /// Queues an outgoing message. The caller must not modify <paramref name="message"/>
        /// after the call returns.
        /// </summary>
        public void SendMessage(byte[] message)
        {
            mWriteQueue.Add(message);
        }

        public bool IsConnected => mPipeStream.IsConnected;

        private void SendMessages()
        {
            foreach (byte[] message in mWriteQueue)
            {
                try
                {
                    if (mPipeStream.IsConnected)
                    {
                        mPipeStream.Write(message, 0, message.Length);
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
            byte[] buffer = new byte[64];
            while (true)
            {
                try
                {
                    if (!mPipeStream.IsConnected)
                    {
                        Trace.WriteLine("Waiting for client to connect");
                        mPipeStream.WaitForConnection();
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

                        int bytesRead = mPipeStream.Read(buffer, bufferOffset, remainingBufferSize);
                        bufferOffset += bytesRead;
                    }
                    while (!mPipeStream.IsMessageComplete);

                    OnMessageReceived(buffer, bufferOffset);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Error reading pipe: {0}", ex);

                    // Pause and keep trying
                    Thread.Sleep(1000);
                }
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
    }
}
