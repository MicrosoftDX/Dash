﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Microsoft.Dash.Server.Handlers
{
    public enum StorageOperationTypes
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

        public static StorageOperationTypes GetBlobOperationFromCompParam(string compParam)
        {
            if (String.IsNullOrWhiteSpace(compParam))
            {
                return StorageOperationTypes.GetPutBlob;
            }
            switch (compParam.ToLower())
            {
                case OperationNameProperties:
                    return StorageOperationTypes.BlobProperties;

                case OperationNameMetadata:
                    return StorageOperationTypes.BlobMetadata;

                case OperationNameLease:
                    return StorageOperationTypes.BlobLease;

                case OperationNameSnapshot:
                    return StorageOperationTypes.BlobSnapshot;

                case OperationNameBlobBlock:
                    return StorageOperationTypes.BlobBlock;

                case OperationNameBlobBlockList:
                    return StorageOperationTypes.BlobBlockList;

                default:
                    System.Diagnostics.Debug.Assert(false);
                    return StorageOperationTypes.Unknown;
            }
        }

    }
}