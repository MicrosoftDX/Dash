//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Microsoft.Dash.Server.Utils
{
    public static class HttpContextFactory
    {
        static HttpContextBase _setContext;

        public static HttpContextBase Current
        {
            get
            {
                if (_setContext == null)
                {
                    lock (typeof(HttpContextFactory))
                    {
                        if (_setContext == null)
                        {
                            _setContext = new HttpContextWrapper(HttpContext.Current);
                        }
                    }
                }
                return _setContext;
            }
            set 
            {  
                lock (typeof(HttpContextFactory))
                {
                    _setContext = value;
                }
            }
        }
    }
}