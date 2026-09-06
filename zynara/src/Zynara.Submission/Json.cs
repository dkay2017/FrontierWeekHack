using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker.Http;

namespace Zynara.Submission;

/// <summary>JSON write for the HTTP layer — camelCase, string enums.</summary>
internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Task<HttpResponseData> Ok<T>(HttpRequestData req, T value) => Write(req, HttpStatusCode.OK, value);

    public static Task<HttpResponseData> Error(HttpRequestData req, HttpStatusCode code, string message) =>
        Write(req, code, new { error = message });

    private static async Task<HttpResponseData> Write<T>(HttpRequestData req, HttpStatusCode code, T value)
    {
        var res = req.CreateResponse(code);
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await res.WriteStringAsync(JsonSerializer.Serialize(value, Options));
        return res;
    }
}
