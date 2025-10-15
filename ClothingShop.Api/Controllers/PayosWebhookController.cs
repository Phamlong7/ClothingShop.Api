using System.Security.Cryptography;
using System.Text;
using ClothingShop.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/payos/webhook")]
public class PayosWebhookController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    // PayOS sends webhook with payload and signature header. Verify and mark order paid.
    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-Payos-Signature"].ToString();
        var secret = configuration["PayOS:WebhookSecret"];

        if (!string.IsNullOrWhiteSpace(secret) && !string.IsNullOrWhiteSpace(signature))
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
            if (!computed.Equals(signature, StringComparison.OrdinalIgnoreCase))
                return Unauthorized();
        }

        // Minimal parse to get order code and status
        var doc = System.Text.Json.JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data)) return BadRequest();
        var orderCode = data.GetProperty("orderCode").GetString();
        var status = data.GetProperty("status").GetString();
        if (string.IsNullOrWhiteSpace(orderCode)) return BadRequest();

        if (string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(orderCode, out var id))
                return BadRequest();
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order is not null)
            {
                order.Status = "paid";
                await db.SaveChangesAsync();
            }
        }

        return Ok(new { ok = true });
    }
}


