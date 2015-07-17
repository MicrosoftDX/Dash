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
            int numDataAccounts = GetSetting("ScaleoutNumberOfAccounts", 0);
            _dataAccounts = new CloudStorageAccount[numDataAccounts];
            for (int accountIndex = 0; accountIndex < numDataAccounts; accountIndex++)
            {
                _dataAccounts[accountIndex] = CloudStorageAccount.Parse(GetSetting("ScaleoutStorage" + accountIndex.ToString(), ""));
            }
            _dataAccountsByName = _dataAccounts
                .ToDictionary(account => account.Credentials.AccountName, StringComparer.OrdinalIgnoreCase);
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
                string configValue = _testConfig[settingName];
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
