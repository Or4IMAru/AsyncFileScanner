using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncFileScanner.Models;

namespace AsyncFileScanner.Services;

public sealed class ConcurrencyTracker
{
    private int _activeOperations;
    private int _maxObservedOperations;

    public void Enter()
    {
        int current = Interlocked.Increment(ref _activeOperations);
        int max;

        do
        {
            max = _maxObservedOperations;
            if (current <= max) break; 
        }
        while (Interlocked.CompareExchange(ref _maxObservedOperations, current, max) != max);
    }

    public void Exit()
    {
        Interlocked.Decrement(ref _activeOperations);
    }

    public int MaxObserved => _maxObservedOperations;
}

public sealed class FileScanner
{
    private readonly FileProbe _probe;

    public FileScanner(FileProbe probe)
    {
        _probe = probe;
    }

    public async Task<ScanSummary> ScanAsync(
        ScanOptions options,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);

        var stopwatch = Stopwatch.StartNew();
        var results = new ConcurrentBag<ScanResult>();
        var errors = new ConcurrentBag<string>();
        var tracker = new ConcurrencyTracker();

        int totalScanned = 0;
        int matchingCount = 0;
        long totalMatchingBytes = 0;

        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var files = Directory.EnumerateFiles(options.DirectoryPath, "*.*", SearchOption.AllDirectories);
        var tasks = new List<Task<ScanResult?>>();

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested) break;

            Interlocked.Increment(ref totalScanned);

            long fileSize;
            try
            {

                fileSize = new FileInfo(file).Length;
            }
            catch (Exception ex)
            {
                errors.Add($"Ошибка чтения метаданных '{file}': {ex.Message}");
                continue;
            }

            if (fileSize < options.MinSizeBytes || fileSize > options.MaxSizeBytes)
            {
                continue;
            }

            Interlocked.Increment(ref matchingCount);
            Interlocked.Add(ref totalMatchingBytes, fileSize);

            var task = ProcessFileLimitedAsync(file, fileSize, semaphore, tracker, errors, cancellationToken);
            tasks.Add(task);
        }

        ScanResult?[] processedResults = await Task.WhenAll(tasks);

        foreach (var res in processedResults)
        {
            if (res != null)
            {
                results.Add(res);
            }
        }

        stopwatch.Stop();

        return new ScanSummary(
            totalScanned,
            matchingCount,
            totalMatchingBytes,
            errors.Count,
            stopwatch.Elapsed,
            maxConcurrency,
            tracker.MaxObserved,
            results.ToList(),
            errors.ToList());
    }

    private async Task<ScanResult?> ProcessFileLimitedAsync(
        string path,
        long fileSize,
        SemaphoreSlim semaphore,
        ConcurrencyTracker tracker,
        ConcurrentBag<string> errors,
        CancellationToken token)
    {
        await semaphore.WaitAsync(token);

        try
        {
            tracker.Enter();
            try
            {
                int headerRead = await _probe.ReadHeaderAsync(path, token);
                return new ScanResult(path, Path.GetFileName(path), fileSize, headerRead);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                errors.Add($"Ошибка доступа к файлу '{path}': {ex.Message}");
                return null;
            }
            finally
            {
                tracker.Exit();
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static void ValidateOptions(ScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            throw new ArgumentException("Путь к директории не может быть пустым.", nameof(options.DirectoryPath));

        if (options.Extensions == null || options.Extensions.Count == 0)
            throw new ArgumentException("Список расширений не может быть пустым.", nameof(options.Extensions));

        if (!Directory.Exists(options.DirectoryPath))
            throw new DirectoryNotFoundException($"Директория не найдена: {options.DirectoryPath}");

        if (options.MinSizeBytes < 0)
            throw new ArgumentException("Минимальный размер не может быть отрицательным.", nameof(options.MinSizeBytes));

        if (options.MaxSizeBytes < options.MinSizeBytes && options.MaxSizeBytes != long.MaxValue)
            throw new ArgumentException("Максимальный размер должен быть больше или равен минимальному.", nameof(options.MaxSizeBytes));
    }
}