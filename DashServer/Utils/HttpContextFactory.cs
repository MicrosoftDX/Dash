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
                if (_setContext != null)
                {
                    return _setContext;
                }
                System.Diagnostics.Debug.Assert(HttpContext.Current != null);
                return new HttpContextWrapper(HttpContext.Current);
            }
            set 
            {  
                _setContext = value;
            }
        }
    }
}