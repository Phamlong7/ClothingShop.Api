using System.Net.Http.Json;

namespace ClothingShop.Api.Services;

public class ExchangeRateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public ExchangeRateService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<decimal> GetUsdToVndAsync(CancellationToken ct = default)
    {
        // Public API: exchangerate.host (no key, free)
        var client = _httpClientFactory.CreateClient("exchange");
        var resp = await client.GetFromJsonAsync<ExchangeResponse>("/latest?base=USD&symbols=VND", ct);
        if (resp?.Rates is not null && resp.Rates.TryGetValue("VND", out var rate))
        {
            return rate;
        }

        // Fallback to configured rate
        var fallback = _configuration.GetSection("VnPay")["UsdToVndRate"];
        if (decimal.TryParse(fallback, out var fallbackRate) && fallbackRate > 0)
            return fallbackRate;

        // Safe default
        return 25000m;
    }

    private sealed class ExchangeResponse
    {
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}


