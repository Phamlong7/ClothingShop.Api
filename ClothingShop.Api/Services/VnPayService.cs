using System.Text;
using System.Net;
using ClothingShop.Api.Models;
using ClothingShop.Api.Utils;
using Microsoft.Extensions.Logging;

namespace ClothingShop.Api.Services;

public class VnPayService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<VnPayService> _logger;

    public VnPayService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<VnPayService> logger)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public (string PaymentUrl, string RawSignData) CreatePaymentUrl(Order order)
    {
        var cfg = _configuration.GetSection("VnPay");
        var tmnCode = (cfg["TmnCode"] ?? string.Empty).Trim();
        var hashSecret = (cfg["HashSecret"] ?? string.Empty).Trim();
        var baseUrl = (cfg["BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html").Trim();
        var returnUrl = (cfg["ReturnUrl"] ?? string.Empty).Trim();

        var httpContext = _httpContextAccessor.HttpContext!;

        // Vietnam time (UTC+7) in a portable way
        var createDate = DateTime.UtcNow.AddHours(7);

        var vnp_Params = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_Amount"] = ((long)Math.Round(order.TotalAmount * 100)).ToString(),
            ["vnp_CreateDate"] = createDate.ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = (httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim())
                               ?? httpContext.Connection.RemoteIpAddress?.ToString()
                               ?? "127.0.0.1",
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = $"Thanh toan don hang {order.Id}",
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_TxnRef"] = order.Id.ToString("N")
        };

        // Encode per application/x-www-form-urlencoded (space as '+')
        static string EncodeForm(string value)
        {
            var enc = WebUtility.UrlEncode(value) ?? string.Empty;
            return enc.Replace("%20", "+");
        }

        // Build encoded query in sorted order and sign EXACTLY this encoded string
        var encodedPairs = vnp_Params.Select(kv => $"{kv.Key}={EncodeForm(kv.Value)}");
        var signData = string.Join('&', encodedPairs);

        var secureHash = Convert.ToHexString(
            new System.Security.Cryptography.HMACSHA512(Encoding.UTF8.GetBytes(hashSecret))
                .ComputeHash(Encoding.UTF8.GetBytes(signData))
        );

        var payUrl = $"{baseUrl}?{signData}&vnp_SecureHashType=HmacSHA512&vnp_SecureHash={secureHash}";

        _logger.LogInformation("VNPAY Encoded Sign Data: {SignData}", signData);
        _logger.LogInformation("VNPAY Payment URL: {PaymentUrl}", payUrl);

        return (payUrl, signData);
    }
}


