using System;
using System.Diagnostics;
using System.IO;
using ZapretUI.Models;

namespace ZapretUI.Services;

public class TelegramProxyService
{
    private Process? _process;

    public bool IsRunning
    {
        get
        {
            return _process != null &&
                   !_process.HasExited;
        }
    }

    public bool Start(TelegramProxyOptions options)
    {
        if (IsRunning)
            return true;

        string exePath = Path.Combine(
            AppContext.BaseDirectory,
            "zapret",
            "TgWsProxy",
            "TgWsProxyCli.exe");

        if (!File.Exists(exePath))
        {
            Debug.WriteLine(
                $"TG WS Proxy не найден: {exePath}");

            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.ArgumentList.Add("--host");
            startInfo.ArgumentList.Add(options.Host);

            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add(
                options.Port.ToString());

            startInfo.ArgumentList.Add("--secret");
            startInfo.ArgumentList.Add(options.Secret);

            foreach (string dc in options.DcIps)
            {
                startInfo.ArgumentList.Add("--dc-ip");
                startInfo.ArgumentList.Add(dc);
            }

            _process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            _process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Debug.WriteLine(
                        $"[TG Proxy] {e.Data}");
                }
            };

            _process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Debug.WriteLine(
                        $"[TG Proxy] {e.Data}");
                }
            };

            _process.Start();

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Ошибка запуска TG WS Proxy: {ex.Message}");

            return false;
        }
    }

    public void Stop()
    {
        if (_process == null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(true);
                _process.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Ошибка остановки TG WS Proxy: {ex.Message}");
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    public string GetTelegramLink(
        TelegramProxyOptions options)
    {
        return
            $"tg://proxy?" +
            $"server={options.Host}" +
            $"&port={options.Port}" +
            $"&secret=dd{options.Secret}";
    }

    public bool OpenTelegram(
        TelegramProxyOptions options)
    {
        try
        {
            string link =
                GetTelegramLink(options);

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = link,
                    UseShellExecute = true
                });

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Не удалось открыть Telegram: {ex.Message}");

            return false;
        }
    }
}