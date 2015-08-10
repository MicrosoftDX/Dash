//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Web.Http;
using Microsoft.Dash.Common.Utils;

namespace DashServer.ManagementAPI.Controllers
{
    public class AuthConfigController : ApiController
    {
        [HttpGet, ActionName("Index")]
        public IHttpActionResult GetAuthConfig()
        {
            return Ok(new
            {
                Tenant = DashConfiguration.Tenant,
                ClientId = DashConfiguration.ClientId,
            });
        }
    }
}