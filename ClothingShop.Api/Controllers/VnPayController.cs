using System.Security.Cryptography;
using System.Text;
using ClothingShop.Api.Data;
using ClothingShop.Api.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/vnpay")] 
public class VnPayController(AppDbContext db, IConfiguration configuration, ILogger<VnPayController> logger) : ControllerBase
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

        // Per VNPAY spec: sort by ASCII (ordinal), URL-encode values, sign the encoded query (exclude vnp_SecureHash/_Type)
        var vnp_Params = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_Amount"] = ((long)Math.Round(order.TotalAmount * 100)).ToString(),
            ["vnp_CreateDate"] = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")).ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = (HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()) ?? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = $"Order {order.Id}",
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_TxnRef"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
        };

        // Build raw string to sign (not URL-encoded) and encoded query to send (both in same key order)
        var rawPairs = vnp_Params
            .Where(kvp => kvp.Key != "vnp_SecureHash" && kvp.Key != "vnp_SecureHashType")
            .Select(kvp => $"{kvp.Key}={kvp.Value}")
            .ToArray();
        var signDataRaw = string.Join('&', rawPairs);

        var encodedPairs = vnp_Params
            .Where(kvp => kvp.Key != "vnp_SecureHash" && kvp.Key != "vnp_SecureHashType")
            .Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}")
            .ToArray();
        var query = string.Join('&', encodedPairs);

        var secureHash = CryptoHelper.HmacSHA512(hashSecret, signDataRaw);
        var payUrl = $"{baseUrl}?{query}&vnp_SecureHash={secureHash}";

        // TEMP DEBUG LOGS: compare signDataRaw (raw) and query (encoded)
        logger.LogInformation("VNPAY signDataRaw: {signDataRaw}", signDataRaw);
        logger.LogInformation("VNPAY queryEncoded: {query}", query);
        logger.LogInformation("VNPAY payUrl: {payUrl}", payUrl);

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
        var calcHash = CryptoHelper.HmacSHA512(hashSecret, signData);
        try
        {
            var calcBytes = Convert.FromHexString(calcHash);
            var recvBytes = Convert.FromHexString(receivedHash);
            if (calcBytes.Length != recvBytes.Length || !CryptographicOperations.FixedTimeEquals(calcBytes, recvBytes))
                return BadRequest(new { message = "Invalid signature" });
        }
        catch
        {
            return BadRequest(new { message = "Invalid signature" });
        }

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

}


