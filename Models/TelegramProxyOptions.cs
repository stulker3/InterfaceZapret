using System.Collections.Generic;

namespace ZapretUI.Models;

public class TelegramProxyOptions
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 1443;

    public string Secret { get; set; } = string.Empty;

    public List<string> DcIps { get; set; } = new()
    {
        "2:149.154.167.220",
        "4:149.154.167.220"
    };
}