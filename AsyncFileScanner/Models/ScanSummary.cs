using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncFileScanner.Models;

public sealed record ScanSummary(
    int TotalFilesScanned,
    int MatchingFilesCount,
    long TotalMatchingSizeBytes,
    int ErrorCount,
    TimeSpan ElapsedTime,
    int ConcurrencyLimit,
    int MaxObservedConcurrentOperations,
    IReadOnlyList<ScanResult> Results,
    IReadOnlyList<string> Errors);
