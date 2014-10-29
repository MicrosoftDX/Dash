//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Microsoft.Dash.Server.Handlers
{
    public enum StorageOperationTyes
    {
        Unknown,
        GetPutBlob,
        BlobProperties,
        BlobMetadata,
        BlobLease,
        BlobSnapshot,
        BlobBlock,
        BlobBlockList,
    }

    public static class StorageOperations
    {
        // Operation parameter selectors
        public const string OperationNameProperties     = "properties";
        public const string OperationNameMetadata       = "metadata";
        public const string OperationNameLease          = "lease";
        public const string OperationNameSnapshot       = "snapshot";
        public const string OperationNameBlobBlock      = "block";
        public const string OperationNameBlobBlockList  = "blocklist";

        public static StorageOperationTyes GetBlobOperationFromCompParam(string compParam)
        {
            if (String.IsNullOrWhiteSpace(compParam))
            {
                return StorageOperationTyes.GetPutBlob;
            }
            switch (compParam.ToLower())
            {
                case OperationNameProperties:
                    return StorageOperationTyes.BlobProperties;

                case OperationNameMetadata:
                    return StorageOperationTyes.BlobMetadata;

                case OperationNameLease:
                    return StorageOperationTyes.BlobLease;

                case OperationNameSnapshot:
                    return StorageOperationTyes.BlobSnapshot;

                case OperationNameBlobBlock:
                    return StorageOperationTyes.BlobBlock;

                case OperationNameBlobBlockList:
                    return StorageOperationTyes.BlobBlockList;

                default:
                    System.Diagnostics.Debug.Assert(false);
                    return StorageOperationTyes.Unknown;
            }
        }

    }
}