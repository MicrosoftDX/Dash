//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Tests
{
    [TestClass]
    public class RequestAuthTests : DashTestBase
    {
        const string StoredPolicyTest       = "testpolicy";
        const string StoredPolicyNoDates    = "testpolicy-nodates";

        static DashTestContext _ctx;

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            // We need to create the container & associated policies
            _ctx = InitializeConfigAndCreateTestBlobs(ctx, "datax1", new Dictionary<string, string>
                {
                    { "AccountName", "dashstorage1" },
                    { "AccountKey", "8jqRVtXUWiEthgIhR+dFwrB8gh3lFuquvJQ1v4eabObIj7okI1cZIuzY8zZHmEdpcC0f+XlUkbFwAhjTfyrLIg==" },
                    { "SecondaryAccountKey", "Klari9ZbVdFQ35aULCfqqehCsd136amhusMHWynTpz2Pg+GyQMJw3GH177hvEQbaZ2oeRYk3jw0mIaV3ehNIRg==" },
                },
                new TestBlob[] { });
            // Create required stored access policies
            var body = new XDocument(
                new XElement("SignedIdentifiers",
                    new XElement("SignedIdentifier",
                        new XElement("Id", StoredPolicyTest),
                        new XElement("AccessPolicy",
                            new XElement("Start", DateTimeOffset.UtcNow.ToString()),
                            new XElement("Expiry", DateTimeOffset.UtcNow.AddMinutes(30).ToString()),
                            new XElement("Permission", "r"))),
                    new XElement("SignedIdentifier",
                        new XElement("Id", StoredPolicyNoDates),
                        new XElement("AccessPolicy",
                            new XElement("Permission", "r")))));
            _ctx.Runner.ExecuteRequest(_ctx.GetContainerUri() + "&comp=acl", "PUT", body, null, HttpStatusCode.OK);
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            CleanupTestBlobs(_ctx);
        }

        [TestMethod]
        public void SharedKeyRequestTest()
        {
            var headers = new[] {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-client-request-id", "adfb540e-1050-4c9b-a53a-be6cb71688d3"),
                Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 22:50:34 GMT"),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
                Tuple.Create("Connection", "Keep-Alive"),
                Tuple.Create("Content-Length", "0"),
            };
            var request = new MockHttpRequestWrapper("GET",
                "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy",
                headers.Concat(new[] { Tuple.Create("Authorization", "SharedKey dashstorage1:rB2yPGBXn3CtquDlk03An34hgRHIibK/Xv+hjG3r0Us=") }));
            Assert.IsTrue(IsRequestAuthorized(request));
            Assert.IsTrue(request.AuthenticationKey.SequenceEqual(Convert.FromBase64String("8jqRVtXUWiEthgIhR+dFwrB8gh3lFuquvJQ1v4eabObIj7okI1cZIuzY8zZHmEdpcC0f+XlUkbFwAhjTfyrLIg==")));
            Assert.AreEqual(request.AuthenticationScheme, "SharedKey");
            // Encoded URI
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/container/test%20encoded",
                headers,
                "SharedKey dashstorage1:CVlJHA7FdyqJ75pPkufGavOsXEtzcr5RAWd3R5fEolA="));
            // For encoded uris, some clients generate the signature on the unencoded path (in violation of the documented spec), but 
            // storage supports it, so we have to as well.
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/container/test%20encoded",
                headers,
                "SharedKey dashstorage1:w6D7S11x58ueIvRKEWZGe1MRVvMkQFO+18wPlfm6f+A="));
            // Alternate encoding - some punctuation characters are encoded as well
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/container/test%20encoded(1)",
                headers,
                "SharedKey dashstorage1:RkJ/SQDzKdpAWVI1fJV+kK715YJ0gigRTnrQdQnx+TU="));
            // Alternate encoding - the full suite of reserved characters
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/dashencode/reserved-characters-blob-[]@!$&()*+,;='",
                headers,
                "SharedKey dashstorage1:kDCnETyjm08UsynfFVPrsXx6zbpR9iWI+H2eylg+UXg="));
            // Standard encoding - the full suite of reserved characters
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/dashencode/reserved-characters-blob-[]@!$&()*+,;='",
                headers,
                "SharedKey dashstorage1:6NTW7igimlQamDKOmDROti282ByL/8kGU4R8n6RVtmM="));
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/container/test%20encoded%281%29",
                headers,
                "SharedKey dashstorage1:RkJ/SQDzKdpAWVI1fJV+kK715YJ0gigRTnrQdQnx+TU="));
            // Secondary key
            request = new MockHttpRequestWrapper("GET",
                "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy",
                headers.Concat(new[] { Tuple.Create("Authorization", "SharedKey dashstorage1:zmKvQUdxMH2OJWQygKOa950BULRSw6Dpue2zpE6TAAA=") }));
            Assert.IsTrue(IsRequestAuthorized(request));
            Assert.IsTrue(request.AuthenticationKey.SequenceEqual(Convert.FromBase64String("Klari9ZbVdFQ35aULCfqqehCsd136amhusMHWynTpz2Pg+GyQMJw3GH177hvEQbaZ2oeRYk3jw0mIaV3ehNIRg==")));
            // Invalid key
            Assert.IsFalse(IsRequestAuthorized("GET",
                "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy",
                headers,
                "SharedKey dashstorage1:ymKvQUdxMH2OJWQygKOa950BULRSw6Dpue2zpE6TAAA="));
            // Non-zero content length
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/container/test/testblob",
                new[] {
                    Tuple.Create("x-ms-version", "2014-02-14"),
                    Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 22:50:34 GMT"),
                    Tuple.Create("Content-Length", "1024"),
                },
                "SharedKey dashstorage1:2LVVY8xiJ1iJzypLMXFTV2dsq6iglj1EAV9BNCi8+B4="));
            // 2015-02-21 version with 0 content length
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/container/test/testblob",
                new[] {
                    Tuple.Create("x-ms-version", "2015-02-21"),
                    Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 22:50:34 GMT"),
                    Tuple.Create("Content-Length", "0"),
                },
                "SharedKey dashstorage1:LN/1NeMcCUcRssRcfKyInYVL1EpqS8SL9xoLlJJsKPs="));
            // Blank content-length with new version
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/container/test/testblob",
                new[] {
                    Tuple.Create("x-ms-version", "2015-02-21"),
                    Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 22:50:34 GMT"),
                },
                "SharedKey dashstorage1:LN/1NeMcCUcRssRcfKyInYVL1EpqS8SL9xoLlJJsKPs="));
            // Valid content-length with new version
            Assert.IsTrue(IsRequestAuthorized("PUT",
                "http://localhost/container/test/testblob",
                new[] {
                    Tuple.Create("x-ms-version", "2015-02-21"),
                    Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 22:50:34 GMT"),
                    Tuple.Create("Content-Length", "1024"),
                },
                "SharedKey dashstorage1:d0i2sKACec56W4SrUEg77CL8b/p7chQZVRzRk7finnI="));
        }

        [TestMethod]
        public void SharedKeyLiteRequestTest()
        {
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy", new[] {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-client-request-id", "bbe0b567-f26e-405c-93e6-341ab6f6bb0e"),
                Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 23:41:08 GMT"),
                Tuple.Create("Authorization", "SharedKeyLite dashstorage1:QG+WCbb0PMit6NZkOiCFNJIzDiz3jlv4oiOj8V6uQB8="),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
                Tuple.Create("Connection", "Keep-Alive"),
            }));
            // Secondary key
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy", new[] {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-client-request-id", "bbe0b567-f26e-405c-93e6-341ab6f6bb0e"),
                Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 23:41:08 GMT"),
                Tuple.Create("Authorization", "SharedKeyLite dashstorage1:uKfAsKUjr3KqNb8jeKVtNuNT7whE+caeAkuGsYWehD8="),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
                Tuple.Create("Connection", "Keep-Alive"),
            }));
            // Invalid key
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy", new[] {
                Tuple.Create("User-Agent", "WA-Storage/4.3.0 (.NET CLR 4.0.30319.34014; Win32NT 6.2.9200.0)"),
                Tuple.Create("x-ms-version", "2014-02-14"),
                Tuple.Create("x-ms-client-request-id", "bbe0b567-f26e-405c-93e6-341ab6f6bb0e"),
                Tuple.Create("x-ms-date", "Fri, 31 Oct 2014 23:41:08 GMT"),
                Tuple.Create("Authorization", "SharedKeyLite dashstorage1:ymKvQUdxMH2OJWQygKOa950BULRSw6Dpue2zpE6TAAA="),
                Tuple.Create("Host", "dashstorage1.blob.core.windows.net"),
                Tuple.Create("Connection", "Keep-Alive"),
            }));
        }

        [TestMethod]
        public void SharedAccessSignatureRequestTest()
        {
            // Blob with container SAS based on primary key
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=c&sig=SPMPjAghEhfEX1RvZLJPD2dQ%2B%2BnpH08vrVSUyWfdPwo%3D&st=2015-05-22T20%3A44%3A13Z&se=2015-05-22T21%3A44%3A13Z&sp=r"));
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/blob/test/test1.txt?sv=2014-02-14&sr=c&sig=SPMPjAghEhfEX1RvZLJPD2dQ%2B%2BnpH08vrVSUyWfdPwo%3D&st=2015-05-22T20%3A44%3A13Z&se=2015-05-22T21%3A44%3A13Z&sp=r"));
            // Blob with container SAS based on secondary key
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=c&sig=OkZlHUAsAzvqvONRiNWIpAW0GA%2F75uHQRWuKzafVdAE%3D&st=2015-05-22T21%3A16%3A24Z&se=2015-05-22T22%3A16%3A24Z&sp=r"));
            // Blob with blob SAS based on secondary key
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=b&sig=gXk4Bxt37rv65%2BwpEfuwa6BHz3Gk4hK6072dlUZiX0E%3D&st=2015-05-22T21%3A21%3A29Z&se=2015-05-22T22%3A21%3A29Z&sp=r"));
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/blob/test/test1.txt?sv=2014-02-14&sr=b&sig=gXk4Bxt37rv65%2BwpEfuwa6BHz3Gk4hK6072dlUZiX0E%3D&st=2015-05-22T21%3A21%3A29Z&se=2015-05-22T22%3A21%3A29Z&sp=r"));
            // Permissions
            Assert.IsFalse(IsRequestAuthorized("PUT", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=b&sig=gXk4Bxt37rv65%2BwpEfuwa6BHz3Gk4hK6072dlUZiX0E%3D&st=2015-05-22T21%3A21%3A29Z&se=2015-05-22T22%3A21%3A29Z&sp=r"));
            Assert.IsTrue(IsRequestAuthorized("PUT", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=b&sig=EMWn11cUlm6BuJhWH%2B%2FGdINAwbGvwWm6LscP8ZADUqM%3D&st=2015-05-22T21%3A24%3A37Z&se=2015-05-22T22%3A24%3A37Z&sp=rw"));
            Assert.IsFalse(IsRequestAuthorized("DELETE", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=b&sig=EMWn11cUlm6BuJhWH%2B%2FGdINAwbGvwWm6LscP8ZADUqM%3D&st=2015-05-22T21%3A24%3A37Z&se=2015-05-22T22%3A24%3A37Z&sp=rw"));
            Assert.IsTrue(IsRequestAuthorized("DELETE", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=b&sig=mH3fmKyYJhmmxc5DMIkN%2FGvszy5ZICnrPmBSJCWgUPU%3D&st=2015-05-22T21%3A25%3A57Z&se=2015-05-22T22%3A25%3A57Z&sp=rwd"));
            // Container list blob
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy&sv=2014-02-14&sr=c&sig=SPMPjAghEhfEX1RvZLJPD2dQ%2B%2BnpH08vrVSUyWfdPwo%3D&st=2015-05-22T20%3A44%3A13Z&se=2015-05-22T21%3A44%3A13Z&sp=r"));
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/container/test?restype=container&comp=list&prefix=te&include=snapshots,uncommittedblobs,metadata,copy&sv=2014-02-14&sr=c&sig=KFHvXPWBWxYFWv6lEapqJO%2BZ6WTGrVBdEHVCK7t6SQ0%3D&st=2015-05-22T21%3A28%3A32Z&se=2015-05-22T22%3A28%3A32Z&sp=rl"));
            // Older version
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2012-02-12&sr=b&sig=i%2FONHWNOfiwr0QBUaAnZ8ppyPKqsGtC%2BHbkBKcgRMFQ%3D&st=2015-05-22T21%3A32%3A13Z&se=2015-05-22T22%3A32%3A13Z&sp=rwd"));
            // Re-enable stored policy auth tests when we don't rely to pre-existing policies
            // Stored policy
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=b&si=" + StoredPolicyTest + "&sig=6VCAjUcL2F%2FVJfBneHvVT4RXv2JcS9jooXaIEKw2fNM%3D",
                new[] { Tuple.Create("StoredPolicyContainer", _ctx.ContainerName) }));
            // Stored policy - attempt to override set expiry time
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=b&si=" + StoredPolicyTest + "&sig=wGmBX5GS2dGrbxRVfOPuT0WkhUtKMffarW1mIF7ZmmA%3D&st=2015-05-22T22%3A48%3A46Z&se=2015-05-22T23%3A48%3A46Z&sp=r",
                new[] { Tuple.Create("StoredPolicyContainer", _ctx.ContainerName) }));
            // Stored policy - attempt to override unset expiry time
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=b&si=" + StoredPolicyNoDates + "&sig=Fvdh21mrNY%2FBGaeVuVAFt0rdnabO6qAYWVWsiG3kTwQ%3D&st=2015-05-22T22%3A53%3A24Z&se=2015-05-22T23%3A53%3A24Z",
                new[] { Tuple.Create("StoredPolicyContainer", _ctx.ContainerName) }));
            // Stored policy - invalid stored policy id
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=b&si=testpolicy-invalid&sig=meGd900%2BhZrSgu3xqsMR9Np%2Fpl6TSnH%2Bsi6wDxBcRgo%3D"));
            // Invalid SAS structure - missing expiry
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=b&sig=Y%2B3LyPR8lozh2Epolb7t4tZBUAGfb9tk6iE6zwA8Udc%3D&st=2015-05-22T23%3A04%3A45Z&sp=r"));
            // Invalid SAS structure - missing permissions
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2014-02-14&sr=b&sig=Z3aYFycY54oa73E4efuLUuOdFjPVmdgDrXkPWG0QzRY%3D&st=2015-05-22T23%3A07%3A46Z&se=2015-05-23T00%3A07%3A46Z"));
            // 2015-04-05 signature, including Protocol & sip
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2015-04-05&sr=c&si=&sig=1urZ3PGr2r8p6aX8%2FDkUY2d%2FGnjj%2Bwx2DCmNRQEx%2BM0%3D&spr=https%2Chttp&sip=127.0.0.1&se=2015-10-09T19%3A22%3A15Z&sp=rl"));
        }

        [TestMethod]
        public void SASAccountRequestTest()
        {
            // Account SAS - List Containers
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/account?comp=list&sv=2015-04-05&sig=j9BTOeDqF3g4145hOOnE%2F81akzKFR%2FGBJFtQldyUY%2FY%3D&se=2015-10-09T18%3A55%3A03Z&srt=s&ss=b&sp=rl"));
            // Account SAS - secondary key
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/account?comp=list&sv=2015-04-05&sig=q5kaCAGu7j2BX%2BUbLEFyOmugtL179iftRm5jOgg2Wro%3D&se=2015-10-09T19%3A08%3A54Z&srt=s&ss=b&sp=rl"));
            // Valid account SAS - but not blob service
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/account?comp=list&sv=2015-04-05&sig=3UYb0OjEwFiMNAV4633Rj8Vqs0nOyjgO2bvxiOla82k%3D&se=2015-10-09T19%3A10%3A33Z&srt=s&ss=fq&sp=rl"));
            // Valid account SAS - but invalid resource type
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/account?comp=list&sv=2015-04-05&sig=Yf%2Fk2bHW7aWMVQvAbiPsdmvZlSWV1G6Wxp44PKs4cSQ%3D&se=2015-10-09T19%3A13%3A15Z&srt=o&ss=b&sp=rl"));
            // Multiple resource types for account SAS
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/account?comp=list&sv=2015-04-05&sig=jZ11TnKQmortK5n8B817OzXp%2BTpSAA6qcoCwTgW4LTk%3D&se=2015-10-09T19%3A14%3A44Z&srt=sco&ss=b&sp=rl"));
            // Account SAS applied to blob operation
            Assert.IsTrue(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2015-04-05&sig=jZ11TnKQmortK5n8B817OzXp%2BTpSAA6qcoCwTgW4LTk%3D&se=2015-10-09T19%3A14%3A44Z&srt=sco&ss=b&sp=rl"));
            // Account SAS applied to blob operation with invalid resource type
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/blob/test/test.txt?sv=2015-04-05&sig=j9BTOeDqF3g4145hOOnE%2F81akzKFR%2FGBJFtQldyUY%2FY%3D&se=2015-10-09T18%3A55%3A03Z&srt=s&ss=b&sp=rl"));
        }

        [TestMethod]
        public void RedirectionSignatureTest()
        {
            var request = new MockHttpRequestWrapper("GET", "http://localhost/container/test%20encoded", null)
            {
                AuthenticationScheme = "SharedKey",
                AuthenticationKey = DashConfiguration.AccountKey,
            };
            var result = HandlerResult.Redirect(request,
                "http://dataaccount.blob.core.windows.net/container/test%20encoded");
            result.Headers = new ResponseHeaders(new[] {
                new KeyValuePair<string, string>("x-ms-date", "Wed, 01 Apr 2015 01:26:43 GMT"),
            });
            Assert.AreEqual(result.SignedLocation, "SharedKey dashstorage1:iU0kJCrvLR7rdIS/HXO0T04gTu09enDo25/3WtrjESI=");
            // Secondary key
            request = new MockHttpRequestWrapper("GET", "http://localhost/container/test%20encoded", null)
            {
                AuthenticationScheme = "SharedKeyLite",
                AuthenticationKey = DashConfiguration.SecondaryAccountKey,
            };
            result = HandlerResult.Redirect(request,
                "http://dataaccount.blob.core.windows.net/container/test%20encoded");
            result.Headers = new ResponseHeaders(new[] {
                new KeyValuePair<string, string>("x-ms-date", "Wed, 01 Apr 2015 01:26:43 GMT"),
            });
            Assert.AreEqual(result.SignedLocation, "SharedKeyLite dashstorage1:o3XAz28naFjcSxCbqoZL394S/zLY2+nYk7v8KbdnlSI=");
        }

        [Flags]
        enum ContainerAccess
        {
            None = 0x00,
            Private = 0x01,
            Container = 0x02,
            Blob = 0x04,
        }

        [TestMethod]
        public void AnonymousAccessTest()
        {
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/account?comp=list"));
            Assert.IsFalse(IsRequestAuthorized("PUT", "http://localhost/account?restype=service&comp=properties"));
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/account?restype=service&comp=properties"));
            Assert.IsFalse(IsRequestAuthorized("GET", "http://localhost/account?restype=service&comp=stats"));
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "", "restype=container");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container, "GET", "", "restype=container");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container, "HEAD", "", "restype=container");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container, "GET", "", "restype=container&comp=metadata");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container, "HEAD", "", "restype=container&comp=metadata");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "", "restype=container&comp=metadata");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "GET", "", "restype=container&comp=acl");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "HEAD", "", "restype=container&comp=acl");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "", "restype=container&comp=acl");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "", "restype=container&comp=lease");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "DELETE", "", "restype=container");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container, "GET", "", "restype=container&comp=list");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container | ContainerAccess.Blob, "GET", "test.txt", "");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container | ContainerAccess.Blob, "HEAD", "test.txt", "");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "comp=properties");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container | ContainerAccess.Blob, "GET", "test.txt", "comp=metadata");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container | ContainerAccess.Blob, "HEAD", "test.txt", "comp=metadata");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "comp=metadata");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "DELETE", "test.txt", "");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "comp=lease");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "", new[] { Tuple.Create("x-ms-copy-source", "http://localhost/blob/myblob/myblob2") });
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "comp=copy");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "comp=block");
            EvaluateAnonymousContainerAccess(ContainerAccess.None, "PUT", "test.txt", "comp=blocklist");
            EvaluateAnonymousContainerAccess(ContainerAccess.Container | ContainerAccess.Blob, "GET", "test.txt", "comp=blocklist");
        }

        static bool IsRequestAuthorized(string method, string uri, IEnumerable<Tuple<string, string>> headers, string authString)
        {
            return IsRequestAuthorized(method, uri, headers.Concat(new[] { Tuple.Create("Authorization", authString) }));
        }

        static bool IsRequestAuthorized(string method, string uri, IEnumerable<Tuple<string, string>> headers = null)
        {
            var clientHeaders = new[] { Tuple.Create("REMOTE_ADDR", "127.0.0.1") };
            if (headers == null)
            {
                headers = clientHeaders;
            }
            else
            {
                headers = headers.Concat(clientHeaders);
            }
            return IsRequestAuthorized(new MockHttpRequestWrapper(method, uri, headers));
        }

        static bool IsRequestAuthorized(IHttpRequestWrapper request)
        {
            return RequestAuthorization.IsRequestAuthorizedAsync(request, true).Result;
        }

        static void EvaluateAnonymousContainerAccess(ContainerAccess expectedAccess, string method, string blob, string uriSuffix, IEnumerable<Tuple<string, string>> headers = null)
        {
            Assert.AreEqual(expectedAccess.HasFlag(ContainerAccess.Private), IsRequestAuthorized(method, FormatContainerAccessUri(ContainerAccess.Private, blob, uriSuffix), headers));
            Assert.AreEqual(expectedAccess.HasFlag(ContainerAccess.Container), IsRequestAuthorized(method, FormatContainerAccessUri(ContainerAccess.Container, blob, uriSuffix), headers));
            Assert.AreEqual(expectedAccess.HasFlag(ContainerAccess.Blob), IsRequestAuthorized(method, FormatContainerAccessUri(ContainerAccess.Blob, blob, uriSuffix), headers));
        }

        static string FormatContainerAccessUri(ContainerAccess accessTest, string blob, string uriSuffix)
        {
            return String.Format("http://localhost/{0}/{1}/{2}?{3}",
                String.IsNullOrEmpty(blob) ? "container" : "blob",
                accessTest == ContainerAccess.Private ? "test" : (accessTest == ContainerAccess.Container ? "anonymouscontainertest" : "anonymousblobtest"),
                blob,
                uriSuffix);
        }

    }
}

