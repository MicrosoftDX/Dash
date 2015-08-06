//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Update
{
    public class FilePackage
    {
        // Known package names
        public const string PackageNameHttp = "HTTP";
        public const string PackageNameHttps = "HTTPS";
        public const string PackageNameConsole = "Console";

        public string PackageName { get; set; }
        public string Description { get; set; }
        public IList<string> Files { get; set; }

        public string FindFileByExtension(string fileExtension)
        {
            if (fileExtension[0] != '.')
            {
                fileExtension = "." + fileExtension;
            }
            return this.Files
                .FirstOrDefault(file => String.Compare(fileExtension, Path.GetExtension(file), true) == 0);
        }
    }
}
