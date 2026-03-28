using System.Text;
using System.Text.Json;

namespace Ciderfy.Tests.Fakers;

internal static class JwtFaker
{
    public static string Make(object header, object payload)
    {
        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);
        return $"{B64Url(headerJson)}.{B64Url(payloadJson)}.fakesig";
    }

    public static long FutureExp => DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
    public static long PastExp => DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
    public static long SoonExp => DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeSeconds();

    private static string B64Url(string json) =>
        Convert
            .ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
