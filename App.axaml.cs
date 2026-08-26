using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using ZapretUI.ViewModels;
using ZapretUI.Views;
using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform;
using static ZapretUI.ViewModels.MainWindowViewModel;

namespace ZapretUI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
        UpdateButtonTick();
    }
    private void TrayIcon_OnClicked(object? sender, EventArgs e) => ShowWindow();

    // Клик "Развернуть" в меню
    private void TrayIcon_Open_Clicked(object? sender, EventArgs e) => ShowWindow();

    // Клик "Выход" (Полное закрытие)
    private void TrayIcon_Exit_Clicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Чтобы winws тоже закрылся при выходе, можно вызвать StopZapret
            (desktop.MainWindow.DataContext as ViewModels.MainWindowViewModel)?.StopZapret();
            desktop.Shutdown();
        }
    }

    private void ShowWindow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow.Show();
            desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
            desktop.MainWindow.Activate();
        }
    }
    public void UpdateTrayIcon(bool isActive)
    {
        // Ищем нашу иконку по имени, которое дали в XAML
        var trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault(); // Или через поиск в ресурсах

        if (trayIcon != null)
        {
            // Пути к вашим иконкам
            string iconUri = isActive
                ? "avares://ZapretUI/Assets/avalonia-logo-on.ico"
                : "avares://ZapretUI/Assets/avalonia-logo-off.ico";

            try
            {
                // Загружаем и меняем иконку и текст подсказки
                trayIcon.Icon = new WindowIcon(AssetLoader.Open(new Uri(iconUri)));
                trayIcon.ToolTipText = isActive ? "ZapretUI: Активен" : "ZapretUI: Остановлен";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Не удалось загрузить иконку трея: {ex.Message}");
            }
        }
    }
    private void UpdateButtonTick()
    {
        try
        {
            // Получаем иконку трея
            var trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();

            // Находим нужный NativeMenuItem по его тексту на экране
            var autostartItem = trayIcon?.Menu?.Items
                .OfType<NativeMenuItem>()
                .FirstOrDefault(item => item.Header?.ToString() == "Автозапуск");

            if (autostartItem != null)
            {
                // Запрашиваем реальный статус из Windows и ставим/убираем галочку
                autostartItem.IsChecked = IsAutostartEnabled();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка обновления галочки: {ex.Message}");
        }
    }
    private void Autostart_OnClick(object? sender, EventArgs e)
    {
        if (sender is NativeMenuItem menuItem)
        {
            // 1. Проверяем текущий статус из системы
            bool currentlyEnabled = IsAutostartEnabled();

            // 2. Меняем статус на противоположный
            if (currentlyEnabled)
            {
                DisableAutostart();
            }
            else
            {
                EnableAutostart();
            }

            // 3. Запрашиваем систему еще раз и ставим галочку на основе РЕАЛЬНОГО состояния в Windows
            UpdateButtonTick();
        }
    }

}