using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace TireForge.ApiProxy;

/// <summary>
/// Maps the domain's exceptions (<see cref="TireForge.Core.Reviewing.Reviewer"/>
/// throws plain <see cref="InvalidOperationException"/> / <see cref="ArgumentException"/>)
/// onto HTTP problem responses. Pure and unit-tested — the functions just call it.
/// </summary>
public static class HttpProblem
{
    public static (int Status, string Title) Classify(Exception ex) => ex switch
    {
        ArgumentException => (400, "Invalid request"),
        JsonException => (400, "Malformed JSON body"),
        InvalidOperationException e when e.Message.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)
            => (404, "Not found"),
        InvalidOperationException => (409, "Conflicting state"),
        _ => (500, "Unexpected error"),
    };

    public static ProblemDetails Details(Exception ex)
    {
        var (status, title) = Classify(ex);
        return new ProblemDetails { Status = status, Title = title, Detail = ex.Message };
    }
}
