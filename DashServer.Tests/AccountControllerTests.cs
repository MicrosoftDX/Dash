//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Tests
{
    [TestClass]
    public class AccountControllerTests : DashTestBase
    {
        static string _containerPrefix;
        static DashTestContext _ctx;
        static DashTestContext _ctx2;

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            _containerPrefix = Guid.NewGuid().ToString().Substring(0, 8);
            _ctx = InitializeConfigAndCreateTestBlobs(ctx, "datax2", new Dictionary<string, string>(), new TestBlob[] { }, _containerPrefix);           // We just need the container
            _ctx2 = InitializeConfigAndCreateTestBlobs(ctx, "datax2", new Dictionary<string, string>(), new TestBlob[] { }, _containerPrefix);          // We just need the container
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            CleanupTestBlobs(_ctx);
            CleanupTestBlobs(_ctx2);
        }

        [TestMethod]
        public void ListContainersControllerTest()
        {
            // Default version (2009-09-19)
            var response = _ctx.Runner.ExecuteRequest("http://mydashserver/account?comp=list",
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
            Assert.IsTrue(containers.Elements().Count() >= 2);
            var container = containers
                .Elements()
                .FirstOrDefault(element => String.Equals(_ctx.ContainerName, element.Element("Name").Value, StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(container);
            Assert.AreEqual(new Uri(_ctx.GetContainerUri(false)).GetLeftPart(UriPartial.Path), container.Element("Url").Value);
            Assert.IsNotNull(container.Element("Properties"));
            Assert.IsNull(container.Element("Properties").Element("LeaseStatus"));

            Assert.IsTrue(containers
                .Elements()
                .Any(element => String.Equals(_ctx2.ContainerName, element.Element("Name").Value, StringComparison.OrdinalIgnoreCase)));

            // Explicit version
            response = _ctx.Runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list",
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
            response = _ctx.Runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list&include=metadata",
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
            var response = _ctx.Runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list&maxresults=1",
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
            Assert.AreEqual(1, containers.Elements().Count());

            bool seenTestContainer = containers
                .Elements()
                .Any(element => String.Equals(_ctx2.ContainerName, element.Element("Name").Value, StringComparison.OrdinalIgnoreCase));

            string nextMarker = enumerationResults.Element("NextMarker").Value;
            response = _ctx.Runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list&marker=" + nextMarker,
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
            Assert.IsTrue(containers.Elements().Count() >= 1);

            seenTestContainer |= containers
                .Elements()
                .Any(element => String.Equals(_ctx2.ContainerName, element.Element("Name").Value, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(seenTestContainer);
        }

        [TestMethod]
        public void ListContainersPrefixControllerTest()
        {
            var response = _ctx.Runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list&prefix=" + _ctx.ContainerName,
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            var doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            var enumerationResults = doc.Root;
            Assert.AreEqual(_ctx.ContainerName, enumerationResults.Element("Prefix").Value);
            Assert.IsNull(enumerationResults.Element("Marker"));
            Assert.IsNull(enumerationResults.Element("MaxResults"));
            Assert.IsNull(enumerationResults.Element("NextMarker"));
            var containers = enumerationResults.Element("Containers");
            Assert.AreEqual(1, containers.Elements().Count());

            // Combine prefix & paging
            response = _ctx.Runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list&maxresults=1&prefix=" + _containerPrefix,
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            enumerationResults = doc.Root;
            Assert.AreEqual(_containerPrefix, enumerationResults.Element("Prefix").Value);
            Assert.IsNull(enumerationResults.Element("Marker"));
            Assert.IsNotNull(enumerationResults.Element("MaxResults"));
            Assert.IsNotNull(enumerationResults.Element("NextMarker"));
            containers = enumerationResults.Element("Containers");
            Assert.AreEqual(1, containers.Elements().Count());

            var container = containers.Element("Container");
            bool container1 = String.Equals(_ctx.ContainerName, container.Element("Name").Value);
            bool container2 = String.Equals(_ctx2.ContainerName, container.Element("Name").Value);

            string nextMarker = enumerationResults.Element("NextMarker").Value;
            response = _ctx.Runner.ExecuteRequestWithHeaders("http://mydashserver/account?comp=list&prefix=" + _containerPrefix + "&marker=" + nextMarker,
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            enumerationResults = doc.Root;
            Assert.AreEqual(_containerPrefix, enumerationResults.Element("Prefix").Value);
            Assert.IsNotNull(enumerationResults.Element("Marker"));
            Assert.IsNull(enumerationResults.Element("MaxResults"));
            Assert.IsNull(enumerationResults.Element("NextMarker"));
            containers = enumerationResults.Element("Containers");
            Assert.AreEqual(1, containers.Elements().Count());

            container = containers.Element("Container");
            container1 |= String.Equals(_ctx.ContainerName, container.Element("Name").Value);
            container2 |= String.Equals(_ctx2.ContainerName, container.Element("Name").Value);

            Assert.IsTrue(container1);
            Assert.IsTrue(container2);
        }

        [TestMethod]
        public void GetServicePropertiesControllerTest()
        {
            // Default version (2009-09-19)
            var response = _ctx.Runner.ExecuteRequest("http://localhost/account?restype=service&comp=properties",
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            var doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            var serviceProps = doc.Root;
            Assert.IsNotNull(serviceProps.Element("Logging"));
            Assert.IsNotNull(serviceProps.Element("Logging").Element("RetentionPolicy"));
            Assert.IsNotNull(serviceProps.Element("Metrics"));
            Assert.IsNotNull(serviceProps.Element("Metrics").Element("RetentionPolicy"));
            // Explicit version
            response = _ctx.Runner.ExecuteRequestWithHeaders("http://localhost/account?restype=service&comp=properties",
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
