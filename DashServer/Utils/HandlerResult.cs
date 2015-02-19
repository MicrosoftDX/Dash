//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using Microsoft.Dash.Server.Diagnostics;
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
                ReasonPhrase = ex.RequestInformation.HttpStatusMessage,
                ErrorInformation = DashErrorInformation.Create(ex.RequestInformation.ExtendedErrorInformation),
            };
        }

        public HttpStatusCode StatusCode { get; set; }
        public string ReasonPhrase { get; set; }
        public string Location { get; set; }
        public ResponseHeaders Headers { get; set; }
        public DashErrorInformation ErrorInformation { get; set; }
    }
}