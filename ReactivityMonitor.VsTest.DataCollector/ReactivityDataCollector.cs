using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using ReactivityMonitor.ProfilerClient;

namespace ReactivityMonitor.VsTest
{
    [DataCollectorFriendlyName("ReactivityDataCollector")]
    [DataCollectorTypeUri("datacollector://ReactivityMonitor/ReactivityDataCollector/1")]
    public sealed class ReactivityDataCollector : DataCollector, ITestExecutionEnvironmentSpecifier
    {
        private readonly string mTempFolder = Path.GetTempPath();
        private readonly ProcessSetup mProcessSetup;
        private TaskCompletionSource<Unit> mSessionEnded;
        private IObservable<Unit> mFileWriterThing;
        private string mDataFilePath;
        private DataCollectionSink mDataSink;

        public ReactivityDataCollector()
        {
            var profilersLocation =
                Path.Combine(
                    Path.GetDirectoryName(
                        new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath),
                    "profiler");

            mProcessSetup = new ProcessSetup(profilersLocation)
            {
                WaitForConnection = true,
                MonitorAllFromStart = true
            };
        }

        public override void Initialize(XmlElement configurationElement, DataCollectionEvents events, DataCollectionSink dataSink, DataCollectionLogger logger, DataCollectionEnvironmentContext environmentContext)
        {
            mDataSink = dataSink;

            events.SessionStart += OnSessionStart;
            events.SessionEnd += OnSessionEnd;
        }

        private void OnSessionStart(object sender, SessionStartEventArgs e)
        {
            mDataFilePath = Path.Combine(mTempFolder, e.Context.SessionId.Id.ToString("N"));
            mSessionEnded = new TaskCompletionSource<Unit>();

            var outgoingMessages = Observable.Never<byte[]>()
                .TakeUntil(mSessionEnded.Task.ToObservable());

            var writingStream = Observable.Using(() => OpenDataFile(mDataFilePath),
                writer =>
                {
                    return ServerCommunication.CreateRawChannel(mProcessSetup.PipeName, outgoingMessages)
                        .Do(msg =>
                        {
                            writer.Write(msg.Length);
                            writer.Write(msg);
                        });
                })
                .Select(_ => Unit.Default)
                .Publish();

            writingStream.Connect();
            mFileWriterThing = writingStream.AsObservable();
        }

        private void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
            mSessionEnded.SetResult(default);
            mFileWriterThing.Wait();

            mDataSink.SendFileAsync(e.Context, mDataFilePath, true);
        }

        public IEnumerable<KeyValuePair<string, string>> GetTestExecutionEnvironmentVariables()
        {
            return mProcessSetup.GetEnvironmentVariables();
        }

        private BinaryWriter OpenDataFile(string file)
        {
            return new BinaryWriter(File.Create(file));
        }
    }
}
