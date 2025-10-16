using System.Security.Cryptography;
using System.Text;
using ClothingShop.Api.Data;
using ClothingShop.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/payos/webhook")]
public class PayosWebhookController(OrderService orderService, IConfiguration configuration, ILogger<PayosWebhookController> logger) : ControllerBase
{
    // PayOS sends webhook with payload and signature header. Verify and mark order paid.
    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-Payos-Signature"].ToString();
        // PayOS uses ChecksumKey for webhook signature verification
        var secret = configuration["PayOS:ChecksumKey"];

        if (!string.IsNullOrWhiteSpace(secret) && !string.IsNullOrWhiteSpace(signature))
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var computedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            try
            {
                var signatureBytes = Convert.FromHexString(signature.Trim().ToLowerInvariant());
                if (signatureBytes.Length != computedBytes.Length || !CryptographicOperations.FixedTimeEquals(computedBytes, signatureBytes))
                {
                    return Unauthorized();
                }
            }
            catch
            {
                return Unauthorized();
            }
        }

        // Parse JSON payload (be tolerant to structure changes)
        var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;
        System.Text.Json.JsonElement data = root;
        if (root.TryGetProperty("data", out var d)) data = d;

        string? orderCode = null;
        if (data.TryGetProperty("orderCode", out var oc)) orderCode = oc.GetString();
        else if (root.TryGetProperty("orderCode", out var oc2)) orderCode = oc2.GetString();

        string? status = null;
        if (data.TryGetProperty("status", out var st)) status = st.GetString();
        else if (root.TryGetProperty("status", out var st2)) status = st2.GetString();
        if (string.IsNullOrWhiteSpace(orderCode)) return BadRequest();

        if (!Guid.TryParse(orderCode, out var id)) return BadRequest();

        // Debug logging
        logger.LogInformation("PayOS Webhook - OrderCode: {OrderCode}, Status: {Status}", orderCode, status);

        if (!string.IsNullOrWhiteSpace(status))
        {
            string? newStatus = status.ToUpper() switch
            {
                "PAID" => "paid",
                "CANCELLED" or "FAILED" or "EXPIRED" => "failed",
                _ => null
            };

            if (newStatus is not null)
            {
                await orderService.UpdateOrderStatusAsync(id, newStatus);
                logger.LogInformation("PayOS Webhook - Updated order {OrderId} to {Status}", id, newStatus);
            }
            else
            {
                logger.LogWarning("PayOS Webhook - Unknown status {Status} for order {OrderId}", status, id);
            }
        }
        else
        {
            logger.LogWarning("PayOS Webhook - No status provided for order {OrderId}", id);
        }

        return Ok(new { ok = true });
    }
}


