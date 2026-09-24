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

    public void UpdateStatus()
    {
        // Ищем процесс по имени
        bool running = Process.GetProcessesByName("winws").Any();

        IsZapretActive = running;
        StatusText = running ? "Статус: Активен" : "Статус: Остановлен";
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
    public void StopZapret()
    {
        // 1. Завершение процессов winws через встроенный метод C# (ваш вариант)
        foreach (var p in Process.GetProcessesByName("winws"))
        {
            try
            {
                p.Kill(true); // true включает завершение всего дерева процессов (аналог /F в taskkill)
            }
            catch { }
        }

        // 2. Функция-помощник для остановки и удаления служб через cmd
        Action<string> stopAndDeleteService = (serviceName) =>
        {
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = "cmd.exe";
                    // Подавляем вывод (>nul 2>&1), чтобы не спамить в консоль, как в исходном bat-файле
                    p.StartInfo.Arguments = $"/c net stop \"{serviceName}\" >nul 2>&1 & sc delete \"{serviceName}\" >nul 2>&1";
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.UseShellExecute = false;
                    p.Start();
                    p.WaitForExit();
                }
            }
            catch { }
        };

        // 3. Последовательное удаление служб
        stopAndDeleteService("zapret");
        stopAndDeleteService("WinDivert");
        stopAndDeleteService("WinDivert14");
        UpdateStatus();
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
    private bool StartZapretForTest(string config)
    {
        try
        {
            string baseDir =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "zapret");

            string binDir =
                Path.Combine(
                    baseDir,
                    "bin");

            string exePath =
                Path.Combine(
                    binDir,
                    "winws.exe");

            if (!File.Exists(exePath))
            {
                Debug.WriteLine(
                    "winws.exe не найден");

                return false;
            }

            string args =
                GetArgsFromBat(config);

            if (string.IsNullOrWhiteSpace(args))
            {
                Debug.WriteLine(
                    $"Не удалось получить параметры {config}");

                return false;
            }

            var psi =
                new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    WorkingDirectory = binDir,

                    WindowStyle =
                        ProcessWindowStyle.Hidden,

                    UseShellExecute = true
                };

            Process? process =
                Process.Start(psi);

            return process != null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Ошибка запуска {config}: {ex.Message}");

            return false;
        }
    }
    private void StopWinwsOnly()
    {
        foreach (
            Process process
            in Process.GetProcessesByName("winws"))
        {
            try
            {
                process.Kill(true);

                process.WaitForExit(2000);
            }
            catch
            {
                // процесс уже мог завершиться
            }
            finally
            {
                process.Dispose();
            }
        }
    }

}