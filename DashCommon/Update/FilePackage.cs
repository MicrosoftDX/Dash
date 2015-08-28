//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Dash.Common.Update
{
    public class FilePackage
    {
        public string PackageName { get; set; }
        public string Description { get; set; }
        public IList<string> Files { get; set; }
        public IList<FileInformation> FileInformations { get; set; }

        public FileInformation FindFileByExtension(string fileExtension)
        {
            if (fileExtension[0] != '.')
            {
                fileExtension = "." + fileExtension;
            }
            if (this.FileInformations == null)
            {
                var fileName = this.Files
                    .FirstOrDefault(file => String.Equals(fileExtension, Path.GetExtension(file), StringComparison.OrdinalIgnoreCase));
                if (!String.IsNullOrWhiteSpace(fileName))
                {
                    return new FileInformation
                    {
                        Name = fileName,
                    };
                }
                return null;
            }
            return this.FileInformations
                .FirstOrDefault(fileInfo => String.Equals(fileExtension, Path.GetExtension(fileInfo.Name), StringComparison.OrdinalIgnoreCase));
        }

        public FileInformation GetFileInformation(string fileName)
        {
            if (this.FileInformations == null)
            {
                return null;
            }
            return this.FileInformations
                .FirstOrDefault(fileInfo => String.Equals(fileInfo.Name, fileName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class FileInformation
    {
        public string Name { get; set; }
        public string SasUri { get; set; }
    }
}
