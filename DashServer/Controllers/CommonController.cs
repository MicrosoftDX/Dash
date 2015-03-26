//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Microsoft.Dash.Server.Diagnostics;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;

namespace Microsoft.Dash.Server.Controllers
{
    public class CommonController : ApiController
    {
        protected HttpRequestBase RequestFromContext(HttpContextBase context)
        {
            return context.Request;
        }

        protected HttpResponseMessage CreateResponse<T>(T result)
        {
            return CreateResponse(result, HttpStatusCode.OK);
        }

        protected HttpResponseMessage CreateResponse<T>(T result, HttpStatusCode status)
        {
            var response = this.Request.CreateResponse(status, result, GlobalConfiguration.Configuration.Formatters.XmlFormatter, "application/xml");
            response.AddStandardResponseHeaders(this.Request.GetHeaders());
            return response;
        }

        protected HttpResponseMessage CreateResponse(HttpStatusCode status, RequestHeaders requestHeaders)
        {
            var response = this.Request.CreateResponse(status);
            response.AddStandardResponseHeaders(requestHeaders ?? this.Request.GetHeaders());
            return response;
        }

        protected async Task<IHttpActionResult> DoHandlerAsync(string handlerName, Func<Task<HandlerResult>> handler)
        {
            return ProcessHandlerResult(await OperationRunner.DoHandlerAsync(handlerName, handler));
        }

        protected IHttpActionResult ProcessHandlerResult(HandlerResult result)
        {
            switch (result.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    return NotFound();

                case HttpStatusCode.Redirect:
                    return Redirect(result.Location);

                case HttpStatusCode.Accepted:
                case HttpStatusCode.Created:
                case HttpStatusCode.InternalServerError:
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Conflict:
                    return ResponseMessage(ProcessResultResponse(result));

                default:
                    System.Diagnostics.Debug.Assert(false);
                    return ResponseMessage(ProcessResultResponse(result));
            }
        }

        protected async Task<HttpResponseMessage> DoHandlerAsync(string handlerName, Func<Task<HttpResponseMessage>> handler)
        {
            return await OperationRunner.DoActionAsync(handlerName, handler, (ex) =>
                {
                    return ProcessResultResponse(HandlerResult.FromException(ex));
                });
        }

        protected HttpResponseMessage ProcessResultResponse(HandlerResult result)
        {
            var response = new HttpResponseMessage(result.StatusCode);
            if (!String.IsNullOrWhiteSpace(result.ReasonPhrase))
            {
                response.ReasonPhrase = result.ReasonPhrase;
            }
            if (result.Headers != null)
            {
                foreach (var header in result.Headers)
                {
                    response.Headers.TryAddWithoutValidation(header.Key, header);
                }
            }
            if (result.ErrorInformation != null && !String.IsNullOrWhiteSpace(result.ErrorInformation.ErrorCode))
            {
                var error = new HttpError
                {
                    { "Code", result.ErrorInformation.ErrorCode },
                    { "Message", result.ErrorInformation.ErrorMessage },
                };
                foreach (var msg in result.ErrorInformation.AdditionalDetails)
                {
                    error.Add(msg.Key, msg.Value);
                }
                response.Content = new ObjectContent<HttpError>(error, GlobalConfiguration.Configuration.Formatters.XmlFormatter, "application/xml");
            }
            return response;
        }
    }
}