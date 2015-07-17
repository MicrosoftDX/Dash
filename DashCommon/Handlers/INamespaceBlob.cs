//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Handlers
{
    public interface INamespaceBlob
    {
        string AccountName { get; set; }

        string Container { get; set; }

        string BlobName { get; set; }

        bool? IsMarkedForDeletion { get; set; }

        string PrimaryAccountName { get; set; }

        IList<string> DataAccounts { get; }

        bool AddDataAccount(string dataAccount);

        bool RemoveDataAccount(string dataAccount);

        bool IsReplicated { get; }

        Task SaveAsync();

        Task<bool> ExistsAsync(bool forceRefresh);
    }
}