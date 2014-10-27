//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace Microsoft.Dash.Server.Controllers
{
    public class ContainerController : ApiController
    {
        public async Task<IHttpActionResult> Get()
        {
            await Task.Delay(10);
            return Ok();
        }
    }
}
