//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Microsoft.Dash.Server.Controllers;
using Microsoft.Dash.Server.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;

namespace Microsoft.Tests
{
    [TestClass]
    public class ContainerTests
    {
        WebApiTestRunner _runner;

        [TestInitialize]
        public void Init()
        {
            _runner = new WebApiTestRunner(new Dictionary<string, string>()
                {
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestdata1;AccountKey=IatOQyIdf8x3HcCZuhtGGLv/nS0v/SwXu2vBS6E9/5/+GYllhdmFFX6YqMXmR7U6UyFYQt4pdZnlLCM+bPcJ4A==" },
                    { "ScaleoutNumberOfAccounts", "1"},
                });
        }

        [TestMethod]
        public void ContainerLifecycleTest()
        {
            string containerName = Guid.NewGuid().ToString("N");
            string baseUri = "http://mydashserver/container/" + containerName + "?restype=container";
            var results = _runner.ExecuteRequest(baseUri, "PUT", expectedStatusCode: HttpStatusCode.Created);

            //Try to re-create the same container again.
            results = _runner.ExecuteRequest(baseUri, "PUT", expectedStatusCode: HttpStatusCode.Conflict);
            //TODO: Add more variations on create container, including attempt to create already existing container

            //Test insertion of metatags
            var content = new StringContent("", System.Text.Encoding.UTF8, "application/xml");
            content.Headers.Add("x-ms-meta-foo", "fee");
            content.Headers.Add("x-ms-meta-Dog", "Cat");
            results = _runner.ExecuteRequest(baseUri + "&comp=metadata", "PUT");
            List<Tuple<string, string>> customHeaders = new List<Tuple<string, string>>();
            string requestGuid = Guid.NewGuid().ToString("N");
            string appVersion = "2014-02-14";
            string metadataUri = baseUri + "&comp=metadata";
            customHeaders.Add(new Tuple<string, string>("x-ms-meta-foo", "fee"));
            customHeaders.Add(new Tuple<string, string>("x-ms-meta-Dog", "Cat"));
            customHeaders.Add(new Tuple<string, string>("x-ms-client-request-id", requestGuid));
            customHeaders.Add(new Tuple<string, string>("x-ms-version", appVersion));
            results = _runner.ExecuteRequestWithHeaders(metadataUri, "PUT", content, customHeaders, HttpStatusCode.OK);
            Assert.AreEqual(requestGuid, results.Headers.GetValues("x-ms-request-id").First());
            Assert.AreEqual(appVersion, results.Headers.GetValues("x-ms-version").First());

            //Test retrieval of metatags
            customHeaders = new List<Tuple<string, string>>();
            requestGuid = Guid.NewGuid().ToString("N");
            customHeaders.Add(new Tuple<string, string>("x-ms-client-request-id", requestGuid));
            customHeaders.Add(new Tuple<string, string>("x-ms-version", appVersion));
            results = _runner.ExecuteRequestWithHeaders(metadataUri, "GET", content, customHeaders, HttpStatusCode.OK);
            Assert.AreEqual(requestGuid, results.Headers.GetValues("x-ms-request-id").First());
            Assert.AreEqual(appVersion, results.Headers.GetValues("x-ms-version").First());
            Assert.AreEqual("fee", results.Headers.GetValues("x-ms-meta-foo").First());
            Assert.AreEqual("Cat", results.Headers.GetValues("x-ms-meta-Dog").First());

            //Test leasing of container
            string leaseUri = baseUri + "&comp=lease";
            requestGuid = Guid.NewGuid().ToString("N");
            customHeaders = new List<Tuple<string, string>>();
            customHeaders.Add(new Tuple<string, string>("x-ms-client-request-id", requestGuid));
            customHeaders.Add(new Tuple<string, string>("x-ms-version", appVersion));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-action", "acquire"));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-duration", "30")); //30 second lease
            results = _runner.ExecuteRequestWithHeaders(leaseUri, "PUT", content, customHeaders, HttpStatusCode.Created);
            Assert.AreEqual(requestGuid, results.Headers.GetValues("x-ms-request-id").First());
            Assert.AreEqual(appVersion, results.Headers.GetValues("x-ms-version").First());
            Assert.IsNotNull(results.Headers.GetValues("x-ms-lease-id").First());
            string leaseId = results.Headers.GetValues("x-ms-lease-id").First();

            //Test renewal of lease
            requestGuid = Guid.NewGuid().ToString("N");
            customHeaders = new List<Tuple<string, string>>();
            customHeaders.Add(new Tuple<string, string>("x-ms-client-request-id", requestGuid));
            customHeaders.Add(new Tuple<string, string>("x-ms-version", appVersion));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-action", "renew"));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-id", leaseId));
            results = _runner.ExecuteRequestWithHeaders(leaseUri, "PUT", content, customHeaders, HttpStatusCode.OK);
            Assert.AreEqual(requestGuid, results.Headers.GetValues("x-ms-request-id").First());
            Assert.AreEqual(appVersion, results.Headers.GetValues("x-ms-version").First());
            Assert.AreEqual(leaseId, results.Headers.GetValues("x-ms-lease-id").First());

            //Test changing of lease id
            requestGuid = Guid.NewGuid().ToString("N");
            string proposedLeaseId = Guid.NewGuid().ToString("N");
            customHeaders = new List<Tuple<string, string>>();
            customHeaders.Add(new Tuple<string, string>("x-ms-client-request-id", requestGuid));
            customHeaders.Add(new Tuple<string, string>("x-ms-version", appVersion));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-action", "change"));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-id", leaseId));
            customHeaders.Add(new Tuple<string, string>("x-ms-proposed-lease-id", proposedLeaseId));
            results = _runner.ExecuteRequestWithHeaders(leaseUri, "PUT", content, customHeaders, HttpStatusCode.OK);
            Assert.AreEqual(requestGuid, results.Headers.GetValues("x-ms-request-id").First());
            Assert.AreEqual(appVersion, results.Headers.GetValues("x-ms-version").First());
            Assert.AreEqual(proposedLeaseId, results.Headers.GetValues("x-ms-lease-id").First().Replace("-", ""));

            string oldLeaseId = leaseId;
            leaseId = results.Headers.GetValues("x-ms-lease-id").First();

            //Test renewing on now-invalid lease id
            requestGuid = Guid.NewGuid().ToString("N");
            customHeaders = new List<Tuple<string, string>>();
            customHeaders.Add(new Tuple<string, string>("x-ms-client-request-id", requestGuid));
            customHeaders.Add(new Tuple<string, string>("x-ms-version", appVersion));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-action", "renew"));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-id", oldLeaseId));
            results = _runner.ExecuteRequestWithHeaders(leaseUri, "PUT", content, customHeaders, HttpStatusCode.PreconditionFailed);

            //Test breaking of lease
            requestGuid = Guid.NewGuid().ToString("N");
            customHeaders = new List<Tuple<string, string>>();
            customHeaders.Add(new Tuple<string, string>("x-ms-client-request-id", requestGuid));
            customHeaders.Add(new Tuple<string, string>("x-ms-version", appVersion));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-action", "break"));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-id", leaseId));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-break-period", "0"));
            results = _runner.ExecuteRequestWithHeaders(leaseUri, "PUT", content, customHeaders, HttpStatusCode.Accepted);
            Assert.AreEqual(requestGuid, results.Headers.GetValues("x-ms-request-id").First());
            Assert.AreEqual(appVersion, results.Headers.GetValues("x-ms-version").First());
            Assert.AreEqual("0", results.Headers.GetValues("x-ms-lease-time").First());

            //Test leasing and then releasing the lease
            requestGuid = Guid.NewGuid().ToString("N");
            customHeaders = new List<Tuple<string, string>>();
            customHeaders.Add(new Tuple<string, string>("x-ms-client-request-id", requestGuid));
            customHeaders.Add(new Tuple<string, string>("x-ms-version", appVersion));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-action", "acquire"));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-duration", "30")); //30 second lease
            results = _runner.ExecuteRequestWithHeaders(leaseUri, "PUT", content, customHeaders, HttpStatusCode.Created);
            Assert.AreEqual(requestGuid, results.Headers.GetValues("x-ms-request-id").First());
            Assert.AreEqual(appVersion, results.Headers.GetValues("x-ms-version").First());
            Assert.IsNotNull(results.Headers.GetValues("x-ms-lease-id").First());
            leaseId = results.Headers.GetValues("x-ms-lease-id").First();

            //Now release the lease
            requestGuid = Guid.NewGuid().ToString("N");
            customHeaders = new List<Tuple<string, string>>();
            customHeaders.Add(new Tuple<string, string>("x-ms-client-request-id", requestGuid));
            customHeaders.Add(new Tuple<string, string>("x-ms-version", appVersion));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-action", "release"));
            customHeaders.Add(new Tuple<string, string>("x-ms-lease-id", leaseId));
            results = _runner.ExecuteRequestWithHeaders(leaseUri, "PUT", content, customHeaders, HttpStatusCode.OK);
            Assert.AreEqual(requestGuid, results.Headers.GetValues("x-ms-request-id").First());
            Assert.AreEqual(appVersion, results.Headers.GetValues("x-ms-version").First());

            //Test setting of ACLs
            string aclUri = baseUri + "&comp=acl";
            customHeaders = new List<Tuple<string, string>>();
            requestGuid = Guid.NewGuid().ToString("N");
            customHeaders.Add(new Tuple<string, string>("x-ms-client-request-id", requestGuid));
            customHeaders.Add(new Tuple<string, string>("x-ms-version", appVersion));
            customHeaders.Add(new Tuple<string, string>("x-ms-blob-public-access", "container"));
            string policyGuid = Guid.NewGuid().ToString("N");
            string xmlBody = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
            xmlBody += "<SignedIdentifiers>";
            xmlBody += "<SignedIdentifier>";
            xmlBody += "<Id>" + policyGuid + "</Id>";
            xmlBody += "<AccessPolicy>";
            xmlBody += "<Start>" + DateTimeOffset.UtcNow.ToString() + "</Start>";
            xmlBody += "<Expiry>" + (DateTimeOffset.UtcNow + new TimeSpan(0, 0, 30)).ToString() + "</Expiry>";
            xmlBody += "<Permission>r</Permission>";
            xmlBody += "</AccessPolicy>";
            xmlBody += "</SignedIdentifier>";
            xmlBody += "</SignedIdentifiers>";
            var xmlContent = new StringContent(xmlBody, System.Text.Encoding.UTF8, "application/xml");
            results = _runner.ExecuteRequestWithHeaders(aclUri, "PUT", xmlContent, customHeaders, HttpStatusCode.OK);
            Assert.AreEqual(requestGuid, results.Headers.GetValues("x-ms-request-id").First());
            Assert.AreEqual(appVersion, results.Headers.GetValues("x-ms-version").First());

            //Test getting of ACLs
            customHeaders = new List<Tuple<string, string>>();
            requestGuid = Guid.NewGuid().ToString("N");
            customHeaders.Add(new Tuple<string, string>("x-ms-client-request-id", requestGuid));
            customHeaders.Add(new Tuple<string, string>("x-ms-version", appVersion));
            results = _runner.ExecuteRequestWithHeaders(aclUri, "GET", content, customHeaders, HttpStatusCode.OK);
            Assert.AreEqual(requestGuid, results.Headers.GetValues("x-ms-request-id").First());
            Assert.AreEqual(appVersion, results.Headers.GetValues("x-ms-version").First());
            Assert.AreEqual("container", results.Headers.GetValues("x-ms-blob-public-access").First());
            XDocument xmlResponse = XDocument.Load(results.Content.ReadAsStreamAsync().Result);
            var enumerationResults = xmlResponse.Root;
            var firstPolicy = (XElement)enumerationResults.Element("SignedIdentifier");
            Assert.AreEqual(policyGuid, firstPolicy.Element("Id").Value);
            var accessPolicy = firstPolicy.Element("AccessPolicy");
            Assert.AreEqual("r", accessPolicy.Element("Permission").Value);

            //Test deletion of container (and cleanup)
            results = _runner.ExecuteRequest(baseUri, "DELETE", expectedStatusCode: HttpStatusCode.Accepted);
        }

        [TestMethod]
        public void DeleteNonExistentContainerTest()
        {
            string containerName = Guid.NewGuid().ToString("N");
            string baseUri = "http://mydashserver/container/" + containerName + "?restype=container";
            var results = _runner.ExecuteRequest(baseUri, "DELETE", expectedStatusCode: HttpStatusCode.NotFound);
        }

    }
}
