using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using Microsoft.Dash.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Tests
{
    [TestClass]
    public class IntegrationTests
    {
        public TestContext TestContext { get; set; }

        public string Endpoint = "http://japoondash.cloudapp.net";
       // public string Endpoint = "http://localhost:23161/";
        public string AccountName = "japoondash";
        public string AccountKey = "wCNvIdXcltACBiDUMyO0BflZpKmjseplqOlzE62tx87qnkwpUMBV/GQhrscW9lmdZVT0x8DilYqUoHMNBlVIGg==";

        public IntegrationTests()
        {
            DashConfiguration.ConfigurationSource = new TestConfigurationProvider(new Dictionary<string, string>
            {
                { "AccountName" , AccountName },
                { "AccountKey", AccountKey }
            });
        }

        [TestMethod]
        public void PutGetDeleteBlob()
        {
            // put container
            var requestHelper = new RequestHelper(Endpoint + "/anonymouscontainertest?restype=container", HttpMethod.Put, AccountName);
            var response = requestHelper.Send();
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
             
            // put blob
            string blobName = String.Format("/anonymouscontainertest/{0}", Guid.NewGuid());
            requestHelper = new RequestHelper(Endpoint + blobName, HttpMethod.Put, AccountName);
            requestHelper.Request.Headers.Add("x-ms-blob-type", "BlockBlob");

            var payload = Encoding.UTF8.GetBytes("put-get-delete-blob-integration-test");
            var stream = new MemoryStream(payload);
            requestHelper.Request.Content = new StreamContent(stream);
            response = requestHelper.Send();
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            // get blob
            requestHelper = new RequestHelper(Endpoint + blobName, HttpMethod.Get, AccountName);
            response = requestHelper.Send();
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("put-get-delete-blob-integration-test", response.Content.ReadAsStringAsync().Result);

            // delete container
            requestHelper = new RequestHelper(Endpoint + "/anonymouscontainertest?restype=container", HttpMethod.Delete, AccountName);
            response = requestHelper.Send();
            Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
        }

        public class RequestHelper
        {
            public HttpRequestMessage Request { get; set; }
            private readonly string _accountName;

            public RequestHelper(string path, HttpMethod method, string accountName)
            {
                this._accountName = accountName;

                Request = new HttpRequestMessage(method, path);
                Request.Headers.Add("x-ms-version", "2014-02-14");
                Request.Headers.Add("x-ms-client-request-id", "adfb540e-1050-4c9b-a53a-be6cb71688d3");

                var requestDate = DateTime.UtcNow.ToString("R");
                Request.Headers.Add("x-ms-date", requestDate);
            }

            public HttpResponseMessage Send()
            {
                // signature
                var signature = RequestAuthorization.GenerateSharedKeySignature(true, true, Request.Method.ToString(),
                    Request.RequestUri.AbsolutePath,
                    RequestHeaders.Create(Request), RequestQueryParameters.Create(Request), String.Empty, String.Empty);
                var authorization = String.Format("SharedKeyLite {0}:{1}",
                    _accountName,
                    signature);
                Request.Headers.Add(HttpRequestHeader.Authorization.ToString(), authorization);

                using (var httpClient = new HttpClient())
                {
                    return httpClient.SendAsync(Request, HttpCompletionOption.ResponseContentRead).Result;
                }
            }
        }

    }
}
