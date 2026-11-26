using System.Text.RegularExpressions;

namespace Dograc.Infrastructure.Diagnostics;

/// <summary>
/// Masks secrets, API keys, auth tokens, passwords, and sensitive document/prompt content
/// before writing to log files or exporting diagnostic reports.
/// </summary>
public static partial class SensitiveDataSanitizer
{
    [GeneratedRegex(@"(Bearer\s+)[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"\b(sk-[A-Za-z0-9]{12,}|ghp_[A-Za-z0-9]{16,}|gho_[A-Za-z0-9]{16,}|github_pat_[A-Za-z0-9_]{16,})\b", RegexOptions.Compiled)]
    private static partial Regex TokenPrefixRegex();

    [GeneratedRegex(@"(?i)(api[_-]?key|password|passwd|secret|access[_-]?token|refresh[_-]?token)\s*([:=])\s*([^\s,;""'\r\n]+)", RegexOptions.Compiled)]
    private static partial Regex KeyValueSecretRegex();

    [GeneratedRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    public static string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var result = input;
        result = BearerRegex().Replace(result, "$1[REDACTED]");
        result = TokenPrefixRegex().Replace(result, "[REDACTED_TOKEN]");
        result = KeyValueSecretRegex().Replace(result, "$1$2[REDACTED]");
        result = EmailRegex().Replace(result, "[REDACTED_EMAIL]");

        // Strip inline RAG context/prompt blocks if accidentally captured in an error string.
        var contextMarker = result.IndexOf("[문서 문맥]", StringComparison.Ordinal);
        if (contextMarker >= 0)
        {
            result = result[..contextMarker] + "[문서 문맥 생략됨 - 민감정보 보호]";
        }

        return result;
    }
}
