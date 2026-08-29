[![](https://img.shields.io/nuget/v/soenneker.compression.sevenzip.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.compression.sevenzip/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.compression.sevenzip/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.compression.sevenzip/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.compression.sevenzip.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.compression.sevenzip/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.compression.sevenzip/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.compression.sevenzip/actions/workflows/codeql.yml)

# Soenneker.Compression.SevenZip

A utility library for 7zip compression related operations.

## Install

```bash
dotnet add package Soenneker.Compression.SevenZip
```

## Quick start

```csharp
using Soenneker.Compression.SevenZip.Registrars;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
var result = services.AddSevenZipCompressionUtilAsSingleton();
```

Adds `ISevenZipCompressionUtil` as a singleton service.

## What you get

- `ISevenZipCompressionUtil` — A utility library for 7zip compression related operations.
- `SevenZipCompressionUtilRegistrar` — A utility library for 7zip compression related operations.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `ISevenZipCompressionUtil.ExtractAdvanced(fileNamePath, specificFileFilter, isParallel, cancellationToken)` | Extracts advanced. | A task whose result is the text returned by extract Advanced. |
| `ISevenZipCompressionUtil.Extract(archivePath, cancellationToken)` | Extracts seven Zip Compression. | A task whose result is the text returned by extract. |
| `SevenZipCompressionUtilRegistrar.AddSevenZipCompressionUtilAsSingleton(services)` | Adds `ISevenZipCompressionUtil` as a singleton service. | The same service collection, so additional registrations can be chained. |
| `SevenZipCompressionUtilRegistrar.AddSevenZipCompressionUtilAsScoped(services)` | Adds `ISevenZipCompressionUtil` as a scoped service. | The same service collection, so additional registrations can be chained. |

## Practical notes

- Cancellation stops pending work; it does not undo work that has already completed.
