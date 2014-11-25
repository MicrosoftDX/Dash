//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Dash.Server.Utils
{
    public class HandlerResult
    {
        public static HandlerResult Redirect(Uri location)
        {
            return Redirect(location.ToString());
        }

        public static HandlerResult Redirect(string location)
        {
            return new HandlerResult
            {
                StatusCode = HttpStatusCode.Redirect,
                Location = location,
            };
        }

        public static HandlerResult FromException(StorageException ex)
        {
            return new HandlerResult
            {
                StatusCode = (HttpStatusCode)ex.RequestInformation.HttpStatusCode,
                ErrorInformation = new DashErrorInformation(ex.RequestInformation.ExtendedErrorInformation),
            };
        }

        public HttpStatusCode StatusCode { get; set; }
        public string Location { get; set; }
        public ResponseHeaders Headers { get; set; }
        public DashErrorInformation ErrorInformation { get; set; }
    }

    public class DashErrorInformation
    {
        public DashErrorInformation()
        {
            this.AdditionalDetails = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public DashErrorInformation(StorageExtendedErrorInformation src)
        {
            this.ErrorCode = src.ErrorCode;
            this.ErrorMessage = src.ErrorMessage;
            this.AdditionalDetails = src.AdditionalDetails;
        }

        public IDictionary<string, string> AdditionalDetails { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }
}