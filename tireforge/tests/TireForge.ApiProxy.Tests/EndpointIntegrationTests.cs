using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TireForge.ApiProxy;
using TireForge.Core.Reporting;
using TireForge.Data;
using TireForge.TestSupport;

namespace TireForge.ApiProxy.Tests;

/// <summary>
/// Drives the function classes over a real seeded SQL Server DB (Testcontainers)
/// and the same DI container <c>Program.cs</c> builds — the HTTP wiring without a
/// Functions host.
/// </summary>
public sealed class EndpointIntegrationTests : IAsyncLifetime, IDisposable
{
    private ServiceProvider _sp = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddTireForgeData(await SqlServer.NewConnectionStringAsync());
        services.AddScoped<ReportsFunctions>();
        services.AddScoped<ReviewFunctions>();
        _sp = services.BuildServiceProvider();
        await _sp.InitializeTireForgeDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _sp?.Dispose();

    private static HttpRequest Req() => new DefaultHttpContext().Request;

    [Fact]
    public async Task Status_returns_the_five_seeded_machines()
    {
        using var scope = _sp.CreateScope();
        var fn = scope.ServiceProvider.GetRequiredService<ReportsFunctions>();

        var result = Assert.IsType<OkObjectResult>(await fn.Status(Req(), default));
        var body = Assert.IsType<StatusResponse>(result.Value);
        Assert.Equal(5, body.Machines.Count);
    }

    [Fact]
    public async Task Cost_endpoint_flags_token_metrics_unavailable()
    {
        using var scope = _sp.CreateScope();
        var fn = scope.ServiceProvider.GetRequiredService<ReportsFunctions>();

        var result = Assert.IsType<OkObjectResult>(await fn.Cost(Req(), default));
        var body = Assert.IsType<CostResponse>(result.Value);
        Assert.False(body.TokenMetricsAvailable);
        Assert.Equal(3, body.Agents.Count);
    }

    [Fact]
    public async Task Approving_an_unknown_diagnosis_is_404()
    {
        using var scope = _sp.CreateScope();
        var fn = scope.ServiceProvider.GetRequiredService<ReviewFunctions>();

        var req = Req();
        req.Body = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes("""{"diagnosisId":"dx-nope","reviewer":"alice"}"""));

        var result = Assert.IsType<ObjectResult>(await fn.Approve(req, default));
        Assert.Equal(404, result.StatusCode);
        Assert.IsType<ProblemDetails>(result.Value);
    }

    [Fact]
    public async Task Rejecting_without_a_note_is_400()
    {
        using var scope = _sp.CreateScope();
        var fn = scope.ServiceProvider.GetRequiredService<ReviewFunctions>();

        var req = Req();
        req.Body = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes("""{"diagnosisId":"dx-1","reviewer":"alice","note":""}"""));

        var result = Assert.IsType<ObjectResult>(await fn.Reject(req, default));
        Assert.Equal(400, result.StatusCode);
    }
}
