using ClothingShop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stripe;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/stripe/webhook")]
[AllowAnonymous]
public class StripeWebhookController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(OrderService orderService, IConfiguration configuration, ILogger<StripeWebhookController> logger)
    {
        _orderService = orderService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        // Read raw body to satisfy Stripe signature validation
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var sigHeader = Request.Headers["Stripe-Signature"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sigHeader))
        {
            _logger.LogWarning("Stripe webhook request missing signature header.");
            return Unauthorized();
        }

        var secret = ResolveWebhookSecret();
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogError("Stripe webhook secret is not configured. Check environment variable 'Stripe:WebhookSecret' or 'STRIPE_WEBHOOK_SECRET'.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Stripe webhook secret not configured");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, sigHeader, secret, throwOnApiVersionMismatch: false);
            _logger.LogInformation("Stripe webhook received: {EventType} for order {ClientReferenceId}", 
                stripeEvent.Type, 
                stripeEvent.Data.Object is Stripe.Checkout.Session s ? s.ClientReferenceId : "unknown");
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature validation failed.");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe webhook payload processing failed.");
            return BadRequest();
        }

        if (stripeEvent.Type == Events.CheckoutSessionCompleted)
        {
            var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
            if (session?.ClientReferenceId != null && Guid.TryParse(session.ClientReferenceId, out var orderId))
            {
                await _orderService.UpdateOrderStatusAsync(orderId, "paid");
                _logger.LogInformation("Order {OrderId} updated to paid (checkout.session.completed)", orderId);
            }
        }
        else if (stripeEvent.Type == Events.PaymentIntentSucceeded)
        {
            var intent = stripeEvent.Data.Object as Stripe.PaymentIntent;
            var orderIdStr = intent?.Metadata != null && intent.Metadata.TryGetValue("orderId", out var value) ? value : null;
            if (orderIdStr != null && Guid.TryParse(orderIdStr, out var orderId))
            {
                await _orderService.UpdateOrderStatusAsync(orderId, "paid");
                _logger.LogInformation("Order {OrderId} updated to paid (payment_intent.succeeded)", orderId);
            }
        }

        return Ok();
    }

    private string? ResolveWebhookSecret()
    {
        var configured = _configuration["Stripe:WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        configured = _configuration["Stripe__WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var env = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET");
        return string.IsNullOrWhiteSpace(env) ? null : env;
    }
}


