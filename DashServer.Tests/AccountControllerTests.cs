//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Xml.Linq;

namespace Microsoft.Tests
{
    [TestClass]
    public class AccountControllerTests
    {
        WebApiTestRunner _runner;

        [TestInitialize]
        public void Init()
        {
            _runner = new WebApiTestRunner(new Dictionary<string, string>()
                {
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestconstant1;AccountKey=Q32sd0MkbWpMfBb0A3Zjg8LhSI0VJblT+CyAbXkczI2rWNYIrsoaQjc7ba1z5w+KOpJtxl/h3vA20WsbENM6hQ==" },
                    { "ScaleoutStorage1", "DefaultEndpointsProtocol=https;AccountName=dashtestconstant2;AccountKey=DHkdb1s/P0K0bDGJ5CaAGjN9HTv7UL1mZ9nriYn0bOkeX0V9qVaDqVp3RjPoJ6CnKarzhGGd4+H84D+ureNisA==" },
                });
        }

        [TestMethod]
        public void ListContainersControllerTest()
        {
            // Default version (2009-09-19)
            var response = _runner.ExecuteRequest("http://mydashserver/account?comp=list",
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            var doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            var enumerationResults = doc.Root;
            Assert.AreEqual("http://mydashserver", enumerationResults.Attribute("AccountName").Value);
            Assert.IsNull(enumerationResults.Element("Prefix"));
            Assert.IsNull(enumerationResults.Element("Marker"));
            Assert.IsNull(enumerationResults.Element("MaxResults"));
            Assert.IsNotNull(enumerationResults.Element("Containers"));
            var containers = enumerationResults.Element("Containers");
            Assert.AreEqual(3, containers.Elements().Count());
            var container = containers.Element("Container");
            Assert.AreEqual("anonymousblobtest", container.Element("Name").Value);
            Assert.AreEqual("http://mydashserver/anonymousblobtest", container.Element("Url").Value);
            Assert.IsNotNull(container.Element("Properties"));
            Assert.IsNull(container.Element("Properties").Element("LeaseStatus"));
            container = containers.Elements("Container").ElementAt(1);
            Assert.AreEqual("anonymouscontainertest", container.Element("Name").Value);
            Assert.AreEqual("test", containers.Elements("Container").ElementAt(2).Element("Name").Value);

            // Explicit version
            response = _runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list",
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            enumerationResults = doc.Root;
            Assert.AreEqual("http://mydashserver", enumerationResults.Attribute("ServiceEndpoint").Value);
            container = enumerationResults.Element("Containers").Element("Container");
            Assert.IsNotNull(container.Element("Properties").Element("LeaseStatus"));
            Assert.IsNull(container.Element("Metadata"));

            // Include metadata
            response = _runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list&include=metadata",
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            enumerationResults = doc.Root;
            container = enumerationResults.Element("Containers").Element("Container");
            Assert.IsNotNull(container.Element("Metadata"));
        }

        [TestMethod]
        public void ListContainersPagedControllerTest()
        {
            var response = _runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list&maxresults=2",
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            var doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            var enumerationResults = doc.Root;
            Assert.IsNull(enumerationResults.Element("Prefix"));
            Assert.IsNull(enumerationResults.Element("Marker"));
            Assert.IsNotNull(enumerationResults.Element("MaxResults"));
            Assert.IsNotNull(enumerationResults.Element("NextMarker"));
            var containers = enumerationResults.Element("Containers");
            Assert.AreEqual(2, containers.Elements().Count());
            string nextMarker = enumerationResults.Element("NextMarker").Value;
            response = _runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list&marker=" + nextMarker,
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            enumerationResults = doc.Root;
            Assert.IsNotNull(enumerationResults.Element("Marker"));
            Assert.IsNull(enumerationResults.Element("MaxResults"));
            Assert.IsNull(enumerationResults.Element("NextMarker"));
            containers = enumerationResults.Element("Containers");
            Assert.AreEqual(1, containers.Elements().Count());
            var container = containers.Element("Container");
            Assert.AreEqual("test", container.Element("Name").Value);
        }

        [TestMethod]
        public void ListContainersPrefixControllerTest()
        {
            var response = _runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list&prefix=anonymous",
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            var doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            var enumerationResults = doc.Root;
            Assert.AreEqual("anonymous", enumerationResults.Element("Prefix").Value);
            Assert.IsNull(enumerationResults.Element("Marker"));
            Assert.IsNull(enumerationResults.Element("MaxResults"));
            Assert.IsNull(enumerationResults.Element("NextMarker"));
            var containers = enumerationResults.Element("Containers");
            Assert.AreEqual(2, containers.Elements().Count());

            // Combine prefix & paging
            response = _runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list&maxresults=1&prefix=anonymous",
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            enumerationResults = doc.Root;
            Assert.AreEqual("anonymous", enumerationResults.Element("Prefix").Value);
            Assert.IsNull(enumerationResults.Element("Marker"));
            Assert.IsNotNull(enumerationResults.Element("MaxResults"));
            Assert.IsNotNull(enumerationResults.Element("NextMarker"));
            containers = enumerationResults.Element("Containers");
            Assert.AreEqual(1, containers.Elements().Count());
            string nextMarker = enumerationResults.Element("NextMarker").Value;
            response = _runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list&prefix=anonymous&marker=" + nextMarker,
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            enumerationResults = doc.Root;
            Assert.AreEqual("anonymous", enumerationResults.Element("Prefix").Value);
            Assert.IsNotNull(enumerationResults.Element("Marker"));
            Assert.IsNull(enumerationResults.Element("MaxResults"));
            Assert.IsNull(enumerationResults.Element("NextMarker"));
            containers = enumerationResults.Element("Containers");
            Assert.AreEqual(1, containers.Elements().Count());
            var container = containers.Element("Container");
            Assert.AreEqual("anonymouscontainertest", container.Element("Name").Value);
        }

        [TestMethod]
        public void GetServicePropertiesControllerTest()
        {
            // Default version (2009-09-19)
            var response = _runner.ExecuteRequest("http://localhost/account?restype=service&comp=properties",
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            var doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            var serviceProps = doc.Root;
            Assert.IsNotNull(serviceProps.Element("Logging"));
            Assert.IsNotNull(serviceProps.Element("Logging").Element("RetentionPolicy"));
            Assert.IsNotNull(serviceProps.Element("Metrics"));
            Assert.IsNotNull(serviceProps.Element("Metrics").Element("RetentionPolicy"));
            // Explicit version
            response = _runner.ExecuteRequestWithHeaders("http://localhost/account?restype=service&comp=properties",
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            serviceProps = doc.Root;
            Assert.IsNotNull(serviceProps.Element("Logging"));
            Assert.IsNotNull(serviceProps.Element("Logging").Element("RetentionPolicy"));
            Assert.IsNotNull(serviceProps.Element("HourMetrics"));
            Assert.IsNotNull(serviceProps.Element("HourMetrics").Element("RetentionPolicy"));
            Assert.IsNotNull(serviceProps.Element("MinuteMetrics"));
            Assert.IsNotNull(serviceProps.Element("MinuteMetrics").Element("RetentionPolicy"));
            Assert.IsNotNull(serviceProps.Element("Cors"));
        }

    }
}
