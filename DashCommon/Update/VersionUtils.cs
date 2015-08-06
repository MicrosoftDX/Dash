//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Microsoft.Dash.Common.Update
{
    public static class VersionUtils
    {
        const char VersionFormatPrefix = 'v';

        public static Version ParseVersion(string versionString)
        {
            if (String.IsNullOrWhiteSpace(versionString))
            {
                throw new ArgumentNullException("versionString is null");
            }
            else if (Char.ToLowerInvariant(versionString[0]) != VersionFormatPrefix)
            {
                throw new FormatException("versionString does not match format 'vM.N.V'");
            }
            return Version.Parse(versionString.Substring(1));
        }

        public static bool TryParseVersion(string versionString, out Version version)
        {
            try
            {
                version = ParseVersion(versionString);
                return true;
            }
            catch
            {
                version = null;
            }
            return false;
        }

        public static string SemanticVersionFormat(this Version version)
        {
            return VersionFormatPrefix + version.ToString();
        }
    }

    public class DashVersionConverter : VersionConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }
            if (reader.TokenType == JsonToken.String)
            {
                try
                {
                    return VersionUtils.ParseVersion((string)reader.Value);
                }
                catch (Exception exception)
                {
                    throw new JsonSerializationException("Error parsing version string", exception);
                }
            }
            throw new JsonSerializationException(String.Format("Unexpected token or value when parsing version. Token: {0}, Value: {1}", reader.TokenType, reader.Value));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                if (!(value is Version))
                {
                    throw new JsonSerializationException("Expected Version object value");
                }
                writer.WriteValue(((Version)value).SemanticVersionFormat());
            }
        }
    }
}
