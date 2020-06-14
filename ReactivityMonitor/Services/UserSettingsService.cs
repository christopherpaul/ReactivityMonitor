using Newtonsoft.Json;
using ReactivityMonitor.Connection;
using ReactivityMonitor.SettingsDto;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Services
{
    internal sealed class UserSettingsService : IUserSettingsService
    {
        private static readonly JsonSerializer cSerializer = new JsonSerializer();
        private const int cMaxMruListSize = 20;

        private readonly object mSync = new object();
        private readonly List<LaunchMruEntry> mLaunchMruEntries;

        public UserSettingsService()
        {
            Settings.Upgrade();

            string launchMruEntriesSerialized = Settings.LaunchMruList;
            if (TryDeserialize(launchMruEntriesSerialized, out List<LaunchMruEntry> entries))
            {
                mLaunchMruEntries = entries;
            }
            else
            {
                mLaunchMruEntries = new List<LaunchMruEntry>();
            }
        }

        public void AddLaunchToMruList(LaunchInfo launchInfo)
        {
            var newEntry = new LaunchMruEntry
            {
                ExecutablePath = launchInfo.FileName ?? string.Empty,
                Arguments = launchInfo.Arguments ?? string.Empty,
                MonitorAllFromStart = launchInfo.Options.HasFlag(LaunchOptions.MonitorAllFromStart)
            };

            lock (mSync)
            {
                int existingIndex = mLaunchMruEntries.FindIndex(existingEntry =>
                    string.Equals(existingEntry.ExecutablePath, newEntry.ExecutablePath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existingEntry.Arguments, newEntry.Arguments, StringComparison.Ordinal));

                if (existingIndex >= 0)
                {
                    mLaunchMruEntries.RemoveAt(existingIndex);
                }

                mLaunchMruEntries.Insert(0, newEntry);
                if (mLaunchMruEntries.Count > cMaxMruListSize)
                {
                    mLaunchMruEntries.RemoveAt(cMaxMruListSize);
                }

                Settings.LaunchMruList = Serialize(mLaunchMruEntries);
                Settings.Save();
            }
        }

        public IEnumerable<LaunchInfo> GetMostRecentLaunches()
        {
            lock (mSync)
            {
                return mLaunchMruEntries.Select(entry => new LaunchInfo
                {
                    FileName = entry.ExecutablePath ?? string.Empty,
                    Arguments = entry.Arguments ?? string.Empty,
                    Options = entry.MonitorAllFromStart ? LaunchOptions.MonitorAllFromStart : LaunchOptions.Default
                }).ToArray().AsEnumerable();
            }
        }

        private static bool TryDeserialize<T>(string serialized, out T deserialized)
        {
            if (!string.IsNullOrWhiteSpace(serialized))
            {
                try
                {
                    using (var textReader = new StringReader(serialized))
                    using (var jsonReader = new JsonTextReader(textReader))
                    {
                        deserialized = cSerializer.Deserialize<T>(jsonReader);
                    }

                    return true;
                }
                catch
                {
                    //TODO log
                }
            }

            deserialized = default;
            return false;
        }

        private static string Serialize<T>(T unserialized)
        {
            using (var writer = new StringWriter())
            {
                cSerializer.Serialize(writer, unserialized);
                return writer.ToString();
            }
        }

        private static Properties.Settings Settings => Properties.Settings.Default;
    }
}
