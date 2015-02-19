//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace Microsoft.Dash.Server.Diagnostics
{
    public static class DashTrace
    {
        static readonly JsonSerializer _serializer = JsonSerializer.Create(new JsonSerializerSettings()
        {
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        });

        public static void TraceInformation(string message)
        {
            TraceInformation(new TraceMessage() { Message = message });
        }

        public static void TraceInformation(string format, params object[] args)
        {
            TraceInformation(new TraceMessage() { Message = String.Format(format, args) });
        }

        public static void TraceInformation(TraceMessage message)
        {
            DoTrace(message, TraceLevel.Info);
        }

        public static void TraceWarning(string message)
        {
            TraceWarning(new TraceMessage() { Message = message });
        }

        public static void TraceWarning(string format, params object[] args)
        {
            TraceWarning(new TraceMessage() { Message = String.Format(format, args) });
        }

        public static void TraceWarning(TraceMessage message)
        {
            DoTrace(message, TraceLevel.Warning);
        }

        public static void TraceError(string message)
        {
            TraceError(new TraceMessage() { Message = message });
        }

        public static void TraceError(string format, params object[] args)
        {
            TraceError(new TraceMessage() { Message = String.Format(format, args) });
        }

        public static void TraceError(TraceMessage message)
        {
            DoTrace(message, TraceLevel.Error);
        }

        static void DoTrace(TraceMessage message, TraceLevel level)
        {
            /*if (String.IsNullOrEmpty(message.CorrelationId))
            {
                ICorrelationSource correlationSource = CorrelationController.CurrentSource;
                if (correlationSource != null)
                {
                    message.CorrelationId = correlationSource.CorrelationId;
                }
            }*/
            using (var writer = new StringWriter())
            {
                _serializer.Serialize(writer, message);
                // Can't use delegates here as all the Trace methods are [Conditional]
                switch (level)
                {
                    case TraceLevel.Info:
                    default:
                        Trace.TraceInformation(writer.ToString());
                        break;

                    case TraceLevel.Warning:
                        Trace.TraceWarning(writer.ToString());
                        break;

                    case TraceLevel.Error:
                        Trace.TraceError(writer.ToString());
                        break;
                }
            }
        }
    }
}


