using ReactivityMonitor.Connection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Services
{
    public interface IUserSettingsService
    {
        IEnumerable<LaunchInfo> GetMostRecentLaunches();
        void AddLaunchToMruList(LaunchInfo launchInfo);
    }
}
