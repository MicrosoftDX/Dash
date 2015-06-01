//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net.Http;
using Microsoft.Dash.Common.Utils;

namespace Microsoft.Dash.Server.Utils
{
    public class DelegatedResponse : SimpleHttpResponse
    {
        public DelegatedResponse(SimpleHttpResponse src)
        {
            this.StatusCode = src.StatusCode;
            this.ReasonPhrase = src.ReasonPhrase;
        }

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