using System.Security.Cryptography;
using System.Text;
using ClothingShop.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/vnpay")] 
public class VnPayController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    // Create payment URL for an order (simple, unsecured demo)
    [HttpPost("create/{orderId:guid}")]
    public async Task<IActionResult> Create(Guid orderId)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return NotFound();
        if (order.Status == "paid") return BadRequest(new { message = "Order already paid" });

        var cfg = configuration.GetSection("VnPay");
        var tmnCode = cfg["TmnCode"] ?? string.Empty;
        var hashSecret = cfg["HashSecret"] ?? string.Empty;
        var baseUrl = cfg["BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var returnUrl = cfg["ReturnUrl"] ?? string.Empty;

        var vnp_Params = new SortedDictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_Amount"] = ((long)Math.Round(order.TotalAmount * 100)).ToString(),
            ["vnp_CreateDate"] = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = $"Order {order.Id}",
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_TxnRef"] = order.Id.ToString("N")
        };

        var query = string.Join('&', vnp_Params.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        var signData = string.Join('&', vnp_Params.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        var secureHash = HmacSHA512(hashSecret, signData);
        var payUrl = $"{baseUrl}?{query}&vnp_SecureHash={secureHash}";

        return Ok(new { url = payUrl });
    }

    // Return URL from VNPAY
    [HttpGet("return")]
    public async Task<IActionResult> Return()
    {
        var cfg = configuration.GetSection("VnPay");
        var hashSecret = cfg["HashSecret"] ?? string.Empty;

        var vnp_Params = Request.Query
            .Where(kv => kv.Key.StartsWith("vnp_"))
            .ToDictionary(k => k.Key, v => v.Value.ToString());

        if (!vnp_Params.TryGetValue("vnp_SecureHash", out var receivedHash))
            return BadRequest(new { message = "Missing secure hash" });

        vnp_Params.Remove("vnp_SecureHash");
        var sorted = new SortedDictionary<string, string>(vnp_Params);
        var signData = string.Join('&', sorted.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        var calcHash = HmacSHA512(hashSecret, signData);
        if (!calcHash.Equals(receivedHash, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Invalid signature" });

        var code = sorted.GetValueOrDefault("vnp_TxnRef");
        var transStatus = sorted.GetValueOrDefault("vnp_TransactionStatus");
        if (Guid.TryParseExact(code, "N", out var orderId))
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order is not null && transStatus == "00")
            {
                order.Status = "paid";
                await db.SaveChangesAsync();
            }
        }

        return Ok(new { ok = true });
    }

    private static string HmacSHA512(string key, string input)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }
}


