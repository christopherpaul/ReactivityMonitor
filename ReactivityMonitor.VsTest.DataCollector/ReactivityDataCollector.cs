using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Google.Protobuf;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using ReactivityMonitor.ProfilerClient;
using ReactivityProfiler.Protocol;
using ReactivityMonitor.Utility.Extensions;
using ReactivityMonitor.Utility.Flyweights;

namespace ReactivityMonitor.VsTest
{
    [DataCollectorFriendlyName("ReactivityDataCollector")]
    [DataCollectorTypeUri("datacollector://ReactivityMonitor/ReactivityDataCollector/1")]
    public sealed class ReactivityDataCollector : DataCollector, ITestExecutionEnvironmentSpecifier
    {
        private readonly string mTempFolder = Path.Combine(Path.GetTempPath(), $"{nameof(ReactivityDataCollector)}-{Guid.NewGuid().ToString("D")}");
        private readonly ProcessSetup mProcessSetup;
        private IObservable<Unit> mFileWriterThing;
        private Action mSessionEnded = () => { };
        private string mDataFilePath;
        private DataCollectionSink mDataSink;
        private Action<RequestMessage> mSendMessage;
        private IObservable<string> mLatestEndedTestName; // cold; null means no active connections

        private const int cStartTestCaseEventId = 1;
        private const int cEndTestCaseEventId = 2;

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
            events.TestCaseStart += OnTestCaseStart;
            events.TestCaseEnd += OnTestCaseEnd;
        }

        private void OnSessionStart(object sender, SessionStartEventArgs e)
        {
            Trace.TraceInformation("Session starting");

            Directory.CreateDirectory(mTempFolder);
            mDataFilePath = Path.Combine(mTempFolder, "TestSessionEvents" + DataFile.ProfileDataFileExtension);

            var outgoingMessageSource = new ReplaySubject<RequestMessage>();
            mSendMessage = outgoingMessageSource.OnNext;

            var outgoingMessages = outgoingMessageSource
                .Select(msg => msg.ToByteArray())
                .Concat(Observable.Never<byte[]>()); // don't close pipe from this end

            var incomingRawMessageStreams = ProfilerCommunication.CreateRawChannel(mProcessSetup.PipeName, outgoingMessages)
                .Publish();

            var incomingMessageStreams = incomingRawMessageStreams
                .Select(rawMessages => rawMessages.Select(EventMessage.Parser.ParseFrom));

            var receivedClientEventStreams = incomingMessageStreams
                .Select(messages => messages
                    .Where(m => m.EventCase == EventMessage.EventOneofCase.ClientEvent)
                    .Select(m => m.ClientEvent)
                    .Replay(1));

            var latestEndedTest =
                Observable.Defer(() =>
                {
                    var returnNull = Observable.Return((string)null);
                    var latestTestPerStream = new Dictionary<int, string>();
                    return receivedClientEventStreams
                        .SelectMany((clientEvents, streamNumber) => clientEvents
                            .Where(ev => ev.Id == cEndTestCaseEventId)
                            .Select(ev => ev.Name)
                            .StartWith(string.Empty)
                            .Concat(returnNull)
                            .Select(testName => (streamNumber, testName)))
                        .Select(item =>
                        {
                            latestTestPerStream[item.streamNumber] = item.testName;
                            if (item.testName is null)
                            {
                                latestTestPerStream.Remove(item.streamNumber);
                            }

                            int count = latestTestPerStream.Values.Distinct().Take(2).Count();
                            if (count == 0)
                            {
                                return null;
                            }
                            else if (count == 1)
                            {
                                return latestTestPerStream.Values.First();
                            }
                            else
                            {
                                return string.Empty;
                            }
                        })
                        .Where(testName => testName != string.Empty);
                })
                .StartWith((string)null)
                .Replay(1);

            latestEndedTest.Connect();
            mLatestEndedTestName = latestEndedTest.AsObservable();

            var fileWriter = Observable.Using(() => OpenDataFile(mDataFilePath),
                writer =>
                {
                    return incomingRawMessageStreams
                        .TakeUntil(Observable.FromEvent(h => mSessionEnded += h, h => mSessionEnded -= h))
                        .Merge()
                        .Do(msg =>
                        {
                            writer.Write(msg.Length);
                            writer.Write(msg);
                        })
                        .LastOrDefaultAsync()
                        .Select(Funcs<byte[]>.DefaultOf<Unit>());
                })
                .Publish();
            fileWriter.Connect();
            mFileWriterThing = fileWriter.AsObservable();

            incomingRawMessageStreams.Connect();
        }

        private void OnTestCaseStart(object sender, TestCaseStartEventArgs e)
        {
            Trace.TraceInformation("Test case starting: {0}", e.TestCaseName);

            mSendMessage(new RequestMessage
            {
                RecordEvent = new RecordEventRequest
                {
                    Id = cStartTestCaseEventId,
                    Name = e.TestCaseId.ToString(),
                    Description = $"Start of test case {e.TestCaseName}"
                }
            });
        }

        private void OnTestCaseEnd(object sender, TestCaseEndEventArgs e)
        {
            Trace.TraceInformation("Test case ended: {0}", e.TestCaseName);

            mSendMessage(new RequestMessage
            {
                SendInstrumentationEvents = new SendInstrumentationEventsRequest
                {
                    Mode = SendInstrumentationEventsRequest.Types.RequestMode.OnceUnsent
                }
            });

            string testCaseId = e.TestCaseId.ToString();
            mSendMessage(new RequestMessage
            {
                RecordEvent = new RecordEventRequest
                {
                    Id = cEndTestCaseEventId,
                    Name = testCaseId,
                    Description = $"End of test case {e.TestCaseName}"
                }
            });

            mLatestEndedTestName.FirstAsync(testName => testName is null || testName == testCaseId)
                .Wait();
        }

        private void OnSessionEnd(object sender, SessionEndEventArgs e)
        {
            Trace.TraceInformation("Session ended");

            mSessionEnded();
            mFileWriterThing.LastOrDefaultAsync().Wait();
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
