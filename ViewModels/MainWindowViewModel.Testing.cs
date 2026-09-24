using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using System.Reactive;
using ReactiveUI;
using Avalonia.Interactivity;
using System.Reactive.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Authentication;
using System.Threading;
using ZapretUI.Models;

namespace ZapretUI.ViewModels;

public partial class MainWindowViewModel
{
    private bool _isTestRunning;

    public async Task RunAutoTestAsync()
    {
        if (_isTestRunning)
            return;

        if (BatFiles.Count == 0)
        {
            TestStatus = "Ошибка: стратегии не найдены";
            return;
        }

        _isTestRunning = true;

        // Запоминаем состояние до теста
        string? originalConfig = SelectedBatFile;
        bool wasRunning = Process.GetProcessesByName("winws").Any();

        try
        {
            TestStatus = "Подготовка к тестированию...";

            var targets = LoadTestTargets();
            var configResults = new List<ConfigTestResult>();

            var configs = BatFiles.ToList();

            for (int i = 0; i < configs.Count; i++)
            {
                string config = configs[i];

                TestStatus =
                    $"Проверка {i + 1}/{configs.Count}: {config}";

                // Останавливаем только winws.
                // StopZapret() здесь использовать не надо,
                // потому что он ещё удаляет службы.
                StopWinwsOnly();

                bool started = StartZapretForTest(config);

                if (!started)
                {
                    configResults.Add(new ConfigTestResult
                    {
                        Config = config,
                        HttpError = 999
                    });

                    continue;
                }

                // Как в PowerShell — ждём запуска стратегии
                await Task.Delay(5000);

                ConfigTestResult result =
                    await TestConfigAsync(config, targets);

                configResults.Add(result);

                Debug.WriteLine(
                    $"{config}: HTTP OK={result.HttpOk}, " +
                    $"ERR={result.HttpError}, " +
                    $"UNSUP={result.Unsupported}, " +
                    $"PING OK={result.PingOk}, " +
                    $"PING FAIL={result.PingFail}");

                StopWinwsOnly();
            }

            // Та же логика, что была в PowerShell:
            // 1. максимум HTTP OK
            // 2. если одинаково — максимум Ping OK
            ConfigTestResult? best = configResults
                .OrderByDescending(x => x.HttpOk)
                .ThenByDescending(x => x.PingOk)
                .FirstOrDefault();

            if (best == null)
            {
                TestStatus = "Не удалось определить стратегию";
                return;
            }

            // Выбираем лучшую стратегию в ComboBox
            SelectedBatFile = best.Config;

            TestStatus =
                $"Лучшая стратегия: {best.Config} ";
        }
        catch (Exception ex)
        {
            TestStatus = $"Ошибка тестирования: {ex.Message}";
            Debug.WriteLine(ex);
        }
        finally
        {
            StopWinwsOnly();

            // Возвращаем состояние zapret, которое было ДО теста.
            //
            // При этом SelectedBatFile остаётся лучшей найденной
            // стратегией в ComboBox.
            if (wasRunning && !string.IsNullOrEmpty(originalConfig))
            {
                StartZapretForTest(originalConfig);
            }

            UpdateStatus();

            _isTestRunning = false;
        }
    }
    private List<TestTarget> LoadTestTargets()
    {
        string targetsPath = Path.Combine(
            AppContext.BaseDirectory,
            "zapret",
            "utils",
            "targets.txt");

        var targets = new List<TestTarget>();

        if (File.Exists(targetsPath))
        {
            try
            {
                foreach (string line in File.ReadAllLines(targetsPath))
                {
                    string trimmed = line.Trim();

                    if (string.IsNullOrWhiteSpace(trimmed))
                        continue;

                    if (trimmed.StartsWith("#"))
                        continue;

                    int separator = trimmed.IndexOf('=');

                    if (separator <= 0)
                        continue;

                    string name =
                        trimmed.Substring(0, separator).Trim();

                    string value =
                        trimmed.Substring(separator + 1)
                            .Trim()
                            .Trim('"');

                    if (string.IsNullOrWhiteSpace(name) ||
                        string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    targets.Add(CreateTarget(name, value));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Ошибка targets.txt: {ex.Message}");
            }
        }

        // Если targets.txt отсутствует или пустой —
        // используем значения из PowerShell
        if (targets.Count == 0)
        {
            targets.Add(CreateTarget(
                "Discord Main",
                "https://discord.com"));

            targets.Add(CreateTarget(
                "Discord Gateway",
                "https://gateway.discord.gg"));

            targets.Add(CreateTarget(
                "Discord CDN",
                "https://cdn.discordapp.com"));

            targets.Add(CreateTarget(
                "Discord Updates",
                "https://updates.discord.com"));

            targets.Add(CreateTarget(
                "YouTube Web",
                "https://www.youtube.com"));

            targets.Add(CreateTarget(
                "YouTube Short",
                "https://youtu.be"));

            targets.Add(CreateTarget(
                "YouTube Image",
                "https://i.ytimg.com"));

            targets.Add(CreateTarget(
                "YouTube Video Redirect",
                "https://redirector.googlevideo.com"));

            targets.Add(CreateTarget(
                "Google Main",
                "https://www.google.com"));

            targets.Add(CreateTarget(
                "Google Gstatic",
                "https://www.gstatic.com"));

            targets.Add(CreateTarget(
                "Cloudflare Web",
                "https://www.cloudflare.com"));

            targets.Add(CreateTarget(
                "Cloudflare CDN",
                "https://cdnjs.cloudflare.com"));

            targets.Add(CreateTarget(
                "Cloudflare DNS 1.1.1.1",
                "PING:1.1.1.1"));

            targets.Add(CreateTarget(
                "Cloudflare DNS 1.0.0.1",
                "PING:1.0.0.1"));

            targets.Add(CreateTarget(
                "Google DNS 8.8.8.8",
                "PING:8.8.8.8"));

            targets.Add(CreateTarget(
                "Google DNS 8.8.4.4",
                "PING:8.8.4.4"));

            targets.Add(CreateTarget(
                "Quad9 DNS 9.9.9.9",
                "PING:9.9.9.9"));
        }

        return targets;
    }

    private TestTarget CreateTarget(
        string name,
        string value)
    {
        if (value.StartsWith(
                "PING:",
                StringComparison.OrdinalIgnoreCase))
        {
            return new TestTarget
            {
                Name = name,

                PingTarget =
                    value.Substring(5).Trim()
            };
        }

        string? host = null;

        try
        {
            host = new Uri(value).Host;
        }
        catch
        {
            // неправильный URL
        }

        return new TestTarget
        {
            Name = name,
            Url = value,
            PingTarget = host
        };
    }
    private async Task<ConfigTestResult> TestConfigAsync(
    string config,
    List<TestTarget> targets)
    {
        const int maxParallel = 8;

        using var semaphore =
            new SemaphoreSlim(maxParallel);

        var tasks = targets.Select(async target =>
        {
            await semaphore.WaitAsync();

            try
            {
                return await TestTargetAsync(target);
            }
            finally
            {
                semaphore.Release();
            }
        });

        TargetTestResult[] results =
            await Task.WhenAll(tasks);

        return new ConfigTestResult
        {
            Config = config,

            HttpOk =
                results.Sum(x => x.HttpOk),

            HttpError =
                results.Sum(x => x.HttpError),

            Unsupported =
                results.Sum(x => x.Unsupported),

            PingOk =
                results.Count(x => x.PingOk),

            PingFail =
                results.Count(x => !x.PingOk)
        };
    }
    private async Task<TargetTestResult> TestTargetAsync(
    TestTarget target)
    {
        var result = new TargetTestResult();

        if (!string.IsNullOrWhiteSpace(target.Url))
        {
            HttpTestType[] tests =
            {
            HttpTestType.Http11,
            HttpTestType.Tls12,
            HttpTestType.Tls13
        };

            foreach (HttpTestType test in tests)
            {
                HttpTestResult testResult =
                    await TestHttpAsync(target.Url, test);

                switch (testResult)
                {
                    case HttpTestResult.Ok:
                        result.HttpOk++;
                        break;

                    case HttpTestResult.Unsupported:
                        result.Unsupported++;
                        break;

                    default:
                        result.HttpError++;
                        break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(target.PingTarget))
        {
            result.PingOk =
                await TestPingAsync(target.PingTarget);
        }
        else
        {
            // Если ping для цели не предусмотрен,
            // не считаем его успешным.
            result.PingOk = false;
        }

        return result;
    }
    private async Task<HttpTestResult> TestHttpAsync(
    string url,
    HttpTestType testType)
    {
        try
        {
            using var handler =
                new SocketsHttpHandler();

            handler.ConnectTimeout =
                TimeSpan.FromSeconds(5);

            switch (testType)
            {
                case HttpTestType.Tls12:

                    handler.SslOptions.EnabledSslProtocols =
                        SslProtocols.Tls12;

                    break;

                case HttpTestType.Tls13:

                    handler.SslOptions.EnabledSslProtocols =
                        SslProtocols.Tls13;

                    break;
            }

            using var client =
                new HttpClient(handler);

            client.Timeout =
                TimeSpan.FromSeconds(5);

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Head,
                    url);

            if (testType == HttpTestType.Http11)
            {
                request.Version =
                    HttpVersion.Version11;

                request.VersionPolicy =
                    HttpVersionPolicy.RequestVersionExact;
            }

            using HttpResponseMessage response =
                await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead);

            // В PowerShell проверяется успешность curl,
            // а не HTTP-код.
            //
            // То есть даже 301 / 403 / 404 означают,
            // что соединение с сервером установлено.
            return HttpTestResult.Ok;
        }
        catch (PlatformNotSupportedException)
        {
            return HttpTestResult.Unsupported;
        }
        catch (AuthenticationException)
        {
            return HttpTestResult.Error;
        }
        catch (HttpRequestException)
        {
            return HttpTestResult.Error;
        }
        catch (TaskCanceledException)
        {
            return HttpTestResult.Error;
        }
        catch
        {
            return HttpTestResult.Error;
        }
    }
    private async Task<bool> TestPingAsync(string host)
    {
        try
        {
            const int count = 3;
            const int timeout = 2000;

            for (int i = 0; i < count; i++)
            {
                using var ping = new Ping();

                PingReply reply =
                    await ping.SendPingAsync(
                        host,
                        timeout);

                if (reply.Status != IPStatus.Success)
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}