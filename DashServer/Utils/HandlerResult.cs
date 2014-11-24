//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;

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

        public HttpStatusCode StatusCode { get; set; }
        public string Location { get; set; }
    }
}