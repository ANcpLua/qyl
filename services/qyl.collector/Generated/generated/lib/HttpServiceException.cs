#nullable enable

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TypeSpec.Helpers
{
    public class HttpServiceException : Exception
    {
        public HttpServiceException(int statusCode, object? value = null, Dictionary<string, string>? headers = null) =>
            (StatusCode, Value, Headers) = (statusCode, value, headers ?? new Dictionary<string, string>());

        public int StatusCode { get; }

        public object? Value { get; }

        public Dictionary<string, string> Headers { get; }
    }

    public class HttpServiceExceptionFilter : IActionFilter, IOrderedFilter
    {
        public int Order => int.MaxValue - 10;

        public void OnActionExecuting(ActionExecutingContext context) { }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Exception is HttpServiceException httpServiceException)
            {
                foreach (var header in httpServiceException.Headers)
                {
                    context.HttpContext.Response.Headers.Append(header.Key, header.Value.ToString());
                }

                context.Result = new ObjectResult(httpServiceException.Value)
                {
                    StatusCode = httpServiceException.StatusCode
                };

                context.ExceptionHandled = true;
            }
        }
    }
}
