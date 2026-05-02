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
using System.Reactive; // Нужен для ReactiveCommand
using ReactiveUI;

namespace ZapretUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string GameFilterTCP = "12";
    private const string GameFilterUDP = "12";

    // Список батников для комбобокса
    public ObservableCollection<string> BatFiles { get; set; } = new ObservableCollection<string>();
    // Выбранный файл
    public string? SelectedBatFile { get; set; }
    // Явное объявление события
    public MainWindowViewModel()
    {
        ScanBatFiles();
    }

    // Метод сканирования папки
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
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = true
            };

            await Task.Run(() => Process.Start(psi));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка запуска: {ex.Message}");
        }
    }
    private string _currentArgs = "Выберите файл";
    public string CurrentArgs
    {
        get => _currentArgs;
        set => this.RaiseAndSetIfChanged(ref _currentArgs, value);
    }
    public void ShowArguments()
    {
        if (string.IsNullOrEmpty(SelectedBatFile)) return;

        string batPath = Path.Combine(Path.Combine(AppContext.BaseDirectory, "zapret\\"), SelectedBatFile);
        CurrentArgs = GetArgsFromBat(batPath);
    }
    // Метод GetArgsFromBat остается прежним из предыдущего ответа...
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
                // Отрезаем всё, что до winws.exe (включая сам экзешник)
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
    }
}
