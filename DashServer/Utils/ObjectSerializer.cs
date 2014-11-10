//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;
using System.Xml;

namespace Microsoft.Dash.Server.Utils
{
    public class ObjectSerializer<T> : XmlObjectSerializer where T : class
    {
        Action<XmlWriter, T> _writeContent;

        public ObjectSerializer(Action<XmlWriter, T> writeContentDelegate)
        {
            this._writeContent = writeContentDelegate;
        }
        
        public override bool IsStartObject(System.Xml.XmlDictionaryReader reader)
        {
            throw new NotImplementedException();
        }

        public override object ReadObject(System.Xml.XmlDictionaryReader reader, bool verifyObjectName)
        {
            throw new NotImplementedException();
        }

        public override void WriteEndObject(System.Xml.XmlDictionaryWriter writer)
        {
            // All content written in WriteObjectContent
        }

        public override void WriteObjectContent(System.Xml.XmlDictionaryWriter writer, object graph)
        {
            this._writeContent(writer, graph as T);
        }

        public override void WriteStartObject(System.Xml.XmlDictionaryWriter writer, object graph)
        {
            // All content written in WriteObjectContent
        }
    }
}