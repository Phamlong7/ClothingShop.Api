using System.Security.Cryptography;
using System.Text;

namespace ClothingShop.Api.Utils;

public static class CryptoHelper
{
    public static string HmacSHA512(string key, string input)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }
}
