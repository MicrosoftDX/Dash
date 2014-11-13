using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Microsoft.Dash.Server.Controllers
{
    public class CommonController: ApiController
    {
        protected HttpResponseMessage CreateResponse<T>(T result)
        {
            return CreateResponse(result, HttpStatusCode.OK);
        }

        protected HttpResponseMessage CreateResponse<T>(T result, HttpStatusCode status)
        {
            var response = this.Request.CreateResponse(status, result, GlobalConfiguration.Configuration.Formatters.XmlFormatter, "application/xml");
            response.Headers.TryAddWithoutValidation("x-ms-version", "2014-02-14");
            response.Headers.TryAddWithoutValidation("x-ms-date", DateTimeOffset.UtcNow.ToString("r"));
            return response;
        }
    }
}