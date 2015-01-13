//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Dash.Server.Utils
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
                _dataAccounts = new Lazy<IList<CloudStorageAccount>>(() =>
                {
                    int numDataAccounts = AzureUtils.GetConfigSetting("ScaleoutNumberOfAccounts", 0);
                    var accountsArray = new CloudStorageAccount[numDataAccounts];
                    for (int accountIndex = 0; accountIndex < numDataAccounts; accountIndex++)
                    {
                        var connectString = AzureUtils.GetConfigSetting("ScaleoutStorage" + accountIndex.ToString(), "");
                        CloudStorageAccount account;
                        if (CloudStorageAccount.TryParse(connectString, out account))
                        {
                            accountsArray[accountIndex] = account;
                            ServicePointManager.FindServicePoint(account.BlobEndpoint).ConnectionLimit = int.MaxValue;
                        }
                        else
                        {
                            // TODO: Trace warning when we have loggin infrastructure
                        }
                    }
                    return Array.AsReadOnly(accountsArray);
                }, LazyThreadSafetyMode.PublicationOnly);

                _dataAccountsByName = new Lazy<IDictionary<string, CloudStorageAccount>>(() => DashConfiguration.DataAccounts
                    .Where(account => account != null)
                    .ToDictionary(account => account.Credentials.AccountName, StringComparer.OrdinalIgnoreCase), LazyThreadSafetyMode.PublicationOnly);

                _namespaceAccount = new Lazy<CloudStorageAccount>(() =>
                {
                    CloudStorageAccount account;
                    if (!CloudStorageAccount.TryParse(AzureUtils.GetConfigSetting("StorageConnectionStringMaster", ""), out account))
                    {
                        // TODO: Trace failure warning when we have logging infrastructure
                    }
                    ServicePointManager.FindServicePoint(account.BlobEndpoint).ConnectionLimit = int.MaxValue;
                    return account;
                }, LazyThreadSafetyMode.PublicationOnly);
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
    }
}