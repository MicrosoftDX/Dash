//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Microsoft.Dash.Server.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Dash.Server.Handlers
{
    public static class BlobHandler
    {
        /// <summary>
        /// Generic function to redirect a put request for properties of a blob
        /// </summary>
        public static async Task<HandlerResult> BasicBlobAsync(string container, string blob)
        {
            var namespaceBlob = await ControllerOperations.FetchNamespaceBlobAsync(container, blob);
            if (!await namespaceBlob.ExistsAsync())
            {
                return new HandlerResult
                {
                    StatusCode = HttpStatusCode.NotFound,
                };
            }
            return HandlerResult.Redirect(ControllerOperations.GetRedirectUri(HttpContextFactory.Current.Request,
                DashConfiguration.GetDataAccountByAccountName(namespaceBlob.AccountName),
                namespaceBlob.Container,
                namespaceBlob.BlobName));
        }

        public static async Task<HandlerResult> PutBlobAsync(string container, string blob)
        {
            var namespaceBlob = await ControllerOperations.CreateNamespaceBlobAsync(container, blob);
            //redirection code
            Uri redirect = ControllerOperations.GetRedirectUri(HttpContextFactory.Current.Request, 
                DashConfiguration.GetDataAccountByAccountName(namespaceBlob.AccountName), 
                container, 
                blob);
            return HandlerResult.Redirect(redirect);
        }
    }
}