using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Services;

internal abstract class ResearchProviderBase : IResearchProvider
{
    private static readonly HttpClient SharedHttpClient = new();

    public abstract string ProviderName { get; }

    public abstract int ProviderRank { get; }

    public abstract Task<ResearchTaskResult> SearchPoiAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken);

    public abstract Task<ResearchTaskResult> QueryDistrictAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken);

    public abstract Task<ResearchTaskResult> GeocodeAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken);

    public abstract Task<ResearchTaskResult> ReverseGeocodeAsync(ResearchQuery query, string apiKey, CancellationToken cancellationToken);

    protected async Task<JsonDocument> GetJsonAsync(string requestUri, CancellationToken cancellationToken)
    {
        using var response = await SharedHttpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
    }

    protected static string BuildUri(string baseUri, IReadOnlyDictionary<string, string> parameters)
    {
        var builder = new StringBuilder(baseUri);
        builder.Append('?');

        var index = 0;
        foreach (var parameter in parameters)
        {
            if (index > 0)
            {
                builder.Append('&');
            }

            builder.Append(Uri.EscapeDataString(parameter.Key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(parameter.Value));
            index++;
        }

        return builder.ToString();
    }

    protected static string CombineRegion(string province, string city, string district)
    {
        return string.Join(
            " / ",
            new[] { province, city, district }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    protected static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString() ?? string.Empty
            : string.Empty;
    }

    protected static double? GetDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var numericValue))
        {
            return numericValue;
        }

        if (property.ValueKind == JsonValueKind.String &&
            double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out numericValue))
        {
            return numericValue;
        }

        return null;
    }

    protected static string ToRawPayload(JsonElement element)
    {
        return element.GetRawText();
    }

    protected static ResearchTaskResult CreateFailure(string summary, Exception exception)
    {
        return new ResearchTaskResult(
            [],
            [],
            [$"{exception.GetType().Name}: {exception.Message}"],
            summary);
    }
}
