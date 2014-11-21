//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Xml;

namespace Microsoft.Dash.Server.Utils
{
    public static class RequestUtils
    {
        public static RequestQueryParameters GetQueryParameters(this HttpRequestMessage request)
        {
            return RequestQueryParameters.Create(request);
        }

        public static RequestQueryParameters GetQueryParameters(this HttpRequestBase request)
        {
            return RequestQueryParameters.Create(request);
        }

        public static RequestHeaders GetHeaders(this HttpRequestMessage request)
        {
            return RequestHeaders.Create(request);
        }

        public static RequestHeaders GetHeaders(this HttpRequestBase request)
        {
            return RequestHeaders.Create(request);
        }

        public static void WriteElementStringIfNotNull(this XmlWriter writer, string localName, string value)
        {
            if (!String.IsNullOrWhiteSpace(value))
            {
                writer.WriteElementString(localName, value);
            }
        }

        public static void WriteElementString(this XmlWriter writer, string localName, DateTimeOffset? value)
        {
            string valueToWrite = String.Empty;
            if (value.HasValue)
            {
                valueToWrite = value.Value.ToString("r");
            }
            writer.WriteElementString(localName, valueToWrite);
        }

        public static void WriteElementStringIfNotNull(this XmlWriter writer, string localName, DateTimeOffset? value)
        {
            if (value.HasValue)
            {
                writer.WriteElementString(localName, value.Value.ToString("r"));
            }
        }

        public static void WriteElementStringIfNotNull<T>(this XmlWriter writer, string localName, T? value) where T : struct
        {
            if (value.HasValue)
            {
                writer.WriteElementString(localName, value.Value.ToString());
            }
        }

        public static void WriteElementStringIfNotEnumValue<T>(this XmlWriter writer, string localName, T value, T invalidValue, bool emitLowerCase = true) where T : struct
        {
            if (!value.IsFlagSet(invalidValue))
            {
                string valueToWrite = String.Empty;
                if (emitLowerCase)
                {
                    valueToWrite = value.ToString().ToLowerInvariant();
                }
                else
                {
                    valueToWrite = value.ToString();
                }
                writer.WriteElementString(localName, valueToWrite);
            }
        }

        public static bool IsFlagSet<T>(this T value, T flag) where T : struct
        {
            int lhs = Convert.ToInt32(value);
            int rhs = Convert.ToInt32(flag);
            // Special case 0 value enums
            if (lhs == 0 && rhs == 0)
            {
                return true;
            }
            return (lhs & rhs) != 0;
        }
    }

}