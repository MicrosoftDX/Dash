// -----------------------------------------------------------------------------------------
// <copyright file="HomeController.cs" company="Microsoft">
//    Copyright 2014 Microsoft Corporation
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>
// -----------------------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace CorsBlogSample.Controllers
{
    /// <summary>
    /// Main controller class that handles web requests
    /// </summary>
    public class HomeController : Controller
    {
        /// <summary>
        /// ListBlobs view controller.
        /// </summary>
        /// <returns>ListBlobs ViewResult</returns>
        public ActionResult ListBlobs()
        {
            return View();
        }

        /// <summary>
        /// UploadImage view controller.
        /// </summary>
        /// <returns>UploadImage ViewResult</returns>
        public ActionResult UploadImage()
        {
            return View();
        }

        /// <summary>
        /// Returns a SAS for the specified blob that can be used to upload/download the blob
        /// </summary>
        /// <param name="blobName">The blob Name</param>
        /// <returns>ContentResult with a SAS signed URI or an empty string</returns>
        [HttpGet]
        public ActionResult GetBlobSasUrl(string blobName = null)
        {
            if (!string.IsNullOrEmpty(blobName))
            {
                CloudBlockBlob blob = AzureCommon.ImagesContainer.GetBlockBlobReference(blobName);
                return Content(GetSasForBlob(blob));
            }
            else
            {
                return Content(String.Format("{0}{1}", AzureCommon.ImagesContainer.Uri, 
                    AzureCommon.ImagesContainer.GetSharedAccessSignature(new SharedAccessBlobPolicy
                    {
                        Permissions = SharedAccessBlobPermissions.List | SharedAccessBlobPermissions.Write,
                        SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(30),
                    })));
            }
        }

        /// <summary>
        /// Generate a blob SAS
        /// </summary>
        /// <param name="blob">CloudBlockBlob</param>
        /// <returns>SAS string for the specified CLoudBlockBlob</returns>
        private static string GetSasForBlob(CloudBlockBlob blob)
        {
            if (blob == null)
            {
                throw new ArgumentNullException("blob can't be null");
            }

            var sas = blob.GetSharedAccessSignature(
                new SharedAccessBlobPolicy()
                {
                    Permissions = SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List,
                    SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(30),
                });
            return string.Format(CultureInfo.InvariantCulture, "{0}{1}", blob.Uri, sas);
        }
    }
}
