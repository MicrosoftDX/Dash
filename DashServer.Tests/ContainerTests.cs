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

            var content = new StringContent("", System.Text.Encoding.UTF8, "application/xml");
            content.Headers.Add("x-ms-meta-foo", "fee");
            content.Headers.Add("x-ms-meta-Dog", "Cat");
            results = _runner.ExecuteRequest(baseUri + "&comp=metadata", "PUT");
            //Assert.AreEqual(HttpStatusCode.OK, results.StatusCode, "Expected OK result");

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
