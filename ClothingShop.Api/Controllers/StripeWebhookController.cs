using ClothingShop.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/stripe/webhook")]
public class StripeWebhookController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly IConfiguration _configuration;

    public StripeWebhookController(OrderService orderService, IConfiguration configuration)
    {
        _orderService = orderService;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        // Read raw body to satisfy Stripe signature validation
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var sigHeader = Request.Headers["Stripe-Signature"].FirstOrDefault();
        var secret = _configuration["Stripe:WebhookSecret"]; 
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, sigHeader, secret);
        }
        catch
        {
            return Unauthorized();
        }

        if (stripeEvent.Type == Events.CheckoutSessionCompleted)
        {
            var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
            if (session?.ClientReferenceId != null && Guid.TryParse(session.ClientReferenceId, out var orderId))
            {
                await _orderService.UpdateOrderStatusAsync(orderId, "paid");
            }
        }
        else if (stripeEvent.Type == Events.PaymentIntentSucceeded)
        {
            var intent = stripeEvent.Data.Object as Stripe.PaymentIntent;
            var orderIdStr = intent?.Metadata != null && intent.Metadata.TryGetValue("orderId", out var value) ? value : null;
            if (orderIdStr != null && Guid.TryParse(orderIdStr, out var orderId))
            {
                await _orderService.UpdateOrderStatusAsync(orderId, "paid");
            }
        }

        return Ok();
    }
}


