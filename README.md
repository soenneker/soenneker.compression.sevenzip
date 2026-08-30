[![](https://img.shields.io/nuget/v/soenneker.compression.sevenzip.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.compression.sevenzip/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.compression.sevenzip/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.compression.sevenzip/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.compression.sevenzip.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.compression.sevenzip/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.compression.sevenzip/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.compression.sevenzip/actions/workflows/codeql.yml)

# Soenneker.Compression.SevenZip

Extracts 7-Zip archives into temporary directories using either SharpCompress or a bundled native 7-Zip executable.

## Installation

```bash
dotnet add package Soenneker.Compression.SevenZip
```

## Registration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Soenneker.Compression.SevenZip.Registrars;

services.AddSevenZipCompressionUtilAsSingleton();
```

`AddSevenZipCompressionUtilAsScoped()` is also available.

## Managed extraction

`ExtractAdvanced` uses SharpCompress and supports suffix filtering:

```csharp
using Soenneker.Compression.SevenZip.Abstract;

string outputDirectory = await sevenZip.ExtractAdvanced(
    archivePath,
    specificFileFilter: ".json",
    cancellationToken: cancellationToken);

try
{
    foreach (string file in Directory.EnumerateFiles(
                 outputDirectory,
                 "*",
                 SearchOption.AllDirectories))
    {
        // Process each extracted JSON file.
    }
}
finally
{
    Directory.Delete(outputDirectory, recursive: true);
}
```

The filter is a case-insensitive `EndsWith` match, not a glob or regular expression. Directories are skipped. When nothing matches, the method returns an empty temporary directory.

Managed extraction is sequential by default. Set `isParallel: true` only for a trusted, tested workload where concurrent entry extraction is beneficial.

## Native extraction

`Extract` runs the packaged native 7-Zip executable and extracts the complete archive:

```csharp
string outputDirectory = await sevenZip.Extract(
    archivePath,
    cancellationToken);
```

The native path is available on Windows and Linux. It throws `PlatformNotSupportedException` on other operating systems. A non-zero 7-Zip exit code is surfaced as an exception.

## Output ownership and failure behavior

- Both methods create and return a new temporary directory. The caller owns that directory and must delete it after consuming the files.
- On cancellation or extraction failure, the utility attempts to remove the incomplete directory before rethrowing the original error.
- The source archive is opened read-only and is not deleted or modified.
- Managed extraction rejects absolute/traversing paths, symbolic-link entries, and multiple entries that resolve to the same destination.
- Managed entry failures are propagated; the method does not report a partially extracted directory as success.

## Handling untrusted archives

Path validation does not make arbitrary archives resource-safe. Neither extraction method imposes limits on expanded byte count, compression ratio, entry count, nesting, or execution time beyond caller cancellation. Validate archive size and provenance, enforce application-level quotas and timeouts, and use an isolated process or container when accepting untrusted uploads.

Do not process extracted files as executable content merely because extraction succeeded.
