using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncFileScanner.Models;

public sealed record ScanSummary(
    int TotalFilesScanned,
    int SuccessfulFiles,
    long TotalBytes,
    TimeSpan ElapsedTime,
    IReadOnlyList<ScanResult> Results,
    IReadOnlyList<string> Errors);
