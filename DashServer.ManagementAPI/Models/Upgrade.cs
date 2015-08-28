//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.Dash.Common.Update;

namespace DashServer.ManagementAPI.Models
{
    public class AvailableUpgrade
    {
        public bool AvailableUpdate { get; set; }
        public string HighestSeverity { get; set; }
        public string UpdateVersion { get; set; }
    }

    public class UpgradePackages
    {
        public string CurrentVersion { get; set; }
        public IEnumerable<PackageManifest> AvailableUpdates { get; set; }
    }

    public class UpdateVersion
    {
        public string version { get; set; }
    }
}