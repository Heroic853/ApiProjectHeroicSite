using System.Diagnostics;
using System.Security.Claims;

namespace WebApi.Middleware
{
    /// <summary>
    /// Logga ogni richiesta che entra e ogni risposta che esce, con il tempo impiegato.
    /// Serve per vedere in console cosa sta succedendo e quali chiamate sono lente.
    /// </summary>
    public class RequestLoggingMiddleware
    {
        // Oltre questa soglia la chiamata viene loggata come Warning
        private const int SlowRequestMs = 1000;

        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Ignora le richieste di keep-alive per non sporcare il log ogni 10 minuti
            var path = context.Request.Path.Value ?? "/";
            if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var traceId = context.TraceIdentifier;
            var method = context.Request.Method;

            _logger.LogInformation("--> {Method} {Path} [{TraceId}]", method, path, traceId);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _next(context);
                stopwatch.Stop();

                var status = context.Response.StatusCode;
                var elapsed = stopwatch.ElapsedMilliseconds;
                var who = Describe(context.User);

                if (elapsed >= SlowRequestMs)
                {
                    _logger.LogWarning(
                        "<-- {Method} {Path} {Status} in {Elapsed}ms SLOW (user: {User}) [{TraceId}]",
                        method, path, status, elapsed, who, traceId);
                }
                else if (status >= 500)
                {
                    _logger.LogError(
                        "<-- {Method} {Path} {Status} in {Elapsed}ms (user: {User}) [{TraceId}]",
                        method, path, status, elapsed, who, traceId);
                }
                else if (status >= 400)
                {
                    _logger.LogWarning(
                        "<-- {Method} {Path} {Status} in {Elapsed}ms (user: {User}) [{TraceId}]",
                        method, path, status, elapsed, who, traceId);
                }
                else
                {
                    _logger.LogInformation(
                        "<-- {Method} {Path} {Status} in {Elapsed}ms (user: {User}) [{TraceId}]",
                        method, path, status, elapsed, who, traceId);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex,
                    "xxx {Method} {Path} EXPLODED after {Elapsed}ms (user: {User}) [{TraceId}]",
                    method, path, stopwatch.ElapsedMilliseconds, Describe(context.User), traceId);

                if (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "Internal server error",
                        traceId
                    });
                }
            }
        }

        /// <summary>
        /// Descrive chi ha fatto la chiamata senza stampare dati sensibili.
        /// </summary>
        private static string Describe(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return "anonymous";

            var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value
                          ?? "authenticated";

            var roles = user.FindAll(ClaimTypes.Role).Select(r => r.Value).ToArray();
            return roles.Length > 0 ? $"{subject} [{string.Join(",", roles)}]" : subject;
        }
    }
}
