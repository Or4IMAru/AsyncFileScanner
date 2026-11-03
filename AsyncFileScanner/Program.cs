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

Console.WriteLine("\nМинимальный размер (КБ):");
long minKb = long.Parse(Console.ReadLine()?.Trim() ?? "0");

Console.WriteLine("Максимальный размер (КБ) (0 для без ограничений):");
long maxKb = long.Parse(Console.ReadLine()?.Trim() ?? "0");

long minBytes = minKb * 1024;
long maxBytes = maxKb == 0 ? long.MaxValue : maxKb * 1024;

Console.WriteLine("\nВведите максимальное количество одновременных операций:");
int maxConcurrency = int.Parse(Console.ReadLine()?.Trim() ?? "4");

var options = new ScanOptions(dirPath, extensions, minBytes, maxBytes);

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
    var summary = await scanner.ScanAsync(options, maxConcurrency, cts.Token);

    if (cts.Token.IsCancellationRequested)
    {
        Console.WriteLine("\nСканирование отменено пользователем.");
        Console.WriteLine($"Обработано файлов: {summary.TotalFilesScanned}");
    }
    else
    {
        Console.WriteLine("\n=== РЕЗУЛЬТАТ СКАНИРОВАНИЯ ===\n");
        Console.WriteLine($"Просмотрено файлов: {summary.TotalFilesScanned}");
        Console.WriteLine($"Подходящих файлов: {summary.MatchingFilesCount}");

        double sizeInMb = summary.TotalMatchingSizeBytes / (1024.0 * 1024.0);
        Console.WriteLine($"Общий размер: {sizeInMb:F2} МБ");

        Console.WriteLine($"Ошибок: {summary.ErrorCount}");
        Console.WriteLine($"Время выполнения: {summary.ElapsedTime.TotalSeconds:F2} сек.");

        Console.WriteLine($"\nОграничение: {summary.ConcurrencyLimit}");
        Console.WriteLine($"Максимум одновременно обрабатываемых файлов: {summary.MaxObservedConcurrentOperations}");

        if (summary.Results.Count > 0)
        {
            Console.WriteLine("\nНайденные файлы (первые 10):");
            var topResults = summary.Results.Take(10).ToList();
            for (int i = 0; i < topResults.Count; i++)
            {
                var res = topResults[i];
                Console.WriteLine($"[{i + 1}] {res.FileName,-25} {res.SizeInBytes,10} байт (прочитано заголовка: {res.HeaderBytesRead})");
            }

            if (summary.Results.Count > 10)
            {
                Console.WriteLine($"... и ещё {summary.Results.Count - 10} файлов.");
            }
        }

        if (summary.Errors.Count > 0)
        {
            Console.WriteLine("\nОшибки при обработке (первые 5):");
            foreach (var err in summary.Errors.Take(5))
            {
                Console.WriteLine($"- {err}");
            }
            if (summary.Errors.Count > 5)
            {
                Console.WriteLine($"... и ещё {summary.Errors.Count - 5} ошибок.");
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\nКритическая ошибка: {ex.Message}");
}