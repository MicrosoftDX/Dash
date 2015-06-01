//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;
using Microsoft.Dash.Common.Processors;
using Microsoft.Dash.Common.Utils;

namespace Microsoft.Tests
{
    [TestClass]
    public class AccountManagementTests : PipelineTestBase
    {
        WebApiTestRunner _runner = new WebApiTestRunner();

        [TestInitialize]
        public void Init()
        {
            InitializeConfig(new Dictionary<string, string>()
                {
                    { "AccountName", "dashtest" },
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestdata1;AccountKey=IatOQyIdf8x3HcCZuhtGGLv/nS0v/SwXu2vBS6E9/5/+GYllhdmFFX6YqMXmR7U6UyFYQt4pdZnlLCM+bPcJ4A==" },
                    { "ScaleoutStorage1", "DefaultEndpointsProtocol=https;AccountName=dashtestimport;AccountKey=eQrjK2CLvgjrMkMqamI05DZWzQwVxVevKJThNc1pK2U7Kjs1Vcs9TAWV09OLj/FWe25gqPYHHdS1ZzGLFZw9rw==" },
                });
        }

        [TestMethod]
        public void ImportAccountContainersTest()
        {
            var importAccount = DashConfiguration.DataAccounts[1];
            var importClient = importAccount.CreateCloudBlobClient();
            // Remove all existing containers - note that this is a race condition with others executing the tests concurrently,
            // but this is unlikely enough to avoid contention
            foreach (var container in importClient.ListContainers())
            {
                container.Delete();
            }
            string containerName = "import-container-" + Guid.NewGuid().ToString("N");
            var newContainer = importClient.GetContainerReference(containerName);
            newContainer.CreateIfNotExists();

            AccountManager.ImportAccountAsync(importAccount.Credentials.AccountName).Wait();

            // Verify that our container was imported
            string baseUri = "http://mydashserver/container/" + containerName + "?restype=container";
            string appVersion = "2014-02-14";
            var results = _runner.ExecuteRequestWithHeaders(baseUri, "GET", null, new List<Tuple<string, string>>() 
                { 
                    Tuple.Create("x-ms-version", appVersion)
                }, 
                HttpStatusCode.OK);
            // Verify that all the existing containers were created in the import account
            Assert.IsTrue(importClient.ListContainers().Count() > 1);
        }
    }
}
