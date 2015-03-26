//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Microsoft.Dash.Server.Utils
{
    public abstract class RequestResponseItems : ILookup<string, string>, IEnumerable<IGrouping<string, string>>
    {
        ILookup<string, string> _items;

        protected RequestResponseItems(IEnumerable<KeyValuePair<string, string>> items)
        {
            _items = items
                .ToLookup(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        }

        protected RequestResponseItems(IEnumerable<KeyValuePair<string, IEnumerable<string>>> items)
        {
            _items = items
                .SelectMany(item => item.Value
                    .Select(itemValue => Tuple.Create(item.Key, itemValue)))
                .ToLookup(item => item.Item1, item => item.Item2, StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<string> this[string itemName]
        {
            get { return _items[itemName]; }
        }

        public bool Contains(string key)
        {
            return _items.Contains(key);
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public IEnumerator<IGrouping<string, string>> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
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
            var values = _items[itemName];
            if (values != null)
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
                        catch
                        {
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