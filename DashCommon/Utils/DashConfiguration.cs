﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Dash.Common.Utils
{
    // Factored out for test mocking
    public interface IDashConfigurationSource
    {
        IList<CloudStorageAccount> DataAccounts { get; }
        IDictionary<string, CloudStorageAccount> DataAccountsByName { get; }
        CloudStorageAccount NamespaceAccount { get; }
        T GetSetting<T>(string settingName, T defaultValue);
    }

    public static class DashConfiguration
    {
        class DashConfigurationSource : IDashConfigurationSource
        {
            static Lazy<IList<CloudStorageAccount>> _dataAccounts;
            static Lazy<IDictionary<string, CloudStorageAccount>> _dataAccountsByName;
            static Lazy<CloudStorageAccount> _namespaceAccount;

            static DashConfigurationSource()
            {
                _dataAccounts = new Lazy<IList<CloudStorageAccount>>(() => GetDataStorageAccountsFromConfig().ToList(), LazyThreadSafetyMode.PublicationOnly);
                _dataAccountsByName = new Lazy<IDictionary<string, CloudStorageAccount>>(() => DashConfiguration.DataAccounts
                    .Where(account => account != null)
                    .ToDictionary(account => account.Credentials.AccountName, StringComparer.OrdinalIgnoreCase), LazyThreadSafetyMode.PublicationOnly);
                _namespaceAccount = new Lazy<CloudStorageAccount>(() =>
                {
                    CloudStorageAccount account;
                    string connectString = AzureUtils.GetConfigSetting("StorageConnectionStringMaster", "");
                    if (!CloudStorageAccount.TryParse(connectString, out account))
                    {
                        DashTrace.TraceError("Error reading namespace account connection string from configuration. Details: {0}", connectString);
                        return null;
                    }
                    ServicePointManager.FindServicePoint(account.BlobEndpoint).ConnectionLimit = int.MaxValue;
                    return account;
                }, LazyThreadSafetyMode.PublicationOnly);
            }

            static IEnumerable<CloudStorageAccount> GetDataStorageAccountsFromConfig()
            {
                for (int accountIndex = 0; true; accountIndex++)
                {
                    var connectString = AzureUtils.GetConfigSetting("ScaleoutStorage" + accountIndex.ToString(), "");
                    if (String.IsNullOrWhiteSpace(connectString))
                    {
                        yield break;
                    }
                    CloudStorageAccount account;
                    if (CloudStorageAccount.TryParse(connectString, out account))
                    {
                        ServicePointManager.FindServicePoint(account.BlobEndpoint).ConnectionLimit = int.MaxValue;
                        yield return account;
                    }
                    else
                    {
                        DashTrace.TraceWarning("Error reading data account connection string from configuration. Configuration details: {0}:{1}", accountIndex, connectString);
                    }
                }
            }

            public IList<CloudStorageAccount> DataAccounts
            {
                get { return _dataAccounts.Value; }
            }

            public IDictionary<string, CloudStorageAccount> DataAccountsByName
            {
                get { return _dataAccountsByName.Value; }
            }

            public CloudStorageAccount NamespaceAccount
            {
                get { return _namespaceAccount.Value; }
            }

            public T GetSetting<T>(string settingName, T defaultValue)
            {
                return AzureUtils.GetConfigSetting(settingName, defaultValue);
            }
        }

        public static IDashConfigurationSource ConfigurationSource = new DashConfigurationSource();

        public static IList<CloudStorageAccount> DataAccounts
        {
            get { return ConfigurationSource.DataAccounts; }
        }

        public static CloudStorageAccount GetDataAccountByAccountName(string accountName)
        {
            return ConfigurationSource.DataAccountsByName[accountName];
        }

        public static CloudStorageAccount NamespaceAccount
        {
            get { return ConfigurationSource.NamespaceAccount; }
        }

        public static IEnumerable<CloudStorageAccount> AllAccounts
        {
            get 
            {
                return new[] { DashConfiguration.NamespaceAccount }
                    .Concat(DashConfiguration.DataAccounts);
            }
        }

        public static string AccountName
        {
            get { return ConfigurationSource.GetSetting("AccountName", ""); }
        }

        public static byte[] AccountKey
        {
            get { return Convert.FromBase64String(ConfigurationSource.GetSetting("AccountKey", "")); }
        }

        public static byte[] SecondaryAccountKey
        {
            get { return Convert.FromBase64String(ConfigurationSource.GetSetting("SecondaryAccountKey", "")); }
        }

        public static bool LogNormalOperations
        {
            get { return ConfigurationSource.GetSetting("LogNormalOperations", false); }
        }

        public static IEnumerable<string> ImportAccounts
        {
            get
            {
                // Account can be specified either by zero-based index or account name
                var accounts = DashConfiguration.DataAccounts;
                foreach (var item in ConfigurationSource.GetSetting("ImportAccounts", "").Split(new [] {',', ';'}, StringSplitOptions.RemoveEmptyEntries))
                {
                    string accountName;
                    int index;
                    if (int.TryParse(item, out index) && index >= 0 && index < accounts.Count)
                    {
                        accountName = DashConfiguration.DataAccounts[index].Credentials.AccountName;
                    }
                    else
                    {
                        accountName = item;
                    }
                    yield return accountName;
                }
            }
        }

        public static string WorkerQueueName
        {
            get { return ConfigurationSource.GetSetting("WorkerQueueName", "dashworkerqueue"); }
        }

        public static int WorkerQueueTimeout
        {
            get { return ConfigurationSource.GetSetting("WorkerQueueTimeout", 30); }
        }
    }
}