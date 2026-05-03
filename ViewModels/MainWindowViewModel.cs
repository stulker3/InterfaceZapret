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

namespace ZapretUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string GameFilterTCP = "1024-65535";
    private const string GameFilterUDP = "1024-65535";

    public ObservableCollection<string> BatFiles { get; set; } = new ObservableCollection<string>();
    public string? SelectedBatFile { get; set; }


    private string _statusText = "Статус: Остановлен";
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    private bool _isZapretActive;
    public bool IsZapretActive
    {
        get => _isZapretActive;
        set => this.RaiseAndSetIfChanged(ref _isZapretActive, value);
    }





    public MainWindowViewModel()
    {
        UpdateStatus();
        ScanBatFiles();
        this.WhenAnyValue(x => x.IsZapretActive)
    .Subscribe(active =>
    {
        // Вызываем метод смены иконки в App
        (App.Current as App)?.UpdateTrayIcon(active);
    });
    }



    private string _testStatus = "Готов к тесту";
    public string TestStatus
    {
        get => _testStatus;
        set => this.RaiseAndSetIfChanged(ref _testStatus, value);
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
                    string strategy = e.Data.Replace("RESULT_STRATEGY_FOUND:", "").Trim();

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        TestStatus = "Лучшая стратегия на сегодня " + strategy;
                    });
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
}
