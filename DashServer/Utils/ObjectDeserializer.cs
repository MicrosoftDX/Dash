//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Microsoft.Dash.Common.Diagnostics;

namespace Microsoft.Dash.Server.Utils
{
    public class ObjectDeserializer
    {
        public static void ReadCollection<T>(XmlReader reader, ICollection<T> collection, Action<T, string, string> propertyAssigner, IDictionary<string, Action<XmlReader, T>> subObjects = null) where T : new()
        {
            // Assumes reader is positioned AT the opening tag of the object. Will continue to consume property values until it sees 
            // this value as a closing tag.
            // May be re-entrant.
            if (reader.IsStartElement())
            {
                string endTag = reader.Name;
                reader.Read();
                reader.MoveToContent();
                while (reader.Name != endTag)
                {
                    T newItem = new T();
                    ReadObject(reader, newItem, propertyAssigner, subObjects);
                    collection.Add(newItem);
                    reader.MoveToContent();
                }
                reader.ReadEndElement();
            }
        }

        public static void ReadObject<T>(XmlReader reader, T objectToRead, Action<T, string, string> propertyAssigner, IDictionary<string, Action<XmlReader, T>> subObjects = null)
        {
            // Assumes reader is positioned AT the opening tag of the object. Will continue to consume property values until it sees 
            // this value as a closing tag.
            // May be re-entrant.
            if (reader.IsStartElement())
            {
                string endTag = reader.Name;
                reader.Read();
                reader.MoveToContent();
                while (reader.Name != endTag)
                {
                    string attributeName = reader.Name;
                    if (subObjects != null && subObjects.ContainsKey(attributeName))
                    {
                        subObjects[attributeName](reader, objectToRead);
                    }
                    else
                    {
                        string attributeValue = reader.ReadElementString();
                        propertyAssigner(objectToRead, attributeName.ToLowerInvariant(), attributeValue);
                    }
                    reader.MoveToContent();
                }
                reader.ReadEndElement();
            }
        }

        public static T TranslateEnumFlags<T, S>(IEnumerable<S> source, IDictionary<S, T> lookup, Func<T, T, T> aggregateFunc, string attributeName) 
        {
            return source
                .Aggregate(default(T), (accumulatedFlags, value) =>
                {
                    T parsedFlag;
                    if (lookup.TryGetValue(value, out parsedFlag))
                    {
                        accumulatedFlags = aggregateFunc(accumulatedFlags, parsedFlag);
                    }
                    else
                    {
                        DashTrace.TraceWarning("Unsupported value specified for {0}: {1}", attributeName, value);
                    }
                    return accumulatedFlags;
                });
        }
    }
}