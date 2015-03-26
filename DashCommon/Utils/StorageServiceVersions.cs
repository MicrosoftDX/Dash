//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Microsoft.Dash.Common.Utils
{
    public static class StorageServiceVersions
    {
        public static readonly DateTimeOffset Version_2009_09_19 = DateTimeOffset.Parse("2009-09-19 +0:00");
        public static readonly DateTimeOffset Version_2011_08_18 = DateTimeOffset.Parse("2011-08-18 +0:00");
        public static readonly DateTimeOffset Version_2012_02_12 = DateTimeOffset.Parse("2012-02-12 +0:00");
        public static readonly DateTimeOffset Version_2013_08_15 = DateTimeOffset.Parse("2013-08-15 +0:00");
        public static readonly DateTimeOffset Version_2014_02_14 = DateTimeOffset.Parse("2014-02-14 +0:00");

        public static string ToVersionString(this DateTimeOffset version)
        {
            return version.ToString("yyyy-MM-dd");
        }
    }
}