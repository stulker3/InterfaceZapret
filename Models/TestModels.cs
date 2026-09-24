namespace ZapretUI.Models;

public class TestTarget
{
    public string Name { get; set; } = string.Empty;

    public string? Url { get; set; }

    public string? PingTarget { get; set; }
}

public class TargetTestResult
{
    public int HttpOk { get; set; }

    public int HttpError { get; set; }

    public int Unsupported { get; set; }

    public bool PingOk { get; set; }
}

public class ConfigTestResult
{
    public string Config { get; set; } = string.Empty;

    public int HttpOk { get; set; }

    public int HttpError { get; set; }

    public int Unsupported { get; set; }

    public int PingOk { get; set; }

    public int PingFail { get; set; }
}

public enum HttpTestType
{
    Http11,
    Tls12,
    Tls13
}

public enum HttpTestResult
{
    Ok,
    Error,
    Unsupported
}