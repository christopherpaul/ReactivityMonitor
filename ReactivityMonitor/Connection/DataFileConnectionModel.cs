using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactivityMonitor.Model;
using ReactivityMonitor.ProfilerClient;

namespace ReactivityMonitor.Connection
{
    internal sealed class DataFileConnectionModel : IConnectionModel
    {
        private readonly IModelUpdateSource mUpdateSource;

        public static DataFileConnectionModel Create(string dataFilePath)
        {
            var updateSource = DataFile.CreateModelUpdateSource(dataFilePath);
            var model = ReactivityModel.Create(updateSource);

            return new DataFileConnectionModel(dataFilePath, model, updateSource);
        }

        private DataFileConnectionModel(string dataFilePath, IReactivityModel model, IModelUpdateSource updateSource)
        {
            DataFilePath = dataFilePath;
            Model = model;
            mUpdateSource = updateSource;
        }

        public string Name => Path.GetFileName(DataFilePath);
        public string DataFilePath { get; }
        public IReactivityModel Model { get; }

        public IDisposable Connect()
        {
            return mUpdateSource.Connect();
        }

        public void PauseUpdates()
        {
        }

        public void RequestObjectProperties(long objectId)
        {
        }

        public void ResumeUpdates()
        {
        }

        public void StartMonitoringCall(int callId)
        {
        }

        public void StopMonitoringCall(int callId)
        {
        }
    }
}
