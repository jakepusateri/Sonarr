﻿using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Events;


namespace NzbDrone.Core.Configuration
{
    public enum ConfigKey
    {
        DownloadedEpisodesFolder
    }

    public class ConfigService : IConfigService
    {
        private readonly IConfigRepository _repository;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;
        private static Dictionary<string, string> _cache;

        public ConfigService(IConfigRepository repository, IEventAggregator eventAggregator, Logger logger)
        {
            _repository = repository;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _cache = new Dictionary<string, string>();
        }

        public IEnumerable<Config> All()
        {
            return _repository.All();
        }

        public Dictionary<String, Object> AllWithDefaults()
        {
            var dict = new Dictionary<String, Object>(StringComparer.InvariantCultureIgnoreCase);

            var type = GetType();
            var properties = type.GetProperties();

            foreach (var propertyInfo in properties)
            {
                var value = propertyInfo.GetValue(this, null);

                dict.Add(propertyInfo.Name, value);
            }

            return dict;
        }

        public void SaveConfigDictionary(Dictionary<string, object> configValues)
        {
            var allWithDefaults = AllWithDefaults();

            foreach (var configValue in configValues)
            {
                object currentValue;
                allWithDefaults.TryGetValue(configValue.Key, out currentValue);
                if (currentValue == null) continue;

                var equal = configValue.Value.ToString().Equals(currentValue.ToString());

                if (!equal)
                    SetValue(configValue.Key, configValue.Value.ToString());
            }

            _eventAggregator.PublishEvent(new ConfigSavedEvent());
        }

        public String DownloadedEpisodesFolder
        {
            get { return GetValue(ConfigKey.DownloadedEpisodesFolder.ToString()); }

            set { SetValue(ConfigKey.DownloadedEpisodesFolder.ToString(), value); }
        }

        public bool AutoUnmonitorPreviouslyDownloadedEpisodes
        {
            get { return GetValueBoolean("AutoUnmonitorPreviouslyDownloadedEpisodes"); }
            set { SetValue("AutoUnmonitorPreviouslyDownloadedEpisodes", value); }
        }

        public int Retention
        {
            get { return GetValueInt("Retention", 0); }
            set { SetValue("Retention", value); }
        }

        public string RecycleBin
        {
            get { return GetValue("RecycleBin", String.Empty); }
            set { SetValue("RecycleBin", value); }
        }

        public string ReleaseRestrictions
        {
            get { return GetValue("ReleaseRestrictions", String.Empty).Trim('\r', '\n'); }
            set { SetValue("ReleaseRestrictions", value.Trim('\r', '\n')); }
        }

        public Int32 RssSyncInterval
        {
            get { return GetValueInt("RssSyncInterval", 15); }

            set { SetValue("RssSyncInterval", value); }
        }

        public Boolean AutoDownloadPropers
        {
            get { return GetValueBoolean("AutoDownloadPropers", true); }

            set { SetValue("AutoDownloadPropers", value); }
        }

        public Boolean AutoRedownloadFailed
        {
            get { return GetValueBoolean("AutoRedownloadFailed", true); }

            set { SetValue("AutoRedownloadFailed", value); }
        }

        public Boolean RemoveFailedDownloads
        {
            get { return GetValueBoolean("RemoveFailedDownloads", true); }

            set { SetValue("RemoveFailedDownloads", value); }
        }

        public Boolean EnableFailedDownloadHandling
        {
            get { return GetValueBoolean("EnableFailedDownloadHandling", true); }

            set { SetValue("EnableFailedDownloadHandling", value); }
        }

        public Boolean CreateEmptySeriesFolders
        {
            get { return GetValueBoolean("CreateEmptySeriesFolders", false); }

            set { SetValue("CreateEmptySeriesFolders", value); }
        }

        public FileDateType FileDate
        {
            get { return GetValueEnum("FileDate", FileDateType.None); }

            set { SetValue("FileDate", value); }
        }

        public String DownloadClientWorkingFolders
        {
            get { return GetValue("DownloadClientWorkingFolders", "_UNPACK_|_FAILED_"); }
            set { SetValue("DownloadClientWorkingFolders", value); }
        }

        public Boolean SetPermissionsLinux
        {
            get { return GetValueBoolean("SetPermissionsLinux", false); }

            set { SetValue("SetPermissionsLinux", value); }
        }

        public String FileChmod
        {
            get { return GetValue("FileChmod", "0644"); }

            set { SetValue("FileChmod", value); }
        }

        public String FolderChmod
        {
            get { return GetValue("FolderChmod", "0755"); }

            set { SetValue("FolderChmod", value); }
        }

        public String ChownUser
        {
            get { return GetValue("ChownUser", ""); }

            set { SetValue("ChownUser", value); }
        }

        public String ChownGroup
        {
            get { return GetValue("ChownGroup", ""); }

            set { SetValue("ChownGroup", value); }
        }

        private string GetValue(string key)
        {
            return GetValue(key, String.Empty);
        }

        private bool GetValueBoolean(string key, bool defaultValue = false)
        {
            return Convert.ToBoolean(GetValue(key, defaultValue));
        }

        private int GetValueInt(string key, int defaultValue = 0)
        {
            return Convert.ToInt32(GetValue(key, defaultValue));
        }

        public T GetValueEnum<T>(string key, T defaultValue)
        {
            return (T)Enum.Parse(typeof(T), GetValue(key, defaultValue), true);
        }

        public string GetValue(string key, object defaultValue, bool persist = false)
        {
            key = key.ToLowerInvariant();
            Ensure.That(key, () => key).IsNotNullOrWhiteSpace();

            EnsureCache();

            string dbValue;

            if (_cache.TryGetValue(key, out dbValue) && dbValue != null && !String.IsNullOrEmpty(dbValue))
                return dbValue;

            _logger.Trace("Unable to find config key '{0}' defaultValue:'{1}'", key, defaultValue);

            if (persist)
            {
                SetValue(key, defaultValue.ToString());
            }
            return defaultValue.ToString();
        }

        private void SetValue(string key, Boolean value)
        {
            SetValue(key, value.ToString());
        }

        private void SetValue(string key, int value)
        {
            SetValue(key, value.ToString());
        }

        public void SetValue(string key, string value)
        {
            key = key.ToLowerInvariant();

            _logger.Trace("Writing Setting to file. Key:'{0}' Value:'{1}'", key, value);

            var dbValue = _repository.Get(key);

            if (dbValue == null)
            {
                _repository.Insert(new Config { Key = key, Value = value });
            }
            else
            {
                dbValue.Value = value;
                _repository.Update(dbValue);
            }

            ClearCache();
        }

        public void SetValue(string key, Enum value)
        {
            SetValue(key, value.ToString().ToLower());
        }

        private void EnsureCache()
        {
            lock (_cache)
            {
                if (!_cache.Any())
                {
                    _cache = All().ToDictionary(c => c.Key.ToLower(), c => c.Value);
                }
            }
        }

        public static void ClearCache()
        {
            lock (_cache)
            {
                _cache = new Dictionary<string, string>();
            }
        }
    }
}
