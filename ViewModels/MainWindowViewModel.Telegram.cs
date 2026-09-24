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
using ZapretUI.Services;
using System.Security.Cryptography;


namespace ZapretUI.ViewModels;

public partial class MainWindowViewModel
{
    private readonly TelegramProxyService
        _telegramProxyService = new();

    private readonly TelegramProxyOptions
        _telegramProxyOptions = new();


    private readonly string _settingsDirectoryTG =
           Path.Combine(
               Environment.GetFolderPath(
                   Environment.SpecialFolder.LocalApplicationData),
               "ZapretUI");

    private string TelegramSecretPath =>
        Path.Combine(
            _settingsDirectoryTG,
            "telegram_secret.txt");
    private bool _isTelegramProxyActive;

    public bool IsTelegramProxyActive
    {
        get => _isTelegramProxyActive;

        set => this.RaiseAndSetIfChanged(
            ref _isTelegramProxyActive,
            value);
    }

    private string _telegramStatus =
        "Telegram: отключен";

    public string TelegramStatus
    {
        get => _telegramStatus;

        set => this.RaiseAndSetIfChanged(
            ref _telegramStatus,
            value);
    }

    public void StartTelegramProxy()
    {
        if (string.IsNullOrWhiteSpace(
                _telegramProxyOptions.Secret))
        {
            _telegramProxyOptions.Secret =
                LoadOrCreateTelegramSecret();
        }

        bool started =
            _telegramProxyService.Start(
                _telegramProxyOptions);

        IsTelegramProxyActive = started;

        TelegramStatus = started
            ? "Telegram: подключен"
            : "Telegram: ошибка запуска";
    }

    public void StopTelegramProxy()
    {
        _telegramProxyService.Stop();

        IsTelegramProxyActive = false;

        TelegramStatus =
            "Telegram: отключен";
    }

    public void ConnectTelegram()
    {
        if (!IsTelegramProxyActive)
        {
            StartTelegramProxy();
        }

        if (!IsTelegramProxyActive)
            return;

        _telegramProxyService.OpenTelegram(
            _telegramProxyOptions);
    }


    private string LoadOrCreateTelegramSecret()
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);

            // Если secret уже есть — используем старый
            if (File.Exists(TelegramSecretPath))
            {
                string savedSecret =
                    File.ReadAllText(TelegramSecretPath)
                        .Trim();

                if (!string.IsNullOrWhiteSpace(savedSecret))
                {
                    return savedSecret;
                }
            }

            // Secret ещё нет — создаём один раз
            string newSecret =
                Convert
                    .ToHexString(
                        RandomNumberGenerator.GetBytes(16))
                    .ToLowerInvariant();

            File.WriteAllText(
                TelegramSecretPath,
                newSecret);

            return newSecret;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Ошибка работы с Telegram secret: {ex.Message}");

            // На крайний случай
            return Convert
                .ToHexString(
                    RandomNumberGenerator.GetBytes(16))
                .ToLowerInvariant();
        }
    }
}