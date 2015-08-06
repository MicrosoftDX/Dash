//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Dash.Common.Update
{
    public enum UpdateSeverity
    {
        None,
        Optional,
        Important,
        Critical,
    }

    public class PackageManifest
    {
        public PackageManifest()
        {
            this.Severity = UpdateSeverity.Optional;
            this.AvailablePackages = new List<FilePackage>();
        }

        [JsonConverter(typeof(DashVersionConverter))]
        public Version Version { get; set; }

        public string Description { get; set; }
        public UpdateSeverity Severity { get; set; }

        public IList<FilePackage> AvailablePackages { get; set; }

        public FilePackage GetPackage(string packageName)
        {
            return this.AvailablePackages
                .FirstOrDefault(package => String.Equals(package.PackageName, packageName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
