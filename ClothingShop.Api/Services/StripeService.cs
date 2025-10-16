using ClothingShop.Api.Models;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;

namespace ClothingShop.Api.Services;

public class StripeService
{
    private readonly StripeClient _client;
    private readonly string _successUrlTemplate;
    private readonly string _cancelUrlTemplate;

    public StripeService(IConfiguration configuration)
    {
        var apiKey = configuration["Stripe:ApiKey"]
            ?? throw new InvalidOperationException("Stripe:ApiKey is required");
        _client = new StripeClient(apiKey);
        _successUrlTemplate = configuration["Stripe:SuccessUrl"] ?? "https://example.com/success?orderId={ORDER_ID}";
        _cancelUrlTemplate = configuration["Stripe:CancelUrl"] ?? "https://example.com/cancel?orderId={ORDER_ID}";
    }

    public async Task<Session> CreateCheckoutSessionAsync(Order order, CancellationToken ct = default)
    {
        var lineItems = order.Items.Select(i => new SessionLineItemOptions
        {
            Quantity = i.Quantity,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = "usd",
                UnitAmountDecimal = (long)Math.Round(i.UnitPrice * 100),
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = i.ProductId.ToString()
                }
            }
        }).ToList();

        var successUrl = _successUrlTemplate.Replace("{ORDER_ID}", order.Id.ToString());
        var cancelUrl = _cancelUrlTemplate.Replace("{ORDER_ID}", order.Id.ToString());

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = order.Id.ToString(),
            LineItems = lineItems
        };

        var service = new SessionService(_client);
        return await service.CreateAsync(options, null, ct);
    }
}


