//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Tests
{
    class TestUtils
    {
        public static void InitializeConfig(IDictionary<string, string> config)
        {
            AzureUtils.ConfigProvider = new TestConfigurationProvider(config);
        }

        class TestConfigurationProvider : IConfigurationProvider
        {
            IDictionary<string, string> _testConfig;

            public TestConfigurationProvider(IDictionary<string, string> config)
            {
                _testConfig = config;
            }

            public string GetSetting(string name)
            {
                string retval = String.Empty;
                _testConfig.TryGetValue(name, out retval);
                return retval;
            }
        }
    }
}
