//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Dash.Common.Diagnostics;

namespace Microsoft.Dash.Server.Utils
{
    public abstract class RequestResponseItems : ILookup<string, string>, IEnumerable<IGrouping<string, string>>
    {
        IDictionary<string, IEnumerable<string>> _items;

        protected RequestResponseItems(IEnumerable<KeyValuePair<string, string>> items)
        {
            _items = items
                .GroupBy(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(item => item.Key, item => item.AsEnumerable(), StringComparer.OrdinalIgnoreCase);
        }

        protected RequestResponseItems(IEnumerable<KeyValuePair<string, IEnumerable<string>>> items)
        {
            _items = items
                .GroupBy(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(groupedItems => groupedItems.Key, groupedItems => groupedItems.SelectMany(keyItems => keyItems), StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<string> this[string itemName]
        {
            get { return _items[itemName]; }
            set { _items[itemName] = value; }
        }

        public bool Contains(string key)
        {
            return _items.ContainsKey(key);
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public IEnumerator<IGrouping<string, string>> GetEnumerator()
        {
            return _items
                .SelectMany(keyValue => keyValue.Value, Tuple.Create)
                .ToLookup(item => item.Item1.Key, item => item.Item2)
                .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public T Value<T>(string itemName) where T : IConvertible
        {
            return Value(itemName, default(T));
        }

        public T Value<T>(string itemName, T defaultValue) where T : IConvertible
        {
            var values = Values<T>(itemName);
            if (values != null && values.Any())
            {
                return values.First();
            }
            return defaultValue;
        }

        public Nullable<T> ValueOrNull<T>(string itemName) where T : struct, IConvertible
        {
            var values = Values<T>(itemName);
            if (values != null && values.Any())
            {
                return values.First();
            }
            return null;
        }

        public IEnumerable<T> Values<T>(string itemName) where T : IConvertible
        {
            IEnumerable<string> values = null;
            if (_items.TryGetValue(itemName, out values))
            {
                return values
                    .Select(value =>
                    {
                        try
                        {
                            if (typeof(T).IsEnum)
                            {
                                return (T)Enum.Parse(typeof(T), value);
                            }
                            return (T)Convert.ChangeType(value, typeof(T));
                        }
                        catch (Exception ex)
                        {
                            DashTrace.TraceWarning(new TraceMessage
                            {
                                Operation = "General Exception",
                                Message = String.Format("Failure converting item type. Item: {0}, new type: {1}", value, typeof(T).Name),
                                ErrorDetails = new DashErrorInformation { ErrorMessage = ex.ToString() },
                            });
                        }
                        return default(T);
                    });
            }
            return Enumerable.Empty<T>();
        }

        public TimeSpan? TimeSpanFromSeconds(string itemName)
        {
            int durationInSecs = this.Value(itemName, -1);
            return durationInSecs == -1 ? (TimeSpan?)null : TimeSpan.FromSeconds(durationInSecs);
        }
    }
}