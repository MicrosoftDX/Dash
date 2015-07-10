//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Dash.Common.Utils;

namespace Microsoft.Tests
{
    class TestConfigurationProvider : IDashConfigurationSource
    {
        IDictionary<string, string> _testConfig;
        IDictionary<string, string> _tempConfig;
        CloudStorageAccount _namespaceAccount;
        IList<CloudStorageAccount> _dataAccounts;
        Dictionary<string, CloudStorageAccount> _dataAccountsByName;

        public TestConfigurationProvider(IDictionary<string, string> config)
        {
            _testConfig = config;
            string connectionString = GetSetting("StorageConnectionStringMaster", "");
            if (!String.IsNullOrWhiteSpace(connectionString))
            {
                _namespaceAccount = CloudStorageAccount.Parse(connectionString);
            }
            _dataAccounts = GetDataStorageAccountsFromConfig().ToArray();
            _dataAccountsByName = _dataAccounts
                .ToDictionary(account => account.Credentials.AccountName, StringComparer.OrdinalIgnoreCase);
        }

        public void SetTemporaryConfig(IDictionary<string, string> config)
        {
            _tempConfig = config;
        }

        public void ResetTemporaryConfig()
        {
            _tempConfig = null;
        }

        IEnumerable<CloudStorageAccount> GetDataStorageAccountsFromConfig()
        {
            for (int accountIndex = 0; true; accountIndex++)
            {
                var connectString = GetSetting("ScaleoutStorage" + accountIndex.ToString(), "");
                if (String.IsNullOrWhiteSpace(connectString))
                {
                    yield break;
                }
                yield return CloudStorageAccount.Parse(connectString);
            }
        }

        public IList<CloudStorageAccount> DataAccounts
        {
            get { return _dataAccounts; }
        }

        public IDictionary<string, CloudStorageAccount> DataAccountsByName
        {
            get { return _dataAccountsByName; }
        }

        public CloudStorageAccount NamespaceAccount
        {
            get { return _namespaceAccount; }
        }

        public T GetSetting<T>(string settingName, T defaultValue)
        {
            try
            {
                bool settingFound = false;
                string configValue = null;
                if (_tempConfig != null)
                {
                    settingFound = _tempConfig.TryGetValue(settingName, out configValue);
                }
                if (!settingFound)
                {
                    configValue = _testConfig[settingName];
                }
                if (typeof(T).IsEnum)
                {
                    return (T)Enum.Parse(typeof(T), configValue);
                }
                return (T)Convert.ChangeType(configValue, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
    }

}
