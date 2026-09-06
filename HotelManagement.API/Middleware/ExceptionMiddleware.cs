using System.Net;
using System.Text.Json;
using HotelManagement.Application.Common.Models;

namespace HotelManagement.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    { _next = next; _logger = logger; }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);

            // The handlers use `throw new Exception("...")` for business-rule
            // violations (e.g. "Check-out date must be after check-in date").
            // Surface those as a 400 with the message; keep everything else generic.
            var isBusinessRule = ex.GetType() == typeof(Exception);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = isBusinessRule
                ? (int)HttpStatusCode.BadRequest
                : (int)HttpStatusCode.InternalServerError;

            var response = ApiResponse<object>.Fail(isBusinessRule
                ? ex.Message
                : "An unexpected error occurred. Please try again.");

            await context.Response.WriteAsync(JsonSerializer.Serialize(response,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
