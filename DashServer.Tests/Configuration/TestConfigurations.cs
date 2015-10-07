//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;

namespace Microsoft.Tests.Configuration
{
    public class TestConfigurations
    {
        public const string DefaultConfigurationFile = "TestConfigurations.json";

        public static TestConfigurations ReadHttp(string configUri)
        {
            using (var reader = new StreamReader((new HttpClient()).GetStreamAsync(configUri).Result))
            {
                return Read(reader);
            }
        }

        public static TestConfigurations ReadFile(string configPath)
        {
            if (String.IsNullOrWhiteSpace(configPath))
            {
                configPath = DefaultConfigurationFile;
            }
            using (var reader = File.OpenText(configPath))
            {
                return Read(reader);
            }
        }

        public static TestConfigurations Read(TextReader configReader)
        {
            using (var reader = new JsonTextReader(configReader))
            {
                return new TestConfigurations
                {
                    Configurations = JsonSerializer.CreateDefault().Deserialize<IDictionary<string, TestConfiguration>>(reader),
                };
            }
        }

        public IDictionary<string, TestConfiguration> Configurations { get; private set; }
    }
}
