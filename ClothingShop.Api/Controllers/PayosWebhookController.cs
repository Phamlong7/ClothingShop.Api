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
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return Ok(new { ok = true });

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = "paid";
            }
            else if (string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(status, "EXPIRED", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = "failed";
            }
        }
        await db.SaveChangesAsync();

        return Ok(new { ok = true });
    }
}


