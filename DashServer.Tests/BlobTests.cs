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
using System.Threading;
using Microsoft.Dash.Server;

namespace Microsoft.Tests
{
    [TestClass]
    public class BlobTests
    {
        WebApiTestRunner _runner;

        [TestInitialize]
        public void Init()
        {
            _runner = new WebApiTestRunner(new Dictionary<string, string>()
                {
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=http;AccountName=dashstorage0;AccountKey=uCNvIdXcltACBiDUMyO0BflZpKmjseplqOlzE62tx87qnkwpUMBV/GQhrscW9lmdZVT0x8DilYqUoHMNBlVIGg==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=http;AccountName=dashstorage1;AccountKey=8jqRVtXUWiEthgIhR+dFwrB8gh3lFuquvJQ1v4eabObIj7okI1cZIuzY8zZHmEdpcC0f+XlUkbFwAhjTfyrLIg==" },
                    { "ScaleoutNumberOfAccounts", "1"},
                });
        }

        [TestMethod]
        public void GetBlobTest()
        {
            var response = _runner.ExecuteRequest("http://localhost/blob/test/test.txt", "GET");
            Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode, "Expected Redirect result");
            Assert.IsNotNull(response.Headers.Location);
            Assert.AreEqual("http://dashstorage1.blob.core.windows.net/test/test.txt", response.Headers.Location.GetLeftPart(UriPartial.Path));
            var redirectQueryParams = HttpUtility.ParseQueryString(response.Headers.Location.Query);
            Assert.AreEqual("2014-02-14", redirectQueryParams["sv"]);
            Assert.IsNotNull(redirectQueryParams["sig"]);
            Assert.IsNotNull(redirectQueryParams["se"]);
        }
    }
}
