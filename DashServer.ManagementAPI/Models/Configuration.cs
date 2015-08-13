//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;

namespace DashServer.ManagementAPI.Models
{
    public class Configuration
    {
        public string OperationId { get; set; }
        public IDictionary<string, string> AccountSettings { get; set; }
        public ScaleAccounts ScaleAccounts { get; set; }
        public IDictionary<string, string> GeneralSettings { get; set; }
    }

    public class ScaleAccounts
    {
        public int MaxAccounts { get; set; }
        public IList<ScaleAccount> Accounts { get; set; }
    }

    public class ScaleAccount
    {
        public string AccountName { get; set; }
        public string AccountKey { get; set; }
        // Currently ignored
        public bool UseTls { get; set; }
    }

    public class StorageValidation
    {
        public bool NewStorageNameValid { get; set; }
        public bool ExistingStorageNameValid { get; set; }
        public bool StorageKeyValid { get; set; }
    }
}