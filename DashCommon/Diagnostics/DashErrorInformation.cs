//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Dash.Common.Diagnostics
{
    public class DashErrorInformation
    {
        public static DashErrorInformation Create(StorageExtendedErrorInformation src)
        {
            if (src != null)
            {
                return new DashErrorInformation
                {
                    ErrorCode = src.ErrorCode,
                    ErrorMessage = src.ErrorMessage,
                    AdditionalDetails = src.AdditionalDetails,
                };
            }
            return null;
        }

        public DashErrorInformation()
        {
            this.AdditionalDetails = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public IDictionary<string, string> AdditionalDetails { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }
}