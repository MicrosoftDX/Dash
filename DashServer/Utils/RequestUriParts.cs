//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dash.Common.Utils;

namespace Microsoft.Dash.Server.Utils
{
    public class RequestUriParts
    {
        public const string ControllerAccount   = "account";
        public const string ControllerContainer = "container";
        public const string ControllerBlob      = "blob";

        public static RequestUriParts Create(IEnumerable<string> uriSegments, IEnumerable<string> originalSegments)
        {
            // URI format is:
            //  /mvc-controller[/container[/blobseg1/blobseg2/.../blobsegn]]
            return new RequestUriParts
            {
                Controller = uriSegments.FirstOrDefault(),
                Container = uriSegments.Skip(1).FirstOrDefault(),
                BlobName = PathUtils.CombinePathSegments(uriSegments.Skip(2)),
                OriginalContainer = originalSegments.Skip(1).FirstOrDefault(),
                OriginalBlobName = PathUtils.CombinePathSegments(originalSegments.Skip(2)),
            };
        }

        private RequestUriParts()
        {

        }

        public string Controller { get; private set; }
        public string Container { get; private set; }
        public string BlobName { get; private set; }
        public string OriginalContainer { get; private set; }
        public string OriginalBlobName { get; private set; }

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
            get { return GetPath(this.Container, this.BlobName); }
        }

        public string OriginalUriPath
        {
            get { return GetPath(this.OriginalContainer, this.OriginalBlobName); }
        }

        private static string GetPath(string container, string blobName)
        {
            return PathUtils.CombineContainerAndBlob(container, blobName, true);
        }
    }
}