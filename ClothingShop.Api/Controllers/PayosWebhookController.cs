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
    // PayOS webhook verification: HMAC-SHA256 over the JSON of `data` using ChecksumKey; compare with `signature` in body.
    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        // Parse JSON payload
        var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;
        var hasData = root.TryGetProperty("data", out var data);
        var hasSignature = root.TryGetProperty("signature", out var sigEl);
        var bodySignature = hasSignature ? sigEl.GetString() : null;

        // Fallback to headers if body signature missing
        if (string.IsNullOrWhiteSpace(bodySignature))
        {
            bodySignature = Request.Headers["X-Payos-Signature"].FirstOrDefault()
                ?? Request.Headers["x-api-signature"].FirstOrDefault();
        }

        var secret = configuration["PayOS:ChecksumKey"];
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(bodySignature) || !hasData)
        {
            return Unauthorized();
        }

        // Verify signature on raw `data` JSON
        var dataRaw = data.GetRawText();
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
        {
            var computedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataRaw));
            try
            {
                var signatureBytes = Convert.FromHexString(bodySignature.Trim().ToLowerInvariant());
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

        string? orderCode = null;
        if (data.TryGetProperty("orderCode", out var oc)) orderCode = oc.GetString();
        if (string.IsNullOrWhiteSpace(orderCode)) return BadRequest();

        if (!Guid.TryParse(orderCode, out var id))
        {
            // orderCode may be format N (no dashes)
            if (!Guid.TryParseExact(orderCode, "N", out id)) return BadRequest();
        }

        // Determine payment result per docs: data.code == "00" means success
        string newStatus = "failed";
        if (data.TryGetProperty("code", out var codeEl))
        {
            var codeStr = codeEl.GetString();
            if (string.Equals(codeStr, "00", StringComparison.OrdinalIgnoreCase))
            {
                newStatus = "paid";
            }
        }
        else if (root.TryGetProperty("success", out var successEl) && successEl.GetBoolean())
        {
            newStatus = "paid";
        }

        await orderService.UpdateOrderStatusAsync(id, newStatus);
        logger.LogInformation("PayOS Webhook - Updated order {OrderId} to {Status}", id, newStatus);

        return Ok(new { ok = true });
    }
}


