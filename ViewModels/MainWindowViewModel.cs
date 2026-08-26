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

    private readonly string _filePathZapret = Path.Combine(AppContext.BaseDirectory, "zapret\\lists\\list-general-user.txt");
    private readonly string _filePathExclude = Path.Combine(AppContext.BaseDirectory, "zapret\\lists\\list-exclude-user.txt");
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
        SetTextZapret();
        SetTextExclude();
        this.WhenAnyValue(x => x.IsZapretActive)
    .Subscribe(active =>
    {
        // Вызываем метод смены иконки в App
        (App.Current as App)?.UpdateTrayIcon(active);
    });
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

    public async Task RunAutoTestAsync()
    {
        string scriptPath = Path.Combine(AppContext.BaseDirectory, "zapret", "utils", "test zapret.ps1");

        if (!File.Exists(scriptPath))
        {
            TestStatus = "Ошибка: Скрипт теста не найден!";
            return;
        }

        TestStatus = "Тестирование запущено. Подождите...";

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        try
        {
            using var process = new Process();
            process.StartInfo = psi;

            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                // Ловим финальный результат
                if (e.Data.Contains("RESULT_STRATEGY_FOUND:"))
                {
                    string _strategy = e.Data.Replace("RESULT_STRATEGY_FOUND:", "").Trim();

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        TestStatus = "Лучшая стратегия на сегодня " + _strategy;
                    });
                    foreach (var strategy in BatFiles)
                    {
                        if (_strategy == strategy)
                        {
                            SelectedBatFile = strategy;
                        }
                    }
                }
                else if (e.Data.Contains("Config:")) // Для живого лога
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        TestStatus = $"Проверка: {e.Data.Trim()}";
                    });
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            TestStatus = $"Ошибка: {ex.Message}";
        }
    }

    public void UpdateStatus()
    {
        // Ищем процесс по имени
        bool running = Process.GetProcessesByName("winws").Any();

        IsZapretActive = running;
        StatusText = running ? "Статус: Активен" : "Статус: Остановлен";
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

    public async Task StartZapret()
    {
        if (string.IsNullOrEmpty(SelectedBatFile)) return;

        try
        {
            string baseDir = Path.Combine(AppContext.BaseDirectory, "zapret\\");
            string binDir = Path.Combine(baseDir, "bin");
            string exePath = Path.Combine(binDir, "winws.exe");

            if (!File.Exists(exePath)) return;

            // Теперь достаем аргументы из ВЫБРАННОГО в комбобоксе файла
            string args = GetArgsFromBat(SelectedBatFile);

            if (string.IsNullOrEmpty(args)) return;

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                WorkingDirectory = binDir,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };

            await Task.Run(() => Process.Start(psi));
            UpdateStatus();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка запуска: {ex.Message}");
        }
    }

    private string GetArgsFromBat(string fileName)
    {
        string batPath = Path.Combine(AppContext.BaseDirectory, "zapret\\", fileName);
        //if (!File.Exists(batPath)) return string.Empty;

        string binDir = Path.Combine(AppContext.BaseDirectory, "zapret\\bin");
        string listsDir = Path.Combine(AppContext.BaseDirectory, "zapret\\lists");

        string[] lines = File.ReadAllLines(batPath);
        StringBuilder argsBuilder = new StringBuilder();
        bool startFound = false;

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();

            // Ищем строку, которая начинается с запуска winws.exe
            if (trimmedLine.Contains("winws.exe"))
            {
                startFound = true;
                int index = trimmedLine.IndexOf("winws.exe");

                trimmedLine = trimmedLine.Substring(index + 11).Trim();
            }

            if (startFound)
            {
                // Убираем символ переноса строки батника ^
                string cleanLine = trimmedLine.Replace("^", "").Trim();

                // Заменяем переменные батника на наши реальные пути
                cleanLine = cleanLine.Replace("%BIN%", binDir + "\\");
                cleanLine = cleanLine.Replace("%LISTS%", listsDir + "\\");
                cleanLine = cleanLine.Replace("%GameFilterTCP%", GameFilterTCP);
                cleanLine = cleanLine.Replace("%GameFilterUDP%", GameFilterUDP);
                // Убираем кавычки вокруг путей, которые мы и так подставляем (необязательно, но полезно для чистоты)
                cleanLine = cleanLine.Replace("\"%BIN%\"", $"\"{binDir}\"");
                cleanLine = cleanLine.Replace("\"%LISTS%\"", $"\"{listsDir}\"");

                argsBuilder.Append(cleanLine + " ");

                // Если строка не заканчивалась на ^, значит это конец команды в батнике
                if (!trimmedLine.EndsWith("^")) break;
            }
        }

        return argsBuilder.ToString().Trim();
    }

    public void StopZapret()
    {
        foreach (var p in Process.GetProcessesByName("winws"))
        {
            try { p.Kill(); } catch { }
        }
        UpdateStatus();
    }

    public async Task GameModeChange()
    {
        this.WhenAnyValue(x => x.IsGameModeActive)
                   .Subscribe(isActive =>
                   {
                       GameFilterUDP = isActive ? "1024-65535" : "12";
                       GameFilterTCP = isActive ? "1024-65535" : "12";
                   });

        await ApplyZapret();
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
