namespace NetLab.Api.Configuration;

public class NetworkOptions
{
    public const string SectionName = "Network";

    /// <summary>Cho phép kết nối tới địa chỉ nội bộ (loopback, 10.x, 192.168.x...). Mặc định TẮT để chống SSRF.</summary>
    public bool AllowPrivateAddresses { get; set; }
    public int MaxPingCount { get; set; } = 10;
    public int MaxTimeoutMs { get; set; } = 10000;
}

public class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Nếu để trống, API không yêu cầu key (chỉ nên dùng khi phát triển).</summary>
    public string ApiKey { get; set; } = string.Empty;
}
