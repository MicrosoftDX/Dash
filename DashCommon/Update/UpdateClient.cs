//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Newtonsoft.Json;

namespace Microsoft.Dash.Common.Update
{
    public class UpdateClient
    {
        const string DefaultUpdateService = "https://www.dash-update.net";

        public enum Components
        {
            DashServer,
            Client,
        }

        public enum PackageFlavors
        {
            Http,
            HttpWithIlb,
            Https,
            HttpsWithIlb,
        }

        public UpdateClient(string requestor = null, string updateServiceRoot = null)
        {
            this.Requestor = requestor;
            if (!String.IsNullOrWhiteSpace(updateServiceRoot))
            {
                this.UpdateServiceUri = updateServiceRoot;
            }
            else
            {
                this.UpdateServiceUri = DefaultUpdateService;
            }
        }

        public string UpdateServiceUri { get; set; }
        public string Requestor { get; set; }

        public static PackageManifest GetAvailableUpdate(Components componentToCheck, string requestor = null, string updateServiceRoot = null)
        {
            var client = new UpdateClient(requestor, updateServiceRoot);
            return client.GetAvailableUpdate(componentToCheck);
        }

        public PackageManifest GetAvailableUpdate(Components componentToCheck)
        {
            return GetAvailableUpdateAsync(componentToCheck).Result;
        }

        public async Task<PackageManifest> GetAvailableUpdateAsync(Components componentToCheck)
        {
            var requestUri = new ApiUriBuilder(this.UpdateServiceUri, new Dictionary<string, string>
                {
                    { ApiUriBuilder.QueryParamCurrentVersion, GetRequestVersion() },
                    { ApiUriBuilder.QueryParamRequestor, this.Requestor },
                });
            requestUri.Path += GetComponentPath(componentToCheck);
            try
            {
                return await ReadObjectAsync<PackageManifest>(requestUri.Uri);
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Failure attempting to check for component [{0}] update manifest [{1}]. Details: {2}", componentToCheck, requestUri.ToString(), ex);
            }

            return null;
        }

        public async Task<PackageManifest> GetUpdateVersionAsync(Components componentToCheck, string version)
        {
            var requestUri = new ApiUriBuilder(this.UpdateServiceUri);
            requestUri.Path += String.Join("/", GetComponentPath(componentToCheck), version);
            try
            {
                return await ReadObjectAsync<PackageManifest>(requestUri.Uri);
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Failure attempting to check for component [{0}] update manifest [{1}]. Details: {2}", componentToCheck, requestUri.ToString(), ex);
            }

            return null;
        }

        public PackageManifest GetLatestRelease(Components component)
        {
            return GetLatestReleaseAsync(component).Result;
        }

        public async Task<PackageManifest> GetLatestReleaseAsync(Components component)
        {
            var manifests = await GetAvailableManifestsAsync(component);
            if (manifests != null)
            {
                // Manifiests are already sorted by version
                return manifests.FirstOrDefault();
            }
            return null;
        }

        public async Task<IEnumerable<PackageManifest>> GetAvailableManifestsAsync(Components component)
        {
            var requestUri = new ApiUriBuilder(this.UpdateServiceUri, new Dictionary<string, string>
                {
                    { ApiUriBuilder.QueryParamRequestor, this.Requestor },
                });
            requestUri.Path += GetComponentPath(component);
            try
            {
                return (await ReadObjectAsync<IEnumerable<PackageManifest>>(requestUri.Uri))
                    .OrderByDescending(manifest => manifest.Version);
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Failure attempting to check for component [{0}] available manifests [{1}]. Details: {2}", component, requestUri.ToString(), ex);
            }
            return null;
        }

        public Uri GetPackageFileUri(Components component, PackageManifest manifest, FilePackage package, string file, string operation = null)
        {
            var requestUri = new ApiUriBuilder(this.UpdateServiceUri, new Dictionary<string, string>
                {
                    { ApiUriBuilder.QueryParamRequestor, this.Requestor },
                });
            requestUri.Path += String.Join("/",
                GetComponentPath(component),
                manifest.Version.SemanticVersionFormat(),
                package.PackageName,
                file);
            if (!String.IsNullOrWhiteSpace(operation))
            {
                requestUri.Query = "comp=" + operation;
            }
            return requestUri.Uri;
        }

        public async Task<Uri> GetPackageFileSasUriAsync(Components component, PackageManifest manifest, FilePackage package, string file)
        {
            var fileUri = GetPackageFileUri(component, manifest, package, file, "sas");
            var sas = await ReadObjectAsync<string>(fileUri);
            return new Uri(sas);
        }

        public async Task<Stream> DownloadPackageFileAsync(Components component, PackageManifest manifest, FilePackage package, string file)
        {
            var fileUri = GetPackageFileUri(component, manifest, package, file);
            try
            {
                using (var downloadClient = new HttpClient())
                {
                    var response = await downloadClient.GetAsync(fileUri);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStreamAsync();
                }
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Failed to download package file [{0}] to local stream. Details: {1}", fileUri, ex);
                throw new InvalidOperationException(String.Format("Failed to download package file [{0}] to local stream.", fileUri), ex);
            }
        }

        public async Task DownloadPackageFileToStreamAsync(Components component, PackageManifest manifest, FilePackage package, string file, Stream destination)
        {
            using (var stream = await DownloadPackageFileAsync(component, manifest, package, file))
            { 
                await stream.CopyToAsync(destination);
            }
        }

        public async Task<Stream> DownloadPackageFileToLocalFileStreamAsync(Components component, PackageManifest manifest, FilePackage package, string file, string localFilename)
        {
            var retval = File.Create(localFilename);
            await DownloadPackageFileToStreamAsync(component, manifest, package, file, retval);
            retval.Seek(0, SeekOrigin.Begin);
            return retval;
        }

        public async Task<Stream> DownloadPackageFileToTempFileStreamAsync(Components component, PackageManifest manifest, FilePackage package, string file)
        {
            string tempLocation = Path.ChangeExtension(Path.GetTempFileName(), Path.GetExtension(file));
            return await DownloadPackageFileToLocalFileStreamAsync(component, manifest, package, file, tempLocation);
        }

        public async Task DownloadPackageFileToLocalFileAsync(Components component, PackageManifest manifest, FilePackage package, string file, string localFilename)
        {
            var stream = await DownloadPackageFileToLocalFileStreamAsync(component, manifest, package, file, localFilename);
            stream.Dispose();
        }

        public async Task<string> DownloadPackageFileToTempFileAsync(Components component, PackageManifest manifest, FilePackage package, string file)
        {
            string tempLocation = Path.ChangeExtension(Path.GetTempFileName(), Path.GetExtension(file));
            await DownloadPackageFileToLocalFileAsync(component, manifest, package, file, tempLocation);
            return tempLocation;
        }

        public static string GetPackageFlavorLabel(PackageFlavors packageFlavor)
        {
            switch (packageFlavor)
            {
                case PackageFlavors.Http:
                    return "HTTP";

                case PackageFlavors.HttpWithIlb:
                    return "HTTP.ILB";

                case PackageFlavors.Https:
                    return "HTTPS";

                case PackageFlavors.HttpsWithIlb:
                    return "HTTPS.ILB";

                default:
                    System.Diagnostics.Debug.Assert(false);
                    break;
            }
            return String.Empty;
        }

        static string GetComponentPath(Components component)
        {
            // Possible future decoupling of enumeration & path segment
            return component.ToString();
        }

        static string GetRequestVersion()
        {
            return Assembly.GetCallingAssembly().GetName().Version.SemanticVersionFormat();
        }

        static async Task<T> ReadObjectAsync<T>(Uri location, IDictionary<string, string> requestHeaders = null)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    if (requestHeaders != null)
                    {
                        foreach (var header in requestHeaders)
                        {
                            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                    using (var response = await client.GetAsync(location))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            return await response.Content.ReadAsAsync<T>(new[] { GetJsonMediaTypeFormatter(location) });
                        }
                        else if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            return default(T);
                        }
                        else
                        {
                            response.EnsureSuccessStatusCode();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Failure reading JSON object [{0}]. Details: {1}", location, ex);
                throw;
            }
            return default(T);
        }

        static MediaTypeFormatter GetJsonMediaTypeFormatter(Uri location)
        {
            var retval = new JsonMediaTypeFormatter
            {
                SerializerSettings = new JsonSerializerSettings
                {
                    Error = (sender, args) =>
                    {
                        DashTrace.TraceWarning("Error deserializing JSON object [{0}]. Details: {1}", location, args.ErrorContext.Error.Message);
                        args.ErrorContext.Handled = true;
                    }
                },
                UseDataContractJsonSerializer = false,
            };
            // To support reading objects from blob store (everything is */octet-stream
            retval.SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/octet-stream"));
            retval.SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("binary/octet-stream"));
            return retval;
        }
    }
}