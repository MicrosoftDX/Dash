//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;

namespace Microsoft.Dash.Server.Utils
{
    public class DelegatedResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public string ReasonPhrase { get; set; }

        public HttpResponseMessage CreateResponse()
        {
            var response = new HttpResponseMessage(this.StatusCode);
            if (!String.IsNullOrEmpty(this.ReasonPhrase))
            {
                response.ReasonPhrase = this.ReasonPhrase;
            }
            return response;
        }
    }
}