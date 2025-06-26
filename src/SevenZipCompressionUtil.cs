using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using Soenneker.Compression.SevenZip.Abstract;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Process.Abstract;
using Soenneker.Utils.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Compression.SevenZip;

/// <inheritdoc cref="ISevenZipCompressionUtil"/>
public sealed class SevenZipCompressionUtil : ISevenZipCompressionUtil
{
    private readonly ILogger<SevenZipCompressionUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IProcessUtil _processUtil;

    public SevenZipCompressionUtil(ILogger<SevenZipCompressionUtil> logger, IDirectoryUtil directoryUtil, IProcessUtil processUtil)
    {
        _logger = logger;
        _directoryUtil = directoryUtil;
        _processUtil = processUtil;
    }

    public async ValueTask<string> ExtractAdvanced(string fileNamePath, string? specificFileFilter = null, bool isParallel = true,
        CancellationToken cancellationToken = default)
    {
        string tempDir = await _directoryUtil.CreateTempDirectory(cancellationToken).NoSync();
        _logger.LogInformation("Extracting file ({file}) to temp dir ({dir})...", fileNamePath, tempDir);

        await using (Stream stream = File.OpenRead(fileNamePath))
        {
            using (SevenZipArchive archive = SevenZipArchive.Open(stream))
            {
                // Filter entries
                List<SevenZipArchiveEntry> entries = archive.Entries.Where(entry =>
                                                                entry.Key != null && !entry.IsDirectory &&
                                                                (specificFileFilter == null || entry.Key.EndsWith(specificFileFilter,
                                                                    StringComparison.OrdinalIgnoreCase)))
                                                            .ToList();

                if (entries.Count == 0)
                {
                    _logger.LogWarning("No entries matched the specified filter '{filter}'.", specificFileFilter);
                    return tempDir; // Return the temp directory even if nothing was extracted
                }

                // Pre-create directories
                List<string> directoriesToCreate = entries.Select(entry => Path.Combine(tempDir, Path.GetDirectoryName(entry.Key!)!)).Distinct().ToList();

                foreach (string directory in directoriesToCreate)
                {
                    _directoryUtil.CreateIfDoesNotExist(directory);
                }

                // Extract entries
                if (isParallel)
                {
                    await Task.WhenAll(entries.Select(entry => ProcessEntry(entry, tempDir, cancellationToken))).NoSync();
                }
                else
                {
                    foreach (SevenZipArchiveEntry entry in entries)
                    {
                        await ProcessEntry(entry, tempDir, cancellationToken).NoSync();
                    }
                }
            }
        }

        _logger.LogInformation("Finished extracting {fileName}", fileNamePath);

        return Path.Combine(tempDir, GetFirstDirectory(tempDir));
    }

    private static string GetSevenZipExecutable()
    {
        if (RuntimeUtil.IsWindows())
            return "7za.exe";

        if (RuntimeUtil.IsLinux())
            return "7zz";

        throw new PlatformNotSupportedException("7-Zip not supported on this OS.");
    }

    public async ValueTask<string> Extract(string archivePath, CancellationToken cancellationToken = default)
    {
        string executable = GetSevenZipExecutable();

        string tempDir = await _directoryUtil.CreateTempDirectory(cancellationToken).NoSync();
        _logger.LogInformation("Extracting file ({file}) to temp dir ({dir})...", archivePath, tempDir);

        var args = $"x \"{archivePath}\" -o\"{tempDir}\" -y";

        _logger.LogInformation("Running 7-Zip extraction: {exe} {args}", executable, args);

        string executablePath = Path.Combine(AppContext.BaseDirectory, "Resources", executable);

        List<string> result = await _processUtil.Start(executablePath, null, args, false, true, null, true, cancellationToken).NoSync();

        _logger.LogInformation("7-Zip extraction complete");

        return tempDir;
    }

    private Task ProcessEntry(SevenZipArchiveEntry entry, string tempDir, CancellationToken cancellation)
    {
        try
        {
            cancellation.ThrowIfCancellationRequested();

            string entryPath = Path.Combine(tempDir, entry.Key!);

            // Extract file
            _logger.LogInformation("Extracting {message} ({size})...", entry.Key, entry.Size);
            return Task.Run(() => entry.WriteToFile(entryPath), cancellation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception extracting entry: {entry}", entry.Key);
        }

        return Task.CompletedTask;
    }


    private static string GetLastPart(string path)
    {
        return path.Split(Path.DirectorySeparatorChar).Last();
    }

    private static string GetFirstDirectory(string path)
    {
        string directory = Directory.GetDirectories(path).First();
        return GetLastPart(directory);
    }
}