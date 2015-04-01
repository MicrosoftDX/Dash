//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Dash.Server.Utils
{
    public class HandlerResult
    {
        const string ResponseDateHeader         = "x-ms-date";

        public static HandlerResult Redirect(IHttpRequestWrapper requestWrapper, Uri location)
        {
            return Redirect(requestWrapper, location.ToString());
        }

        public static HandlerResult Redirect(IHttpRequestWrapper requestWrapper, string location)
        {
            return new HandlerResult
            {
                StatusCode = HttpStatusCode.Redirect,
                Location = location,
                Request = requestWrapper,
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

        public IHttpRequestWrapper Request { get; private set; }
        public HttpStatusCode StatusCode { get; set; }
        public string ReasonPhrase { get; set; }
        public string Location { get; private set; }
        public ResponseHeaders Headers { get; set; }
        public DashErrorInformation ErrorInformation { get; set; }

        string _signedLocation;
        public string SignedLocation
        {
            get
            {
                if (String.IsNullOrWhiteSpace(this.Location))
                {
                    throw new InvalidOperationException("Cannot determine signed location until the Location property is set.");
                }
                if (String.IsNullOrWhiteSpace(_signedLocation))
                {
                    // TODO: Handle encryption for SAS requests
                    if (String.IsNullOrWhiteSpace(this.Request.AuthenticationScheme))
                    {
                        return String.Empty;
                    }
                    string responseDate = null;
                    if (this.Headers == null)
                    {
                        responseDate = DateTimeOffset.UtcNow.ToString("r");
                        this.Headers = new ResponseHeaders(new[] {
                            new KeyValuePair<string, string>(ResponseDateHeader, responseDate),
                        });
                    }
                    else if (!this.Headers.Contains(ResponseDateHeader))
                    {
                        responseDate = DateTimeOffset.UtcNow.ToString("r");
                        this.Headers["Date"] = Enumerable.Repeat(responseDate, 1);
                    }
                    else
                    {
                        responseDate = this.Headers.Value<string>(ResponseDateHeader);
                    }
                    _signedLocation = SharedKeySignature.FormatSignatureHeader(this.Request.AuthenticationScheme,
                        SharedKeySignature.GenerateSignature(() => String.Format("{0}\n{1}\n{2}\n{3}",
                                                    this.Request.HttpMethod,
                                                    responseDate,
                                                    this.Location,
                                                    SharedKeySignature.GetCanonicalizedHeaders(this.Headers))));
                }
                return _signedLocation;
            }
        }
    }
}