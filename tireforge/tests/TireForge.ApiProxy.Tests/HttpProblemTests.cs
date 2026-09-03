using System.Text.Json;
using TireForge.ApiProxy;

namespace TireForge.ApiProxy.Tests;

/// <summary>The exception → HTTP status mapping the reviewer endpoints rely on (D10).</summary>
public class HttpProblemTests
{
    [Fact]
    public void Argument_errors_are_400()
        => Assert.Equal(400, HttpProblem.Classify(new ArgumentException("A rejection must carry a note.")).Status);

    [Fact]
    public void Malformed_json_is_400()
    {
        var ex = Record.Exception(() => JsonSerializer.Deserialize<int>("{ not json"))!;
        Assert.Equal(400, HttpProblem.Classify(ex).Status);
    }

    [Fact]
    public void Unknown_entity_is_404()
        => Assert.Equal(404, HttpProblem.Classify(new InvalidOperationException("Unknown diagnosis 'dx-1'.")).Status);

    [Fact]
    public void Bad_state_transition_is_409()
        => Assert.Equal(409, HttpProblem.Classify(
            new InvalidOperationException("Diagnosis 'dx-1' is 'Approved', not awaiting review.")).Status);

    [Fact]
    public void Unexpected_errors_are_500()
        => Assert.Equal(500, HttpProblem.Classify(new Exception("boom")).Status);

    [Fact]
    public void Details_carry_status_and_message()
    {
        var d = HttpProblem.Details(new InvalidOperationException("Unknown work order 'wo-9'."));
        Assert.Equal(404, d.Status);
        Assert.Equal("Unknown work order 'wo-9'.", d.Detail);
        Assert.False(string.IsNullOrWhiteSpace(d.Title));
    }
}
