using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncFileScanner.Models;

public sealed record ScanResult(
    string FilePath,
    string FileName,
    long SizeInBytes,
    int HeaderBytesRead);
