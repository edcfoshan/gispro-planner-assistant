using System.Threading;
using GisProAddinWorkbench.AddIn.Models;

namespace GisProAddinWorkbench.AddIn.Services;

internal sealed class ResearchCoordinator
{
    private static int _baiduKeyCursor;
    private static int _amapKeyCursor;

    internal async Task<ResearchTaskResult> ExecuteAsync(
        ResearchQuery query,
        ResearchSettings settings,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var records = new List<ResearchRecord>();
        var summaries = new List<string>();
        var tasks = new List<Task<ResearchTaskResult>>();

        foreach (var provider in ResolveProviders(query.ProviderType))
        {
            var key = ResolveApiKey(provider, settings);
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"{provider.ProviderName} 缺少可用 Key，请先在 research-settings.json 中配置。");
                continue;
            }

            tasks.Add(ExecuteProviderAsync(provider, query, key, cancellationToken));
        }

        if (tasks.Count == 0)
        {
            return new ResearchTaskResult([], warnings, errors, "没有可执行的数据源。");
        }

        var providerResults = await Task.WhenAll(tasks);
        foreach (var result in providerResults)
        {
            summaries.Add(result.RequestSummary);
            warnings.AddRange(result.Warnings);
            errors.AddRange(result.Errors);
            records.AddRange(result.Records);
        }

        var orderedRecords = records
            .OrderBy(record => record.ProviderRank)
            .ThenBy(record => record.CreatedAt)
            .ToList();

        return new ResearchTaskResult(
            orderedRecords,
            warnings,
            errors,
            string.Join(" | ", summaries.Where(summary => !string.IsNullOrWhiteSpace(summary))));
    }

    private static IEnumerable<IResearchProvider> ResolveProviders(ResearchProviderType providerType)
    {
        return providerType switch
        {
            ResearchProviderType.Baidu => [new BaiduResearchProvider()],
            ResearchProviderType.Amap => [new AmapResearchProvider()],
            _ => [new BaiduResearchProvider(), new AmapResearchProvider()]
        };
    }

    private static async Task<ResearchTaskResult> ExecuteProviderAsync(
        IResearchProvider provider,
        ResearchQuery query,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds = query.RequestTimeoutSeconds > 0 ? query.RequestTimeoutSeconds : 30;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            return query.TaskType switch
            {
                ResearchTaskType.Poi => await provider.SearchPoiAsync(query, apiKey, timeoutCts.Token),
                ResearchTaskType.District => await provider.QueryDistrictAsync(query, apiKey, timeoutCts.Token),
                ResearchTaskType.Geocode when query.GeocodeMode == GeocodeMode.Reverse => await provider.ReverseGeocodeAsync(query, apiKey, timeoutCts.Token),
                _ => await provider.GeocodeAsync(query, apiKey, timeoutCts.Token)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ResearchTaskResult(
                [],
                [],
                [$"{provider.ProviderName} 请求超时（{timeoutSeconds}s）。"],
                $"{provider.ProviderName} 超时");
        }
        catch (Exception exception)
        {
            return new ResearchTaskResult(
                [],
                [],
                [$"{provider.ProviderName} 执行异常: {exception.Message}"],
                $"{provider.ProviderName} 异常");
        }
    }

    private static string ResolveApiKey(IResearchProvider provider, ResearchSettings settings)
    {
        var keyPool = provider.ProviderRank switch
        {
            1 => settings.EffectiveBaiduAk,
            2 => settings.EffectiveAmapKey,
            _ => []
        };

        var availableKeys = keyPool
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (availableKeys.Length == 0)
        {
            return string.Empty;
        }

        if (provider.ProviderRank == 1)
        {
            var index = Math.Abs(Interlocked.Increment(ref _baiduKeyCursor)) % availableKeys.Length;
            return availableKeys[index];
        }

        if (provider.ProviderRank == 2)
        {
            var index = Math.Abs(Interlocked.Increment(ref _amapKeyCursor)) % availableKeys.Length;
            return availableKeys[index];
        }

        return availableKeys[0];
    }
}
