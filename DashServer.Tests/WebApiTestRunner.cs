//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Web;
using System.Web.Http;
using System.Xml.Linq;
using Microsoft.Dash.Server;
using Microsoft.Dash.Server.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Tests
{
    class WebApiTestRunner 
    {
        HttpClient _requestClient;

        public WebApiTestRunner(IDictionary<string, string> config = null)
        {
            var webApiConfig = new HttpConfiguration();
            WebApiConfig.Register(webApiConfig);
            var server = new HttpServer(webApiConfig);
            _requestClient = new HttpClient(server);

            if (config != null)
            {
                InitializeConfig(config);
            }
        }

        public static void InitializeConfig(IDictionary<string, string> config)
        {
            DashConfiguration.ConfigurationSource = new TestConfigurationProvider(config);
        }

        public static void SetupRequest(string uri, string method)
        {
            var requestUri = new Uri(uri);
            var request = new HttpRequest("", requestUri.GetLeftPart(UriPartial.Path), requestUri.Query);
            var httpMethodField = typeof(HttpRequest).GetField("_httpMethod", BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance);
            httpMethodField.SetValue(request, method);

            HttpContextFactory.Current = new HttpContextWrapper(new HttpContext(
                request,
                new HttpResponse(null)));
        }

        public HttpResponseMessage ExecuteRequest(string uri, string method, HttpContent content, HttpStatusCode expectedStatusCode = HttpStatusCode.Unused)
        {
            return ExecuteRequestWithHeaders(uri, method, content, null, expectedStatusCode);
        }

        public HttpResponseMessage ExecuteRequestWithHeaders(string uri, string method, HttpContent content, IEnumerable<Tuple<string, string>> headers, HttpStatusCode expectedStatusCode = HttpStatusCode.Unused)
        {
            HttpResponseMessage retval = null;
            SetupRequest(uri, method);
            switch (method)
            {
                case "GET":
                    retval = _requestClient.GetAsync(uri).Result;
                    break;

                case "HEAD":
                    retval = _requestClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, uri)).Result;
                    break;

                case "PUT":
                    var request = new HttpRequestMessage(HttpMethod.Put, uri);
                    if (content != null)
                    {
                        request.Content = content;
                    }
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            if (!request.Headers.TryAddWithoutValidation(header.Item1, header.Item2) && request.Content != null)
                            {
                                request.Content.Headers.TryAddWithoutValidation(header.Item1, header.Item2);
                            }
                        }
                    }
                    retval = _requestClient.SendAsync(request).Result;
                    break;

                case "DELETE":
                    retval = _requestClient.DeleteAsync(uri).Result;
                    break;

                default:
                    // Unsupported method
                    System.Diagnostics.Debug.Assert(false);
                    break;
            }
            if (expectedStatusCode != HttpStatusCode.Unused && retval != null)
            {
                Assert.AreEqual(expectedStatusCode, retval.StatusCode);
            }
            return retval;
        }

        public HttpResponseMessage ExecuteRequest(string uri, string method, XDocument body = null, HttpStatusCode expectedStatusCode = HttpStatusCode.Unused)
        {
            HttpContent bodycontent = null;
            if (body != null)
            {
                bodycontent = new StringContent(body.ToString(SaveOptions.OmitDuplicateNamespaces));
                bodycontent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
            }
            return ExecuteRequest(uri, method, bodycontent, expectedStatusCode);
        }

        public XDocument ExecuteRequestResponse(string uri, string method, XDocument body = null, HttpStatusCode expectedStatusCode = HttpStatusCode.Unused)
        {
            var response = ExecuteRequest(uri, method, body);
            return XDocument.Load(response.Content.ReadAsStreamAsync().Result);
        }
    }
}
