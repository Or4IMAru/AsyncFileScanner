using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncFileScanner.Models;
using AsyncFileScanner.Services;

Console.WriteLine("=== FILE SCANNER ===\n");

Console.WriteLine("Введите путь к директории:");
string dirPath = Console.ReadLine()?.Trim() ?? "";

Console.WriteLine("\nВведите расширения через запятую (например: .cs,.txt,.json):");
string extInput = Console.ReadLine()?.Trim() ?? "";

var extensions = extInput
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(e => e.StartsWith(".") ? e : "." + e)
    .ToList();

var options = new ScanOptions(dirPath, extensions);

using var cts = new CancellationTokenSource();

Console.WriteLine("\nСканирование...");
Console.WriteLine("Нажмите Enter для отмены.\n");

var cancelTask = Task.Run(() =>
{
    Console.ReadLine();
    cts.Cancel();
});

var probe = new FileProbe();
var scanner = new FileScanner(probe);

try
{
    var summary = await scanner.ScanAsync(options, cts.Token);

    if (cts.Token.IsCancellationRequested)
    {
        Console.WriteLine("\nСканирование отменено пользователем.");
        Console.WriteLine($"Обработано файлов: {summary.TotalFilesScanned}");
    }
    else
    {
        Console.WriteLine("\nНайденные файлы:\n");

        for (int i = 0; i < summary.Results.Count; i++)
        {
            var res = summary.Results[i];
            Console.WriteLine($"[{i + 1}] {res.FileName,-20} {res.SizeInBytes} байт");
        }

        Console.WriteLine("\nСканирование завершено.");
        Console.WriteLine($"\nВсего найдено файлов: {summary.SuccessfulFiles}");
        Console.WriteLine($"Общий размер: {summary.TotalBytes} байт");

        if (summary.Errors.Count > 0)
        {
            Console.WriteLine("\nОшибки при обработке:");
            foreach (var err in summary.Errors)
            {
                Console.WriteLine($"- {err}");
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\nКритическая ошибка: {ex.Message}");
}