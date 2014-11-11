//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Dash.Server.Utils
{
    public static class DashConfiguration
    {
        static Lazy<IList<CloudStorageAccount>> _dataAccounts;
        static Lazy<IDictionary<string, CloudStorageAccount>> _dataAccountsByName;
        static Lazy<CloudStorageAccount> _namespaceAccount;

        static DashConfiguration()
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
                    }
                    else
                    {
                        // TODO: Trace warning when we have loggin infrastructure
                    }
                }
                return Array.AsReadOnly(accountsArray);
            }, LazyThreadSafetyMode.PublicationOnly);

            _dataAccountsByName = new Lazy<IDictionary<string,CloudStorageAccount>>(() => DashConfiguration.DataAccounts
                .Where(account => account != null)
                .ToDictionary(account => account.Credentials.AccountName), LazyThreadSafetyMode.PublicationOnly);

            _namespaceAccount = new Lazy<CloudStorageAccount>(() =>
            {
                CloudStorageAccount account;
                if (!CloudStorageAccount.TryParse(AzureUtils.GetConfigSetting("StorageConnectionStringMaster", ""), out account))
                {
                    // TODO: Trace failure warning when we have logging infrastructure
                }
                return account;
            }, LazyThreadSafetyMode.PublicationOnly);
        }

        public static IList<CloudStorageAccount> DataAccounts
        {
            get { return _dataAccounts.Value; }
        }

        public static CloudStorageAccount GetDataAccountByAccountName(string accountName)
        {
            return _dataAccountsByName.Value[accountName];
        }

        public static CloudStorageAccount NamespaceAccount
        {
            get { return _namespaceAccount.Value; }
        }

        public static string AccountName
        {
            get { return AzureUtils.GetConfigSetting("AccountName", ""); }
        }

        public static byte[] AccountKey
        {
            get { return Convert.FromBase64String(AzureUtils.GetConfigSetting("AccountKey", "")); }
        }
    }
}