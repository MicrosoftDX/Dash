//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.Dash.Common.Platform.Payloads
{
    public class UpdateServicePayload : ServiceOperationPayload
    {
        public const string AccountsToImport    = "accountstoimport";
        public const string ConfigSettings      = "configsettings";

        private QueueMessage _message;

        public UpdateServicePayload(QueueMessage message)
        {
            this._message = message;
        }

        public IEnumerable<string> ImportAccounts
        {
            get
            {
                return JsonConvert.DeserializeObject<string[]>(_message.Payload[AccountsToImport]);
            }
            set
            {
                _message.Payload[AccountsToImport] = JsonConvert.SerializeObject(value, Formatting.None);
            }
        }

        public IDictionary<string, string> Settings
        {
            get
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(_message.Payload[ConfigSettings]);
            }
            set
            {
                _message.Payload[ConfigSettings] = JsonConvert.SerializeObject(value, Formatting.None);
            }
        }
    }
}
