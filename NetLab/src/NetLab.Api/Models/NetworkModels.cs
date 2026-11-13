namespace NetLab.Api.Models;

public record DnsResult(string Host, IReadOnlyList<string> Addresses, double ElapsedMs);

public record PingEntry(int Sequence, string Status, long? RoundtripMs, int? Ttl);

public record PingResult(
    string Host, string Address, int Sent, int Received, double LossPercent,
    long? MinMs, double? AvgMs, long? MaxMs, IReadOnlyList<PingEntry> Replies);

public record TcpCheckResult(string Host, string Address, int Port, string Status, double ElapsedMs, string? Error);

public record HttpCheckResult(
    string Url, int? StatusCode, string? ReasonPhrase, string? HttpVersion, double ElapsedMs,
    string? Server, string? ContentType, long? ContentLength, string? RedirectLocation, string? Error);

public record TlsInfo(
    string Host, int Port, string Protocol, string Subject, string Issuer,
    DateTime NotBeforeUtc, DateTime NotAfterUtc, int DaysRemaining,
    string? SubjectAlternativeNames, string PolicyErrors);

public record HolidayDto(DateOnly Date, string Name, string LocalName);
