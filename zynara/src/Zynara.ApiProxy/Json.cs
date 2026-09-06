using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker.Http;

namespace Zynara.ApiProxy;

/// <summary>JSON read/write for the HTTP layer — camelCase, string enums, so the dashboard gets readable values.</summary>
internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<HttpResponseData> Ok<T>(HttpRequestData req, T value)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        await WriteJson(res, value);
        return res;
    }

    public static async Task<HttpResponseData> Created<T>(HttpRequestData req, T value)
    {
        var res = req.CreateResponse(HttpStatusCode.Created);
        await WriteJson(res, value);
        return res;
    }

    public static async Task<HttpResponseData> Error(HttpRequestData req, HttpStatusCode code, string message)
    {
        var res = req.CreateResponse(code);
        await WriteJson(res, new { error = message });
        return res;
    }

    public static async Task<T?> ReadAsync<T>(HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        return string.IsNullOrWhiteSpace(body) ? default : JsonSerializer.Deserialize<T>(body, Options);
    }

    private static async Task WriteJson<T>(HttpResponseData res, T value)
    {
        res.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await res.WriteStringAsync(JsonSerializer.Serialize(value, Options));
    }
}
