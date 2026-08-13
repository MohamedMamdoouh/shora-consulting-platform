using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shora.Tests.Common;

public static class ApiTestJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public static class ApiTestHttpExtensions
{
    public static Task<T?> ReadApiJsonAsync<T>(
        this HttpContent content,
        CancellationToken cancellationToken = default) =>
        content.ReadFromJsonAsync<T>(ApiTestJson.Options, cancellationToken);

    public static Task<HttpResponseMessage> PostApiJsonAsync<T>(
        this HttpClient client,
        string? requestUri,
        T value,
        CancellationToken cancellationToken = default) =>
        client.PostAsJsonAsync(requestUri, value, ApiTestJson.Options, cancellationToken);
}
