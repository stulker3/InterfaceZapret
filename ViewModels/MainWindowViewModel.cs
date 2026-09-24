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

namespace ZapretUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private string _gameFilterTCP = "12";
    public string GameFilterTCP
    {
        get => _gameFilterTCP;
        set => this.RaiseAndSetIfChanged(ref _gameFilterTCP, value);
    }
    private string _gameFilterUDP = "12";
    public string GameFilterUDP
    {
        get => _gameFilterUDP;
        set => this.RaiseAndSetIfChanged(ref _gameFilterUDP, value);
    }
    private readonly string _settingsDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZapretUI");
    private string SelectedBatSettingsPath =>
        Path.Combine(_settingsDirectory, "selected_bat.txt");
    private const string TaskName = "InterfaceZapret";
    private string _testStatus = "Готов к тесту";
    public ObservableCollection<string> BatFiles { get; set; } = new ObservableCollection<string>();
    private string? _selectedBatFile;
    public string? SelectedBatFile
    {
        get => _selectedBatFile;
        set => this.RaiseAndSetIfChanged(ref _selectedBatFile, value);
    }
    private bool _isGameMode = true;
    public bool IsGameMode
    {
        get => _isGameMode;
        set => this.RaiseAndSetIfChanged(ref _isGameMode, value);
    }
    private string _statusText = "Статус: Остановлен";
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    private bool _isGameModeActive;
    public bool IsGameModeActive
    {
        get => _isGameModeActive;
        set => this.RaiseAndSetIfChanged(ref _isGameModeActive, value);
    }
    private bool _isZapretActive;
    public bool IsZapretActive
    {
        get => _isZapretActive;
        set => this.RaiseAndSetIfChanged(ref _isZapretActive, value);
    }

    private readonly string _filePathZapret = Path.Combine(AppContext.BaseDirectory, "zapret\\lists\\list-general.txt");
    private readonly string _filePathExclude = Path.Combine(AppContext.BaseDirectory, "zapret\\lists\\list-exclude.txt");
    private string _zapretText = string.Empty;
    public string ZapretText
    {
        get => _zapretText;
        set => this.RaiseAndSetIfChanged(ref _zapretText, value);
    }
    private string _excludeText = string.Empty;
    public string ExcludeText
    {
        get => _excludeText;
        set => this.RaiseAndSetIfChanged(ref _excludeText, value);
    }
    public string TestStatus
    {
        get => _testStatus;
        set => this.RaiseAndSetIfChanged(ref _testStatus, value);
    }
    public MainWindowViewModel()
    {
        UpdateStatus();
        ScanBatFiles();
        LoadSelectedBatFile();
        SetTextZapret();
        SetTextExclude();
        InitializeTelegramProxy();
        this.WhenAnyValue(x => x.SelectedBatFile)
        .Where(file => !string.IsNullOrWhiteSpace(file))
        .DistinctUntilChanged()
        .Subscribe(_ => SaveSelectedBatFile());
        this.WhenAnyValue(x => x.IsZapretActive)
    .Subscribe(active =>
    {
        // Вызываем метод смены иконки в App
        (App.Current as App)?.UpdateTrayIcon(active);
    });
        this.WhenAnyValue(x => x.IsGameModeActive)
                       .Subscribe(isActive =>
                       {
                           GameFilterUDP = isActive ? "1024-65535" : "12";
                           GameFilterTCP = isActive ? "1024-65535" : "12";
                       });
        ApplyZapret();
    }

    private void InitializeTelegramProxy()
    {
        _telegramProxyOptions.Secret =
            LoadOrCreateTelegramSecret();

        StartTelegramProxy();
    }
    private void SaveSelectedBatFile()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedBatFile))
                return;

            Directory.CreateDirectory(_settingsDirectory);

            File.WriteAllText(
                SelectedBatSettingsPath,
                SelectedBatFile);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Ошибка сохранения выбранного обхода: {ex.Message}");
        }
    }
    private void LoadSelectedBatFile()
    {
        try
        {
            if (!File.Exists(SelectedBatSettingsPath))
                return;

            string savedBatFile =
                File.ReadAllText(SelectedBatSettingsPath).Trim();

            var existingBatFile = BatFiles.FirstOrDefault(file =>
                string.Equals(
                    file,
                    savedBatFile,
                    StringComparison.OrdinalIgnoreCase));

            if (existingBatFile != null)
            {
                SelectedBatFile = existingBatFile;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Ошибка загрузки выбранного обхода: {ex.Message}");
        }
    }
    public void SetTextZapret()
    {
        if (File.Exists(_filePathZapret))
        {
            try
            {
                _zapretText = File.ReadAllText(_filePathZapret);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка чтения файла: {ex.Message}");
            }
        }
        this.WhenAnyValue(x => x.ZapretText)
   .ObserveOn(RxApp.MainThreadScheduler)
   .Subscribe(text => SaveToFile(text, _filePathZapret));
    }
    public void SetTextExclude()
    {
        if (File.Exists(_filePathExclude))
        {
            try
            {
                _excludeText = File.ReadAllText(_filePathExclude);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка чтения файла: {ex.Message}");
            }
        }
        this.WhenAnyValue(x => x.ExcludeText)
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(text => SaveToFile(text, _filePathExclude));
    }
    private void SaveToFile(string text, string filePath)
    {
        try
        {
            File.WriteAllText(filePath, text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка записи в файл: {ex.Message}");
        }
    }

    public void ScanBatFiles()
    {
        try
        {
            BatFiles.Clear();
            // Ищем батники в корневой папке приложения
            string baseDir = Path.Combine(AppContext.BaseDirectory, "zapret\\");
            var files = Directory.GetFiles(baseDir, "*.bat")
                                 .Select(Path.GetFileName)
                                 .Where(f => f != null)
                                 .ToList();

            foreach (var file in files)
            {
                // Исключаем служебный service.bat, если нужно
                if (file!.ToLower() != "service.bat")
                {
                    BatFiles.Add(file);
                }
            }

            // Выбираем первый файл по умолчанию
            if (BatFiles.Count > 0)
                SelectedBatFile = BatFiles[0];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка сканирования: {ex.Message}");
        }
    }

    public static void EnableAutostart()
    {
        // Получаем путь к текущему запущенному .exe файлу
        string? exePath = Process.GetCurrentProcess().MainModule?.FileName;

        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return;

        // Формируем аргументы для schtasks
        // /Create - создать задачу
        // /TN - имя задачи
        // /TR - путь к файлу (в кавычках)
        // /SC ONLOGON - запускать при входе пользователя
        // /RL HIGHEST - запуск с правами администратора
        // /F - принудительно перезаписать, если задача уже есть
        string arguments = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /RL HIGHEST /F";

        RunSchtasks(arguments);
    }

    public static void DisableAutostart()
    {
        // /Delete - удалить задачу
        // /TN - имя задачи
        // /F - удалить без подтверждения
        string arguments = $"/Delete /TN \"{TaskName}\" /F";

        RunSchtasks(arguments);
    }

    private static void RunSchtasks(string arguments)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();

                // Для отладки можно проверить process.ExitCode (0 - успех)
                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    Console.WriteLine($"Ошибка schtasks: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не удалось изменить автозагрузку: {ex.Message}");
        }
    }

    public static bool IsAutostartEnabled()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "schtasks",
                // /Query — запросить информацию, /TN — имя нашей задачи
                Arguments = $"/Query /TN \"{TaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();

                // Если код возврата равен 0, значит Windows нашла задачу с таким именем.
                // Если код возврата 1 (или другой), значит задачи в системе нет.
                return process.ExitCode == 0;
            }
        }
        catch
        {
            // Если что-то пошло не так (например, доступ заблокирован), считаем, что автозапуска нет
            return false;
        }
    }

    public async Task ApplyZapret()
    {
        if (IsZapretActive)
        {
            StopZapret();
            await StartZapret();
            UpdateStatus();
        }
        else
        {
            StopZapret();
            UpdateStatus();
        }
    }
}
