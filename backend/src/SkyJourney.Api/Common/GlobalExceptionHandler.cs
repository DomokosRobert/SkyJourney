using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkyJourney.Application.Common;
using SkyJourney.Domain.Common;

namespace SkyJourney.Api.Common;

/// <summary>
/// Maps exceptions to RFC 9457 problem details. Unknown exceptions become a generic 500 response;
/// their details are logged but never returned to the client.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => CreateValidationProblem(validation),
            NotFoundException notFound => Problem(StatusCodes.Status404NotFound, "Not found", notFound.Message, "not_found"),
            ConflictException conflict => Problem(StatusCodes.Status409Conflict, "Conflict", conflict.Message, conflict.Code),
            DomainException domain => Problem(StatusCodes.Status409Conflict, "Business rule violated", domain.Message, domain.Code),
            UnprocessableException unprocessable => Problem(StatusCodes.Status422UnprocessableEntity, "Unprocessable request", unprocessable.Message, unprocessable.Code),
            ForbiddenException forbidden => Problem(StatusCodes.Status403Forbidden, "Forbidden", forbidden.Message, "forbidden"),
            DbUpdateConcurrencyException => Problem(StatusCodes.Status409Conflict, "Conflict",
                "The resource was modified by another request. Reload it and try again.", "concurrency_conflict"),
            BadHttpRequestException badRequest => Problem(StatusCodes.Status400BadRequest, "Bad request", badRequest.Message, "bad_request"),
            _ => null,
        };

        if (problem is null)
        {
            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            {
                // Client went away; nothing to respond to.
                httpContext.Response.StatusCode = RequestLogLevel.ClientClosedRequest;
                return true;
            }

            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, "Server error",
                "An unexpected error occurred. Quote the correlation id when reporting this problem.", "server_error");
        }
        else
        {
            logger.LogInformation("Request failed with {StatusCode} {ErrorCode}", problem.Status, problem.Extensions["code"]);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail, string code) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Extensions = { ["code"] = code },
    };

    private static HttpValidationProblemDetails CreateValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return new HttpValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more fields are invalid.",
            Extensions = { ["code"] = "validation_failed" },
        };
    }

    private static string ToCamelCase(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(part =>
            part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
}
