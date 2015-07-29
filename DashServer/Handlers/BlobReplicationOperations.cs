//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Dash.Server.Handlers
{
    public class BlobReplicationOperations
    {
        static readonly HashSet<StorageOperationTypes> _replicationTriggerOperations = new HashSet<StorageOperationTypes>()
        {
            StorageOperationTypes.PutBlob,
            StorageOperationTypes.PutBlockList,
            StorageOperationTypes.PutPage,
            StorageOperationTypes.SetBlobMetadata,
            StorageOperationTypes.SetBlobProperties,
            StorageOperationTypes.DeleteBlob,
            StorageOperationTypes.CopyBlob,
        };

        public static bool DoesOperationTriggerReplication(StorageOperationTypes operation)
        {
            return _replicationTriggerOperations.Contains(operation);
        }
    }
}