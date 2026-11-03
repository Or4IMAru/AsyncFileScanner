using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncFileScanner.Models;

namespace AsyncFileScanner.Services;

public sealed class FileScanner
{
    private readonly FileProbe _probe;

    public FileScanner(FileProbe probe)
    {
        _probe = probe;
    }

    public async Task<ScanSummary> ScanAsync(
        ScanOptions options,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);

        var stopwatch = Stopwatch.StartNew();
        var results = new List<ScanResult>();
        var errors = new List<string>();

        int scannedFiles = 0;
        long totalBytes = 0;

        try
        {
            var files = Directory.EnumerateFiles(options.DirectoryPath, "*.*", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var extension = Path.GetExtension(file).ToLowerInvariant();

                if (!options.Extensions.Contains(extension))
                {
                    continue;
                }

                scannedFiles++;

                try
                {
                    var fileInfo = new FileInfo(file);
                    long size = fileInfo.Length;

                    int headerRead = await _probe.ReadHeaderAsync(file, cancellationToken);

                    results.Add(new ScanResult(file, fileInfo.Name, size, headerRead));
                    totalBytes += size;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    errors.Add($"Ошибка доступа к файлу '{file}': {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException)
        {
            errors.Add($"Ошибка доступа к директории: {ex.Message}");
        }

        stopwatch.Stop();

        return new ScanSummary(
            scannedFiles,
            results.Count,
            totalBytes,
            stopwatch.Elapsed,
            results,
            errors);
    }

    private static void ValidateOptions(ScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            throw new ArgumentException("Путь к директории не может быть пустым.", nameof(options.DirectoryPath));

        if (options.Extensions == null || options.Extensions.Count == 0)
            throw new ArgumentException("Список расширений не может быть пустым.", nameof(options.Extensions));

        if (!Directory.Exists(options.DirectoryPath))
            throw new DirectoryNotFoundException($"Директория не найдена: {options.DirectoryPath}");
    }
}