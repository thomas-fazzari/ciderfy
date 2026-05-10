using System.ComponentModel.DataAnnotations;

namespace Ciderfy.Configuration;

/// <summary>
/// Validates that an option value is an absolute HTTP or HTTPS URL.
/// </summary>
/// <remarks>
/// Empty values are allowed here so <see cref="RequiredAttribute" /> remains responsible for
/// required-value failures.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
internal sealed class HttpUrlAttribute : ValidationAttribute
{
    /// <summary>
    /// Returns true when the value is null, empty, or an absolute HTTP/HTTPS URL.
    /// </summary>
    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        if (value is not string text)
            return false;

        if (text.Length == 0)
            return true;

        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public override string FormatErrorMessage(string name) =>
        $"{name} must be an absolute HTTP or HTTPS URL.";
}

/// <summary>
/// Validates that an option value ends with a slash.
/// </summary>
/// <remarks>
/// Empty values are allowed here so <see cref="RequiredAttribute" /> remains responsible for
/// required-value failures.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
internal sealed class EndsWithSlashAttribute : ValidationAttribute
{
    /// <summary>
    /// Returns true when the value is null, empty, or a string ending with '/'.
    /// </summary>
    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        if (value is not string text)
            return false;

        return text.Length == 0 || text.EndsWith('/');
    }

    public override string FormatErrorMessage(string name) => $"{name} must end with '/'.";
}

/// <summary>
/// Validates that an option value is a legal HTTP header field name.
/// </summary>
/// <remarks>
/// Header names must use RFC token characters only. Empty values are allowed here so
/// <see cref="RequiredAttribute" /> remains responsible for required-value failures.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
internal sealed class HeaderNameAttribute : ValidationAttribute
{
    /// <summary>
    /// Returns true when the value is null, empty, or contains only HTTP token characters.
    /// </summary>
    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        return value is string text && (text.Length == 0 || text.All(IsTokenCharacter));
    }

    public override string FormatErrorMessage(string name) =>
        $"{name} must be a valid HTTP header name.";

    private static bool IsTokenCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character)
        || character
            is '!'
                or '#'
                or '$'
                or '%'
                or '&'
                or '\''
                or '*'
                or '+'
                or '-'
                or '.'
                or '^'
                or '_'
                or '`'
                or '|'
                or '~';
}
