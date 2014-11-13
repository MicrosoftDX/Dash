//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Microsoft.Dash.Server.Utils
{
    public class RequestUriParts
    {
        public const string ControllerAccount   = "account";
        public const string ControllerContainer = "container";
        public const string ControllerBlob      = "blob";

        public static RequestUriParts Create(Uri uri)
        {
            // URI format is:
            //  /mvc-controller[/container[/blobseg1/blobseg2/.../blobsegn]]
            var uriSegments = uri.Segments
                .Select(segment => segment.Trim('/'))
                .Where(segment => !String.IsNullOrWhiteSpace(segment))
                .ToArray();
            return new RequestUriParts
            {
                Controller = uriSegments.FirstOrDefault(),
                Container = uriSegments.Skip(1).FirstOrDefault(),
                BlobName = String.Join("/", uriSegments.Skip(2)),
            };
        }

        private RequestUriParts()
        {

        }

        public string Controller { get; private set; }
        public string Container { get; private set; }
        public string BlobName { get; private set; }

        public bool IsAccountRequest
        {
            get
            {
                return String.Equals(this.Controller, ControllerAccount, StringComparison.OrdinalIgnoreCase) &&
                    String.IsNullOrWhiteSpace(this.Container);
            }
        }

        public bool IsContainerRequest
        {
            get
            {
                return String.Equals(this.Controller, ControllerContainer, StringComparison.OrdinalIgnoreCase) &&
                    !String.IsNullOrWhiteSpace(this.Container) &&
                    String.IsNullOrWhiteSpace(this.BlobName);
            }
        }

        public bool IsBlobRequest
        {
            get
            {
                return String.Equals(this.Controller, ControllerBlob, StringComparison.OrdinalIgnoreCase) &&
                    !String.IsNullOrWhiteSpace(this.BlobName);
            }
        }

        public string PublicUriPath
        {
            get
            {
                return "/" + this.Container + (String.IsNullOrWhiteSpace(this.Container) ? String.Empty : "/" + this.BlobName);
            }
        }
    }
}