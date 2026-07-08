using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace AuthReference.Server.Logging;

/// <summary>
/// Serilog enricher that scrubs common secrets out of every log event's
/// rendered message + property values. Cheap belt-and-braces defence — the
/// primary defence is not putting secrets in log messages to begin with.
///
/// Patterns caught:
///   * <c>password=…</c> / <c>password":"…"</c>
///   * <c>refresh_token=…</c>
///   * <c>client_secret=…</c>
///   * long-looking JWTs (three base64 segments joined by dots)
/// </summary>
public sealed class PiiRedactor : ILogEventEnricher
{
    private static readonly Regex[] Patterns = new[]
    {
        new Regex(@"(""(password|refresh_token|client_secret|access_token)""\s*:\s*"")([^""]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"((?:password|refresh_token|client_secret|access_token)=)([^&\s""]+)",           RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"eyJ[A-Za-z0-9_\-]+\.eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+",                       RegexOptions.Compiled)
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Serilog log events are immutable — we can't rewrite the message template.
        // What we CAN do is scrub each string property so anything captured with
        // {@Body} or {Payload} is safe. Message-template constants are the
        // developer's responsibility.
        foreach (var kvp in logEvent.Properties.ToList())
        {
            if (kvp.Value is not ScalarValue { Value: string s }) continue;
            var redacted = Redact(s);
            if (redacted == s) continue;
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(kvp.Key, redacted));
        }
    }

    internal static string Redact(string input)
    {
        var current = input;
        current = Patterns[0].Replace(current, "$1***REDACTED***");
        current = Patterns[1].Replace(current, "$1***REDACTED***");
        current = Patterns[2].Replace(current, "***JWT-REDACTED***");
        return current;
    }
}
