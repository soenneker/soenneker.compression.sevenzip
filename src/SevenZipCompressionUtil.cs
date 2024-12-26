using Soenneker.Compression.SevenZip.Abstract;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using Soenneker.Utils.Directory.Abstract;
using System.Threading;
using System.Collections.Generic;
using Soenneker.Extensions.Task;

namespace Soenneker.Compression.SevenZip;

/// <inheritdoc cref="ISevenZipCompressionUtil"/>
public class SevenZipCompressionUtil : ISevenZipCompressionUtil
{
    private readonly ILogger<SevenZipCompressionUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;

    public SevenZipCompressionUtil(ILogger<SevenZipCompressionUtil> logger, IDirectoryUtil directoryUtil)
    {
        _logger = logger;
        _directoryUtil = directoryUtil;
    }

    public async ValueTask<string> Extract(
        string fileNamePath,
        string? specificFileFilter = null,
        bool isParallel = true,
        CancellationToken cancellation = default)
    {
        string tempDir = _directoryUtil.CreateTempDirectory();
        _logger.LogInformation("Extracting file ({file}) to temp dir ({dir})...", fileNamePath, tempDir);

        await using (Stream stream = File.OpenRead(fileNamePath))
        {
            using (SevenZipArchive archive = SevenZipArchive.Open(stream))
            {
                // Filter entries
                List<SevenZipArchiveEntry> entries = archive.Entries
                    .Where(entry =>
                        entry.Key != null &&
                        !entry.IsDirectory &&
                        (specificFileFilter == null || entry.Key.EndsWith(specificFileFilter, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (entries.Count == 0)
                {
                    _logger.LogWarning("No entries matched the specified filter '{filter}'.", specificFileFilter);
                    return tempDir; // Return the temp directory even if nothing was extracted
                }

                // Pre-create directories
                List<string> directoriesToCreate = entries
                    .Select(entry => Path.Combine(tempDir, Path.GetDirectoryName(entry.Key!)!))
                    .Distinct()
                    .ToList();

                foreach (string directory in directoriesToCreate)
                {
                    _directoryUtil.CreateIfDoesNotExist(directory);
                }

                // Extract entries
                if (isParallel)
                {
                    await Task.WhenAll(entries.Select(entry => ProcessEntryAsync(entry, tempDir, cancellation))).NoSync();
                }
                else
                {
                    foreach (SevenZipArchiveEntry entry in entries)
                    {
                        await ProcessEntryAsync(entry, tempDir, cancellation).NoSync();
                    }
                }
            }
        }

        _logger.LogInformation("Finished extracting {fileName}", fileNamePath);

        string path = Path.Combine(tempDir, GetFirstDirectory(tempDir));
        return path;
    }

    private Task ProcessEntryAsync(
        SevenZipArchiveEntry entry,
        string tempDir,
        CancellationToken cancellation)
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
        directory = GetLastPart(directory);
        return directory;
    }
}