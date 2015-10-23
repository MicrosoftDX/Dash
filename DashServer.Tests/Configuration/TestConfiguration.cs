//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;

namespace Microsoft.Tests.Configuration
{
    public class TestConfiguration
    {
        public string NamespaceConnectionString { get; set; }
        public IEnumerable<string> DataConnectionStrings { get; set; }
    }
}
