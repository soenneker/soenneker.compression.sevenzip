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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.String;

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

    public async ValueTask<string> ExtractAdvanced(
        string fileNamePath,
        string? specificFileFilter = null,
        bool isConcurrent = true,
        CancellationToken cancellationToken = default)
    {
        string tempDir = await _directoryUtil.CreateTempDirectory(cancellationToken).NoSync();
        _logger.LogInformation("Extracting file ({file}) to temp dir ({dir})...", fileNamePath, tempDir);

        // Full, normalized root used for traversal protection
        string rootFullPath = EnsureTrailingSeparator(Path.GetFullPath(tempDir));

        var fsOptions = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan
        };

        await using var stream = new FileStream(fileNamePath, fsOptions);
        IAsyncArchive archive = await SevenZipArchive.OpenAsyncArchive(stream, cancellationToken: cancellationToken).NoSync();

        // Materialize matching entries once; SevenZipArchiveEntry is a reference type
        // and we need a stable snapshot before extracting.
        List<IArchiveEntry> entries = new(capacity: 32);

        await foreach (IArchiveEntry archiveEntry in archive.EntriesAsync.WithCancellation(cancellationToken))
        {
            // Fast rejects
            if (archiveEntry.IsDirectory)
                continue;

            string? key = archiveEntry.Key;
            if (key.IsNullOrEmpty())
                continue;

            if (specificFileFilter != null && !key.EndsWith(specificFileFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            entries.Add(archiveEntry);
        }

        if (entries.Count == 0)
        {
            _logger.LogWarning("No entries matched the specified filter '{filter}'.", specificFileFilter);
            return tempDir;
        }

        if (isConcurrent)
        {
            // Bounded concurrency prevents threadpool thrash on large archives.
            int dop = Math.Clamp(Environment.ProcessorCount, 1, 8);
            using var gate = new SemaphoreSlim(dop, dop);

            var tasks = new Task[entries.Count];

            for (var i = 0; i < entries.Count; i++)
            {
                IArchiveEntry entry = entries[i];
                tasks[i] = ProcessEntryBounded(entry, rootFullPath, gate, cancellationToken);
            }

            await Task.WhenAll(tasks).NoSync();
        }
        else
        {
            for (var i = 0; i < entries.Count; i++)
                await ProcessEntryInline(entries[i], rootFullPath, cancellationToken).NoSync();
        }

        _logger.LogInformation("Finished extracting {fileName} to directory ({dir})", fileNamePath, tempDir);
        return tempDir;
    }

    private Task ProcessEntryBounded(
        IArchiveEntry entry,
        string rootFullPath,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        // Minimal async state: wait bounded, then run extraction on threadpool (SharpCompress is sync).
        return Task.Run(async () =>
        {
            await gate.WaitAsync(cancellationToken).NoSync();

            try
            {
                await ProcessEntryInline(entry, rootFullPath, cancellationToken).NoSync();
            }
            finally
            {
                gate.Release();
            }
        }, cancellationToken);
    }

    private async ValueTask ProcessEntryInline(
        IArchiveEntry entry,
        string rootFullPath,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            string key = entry.Key!;

            // Compute safe destination path (blocks traversal)
            string destinationPath = GetSafeDestinationPath(rootFullPath, key);

            // Ensure containing directory exists (cheap if already exists)
            string? dir = Path.GetDirectoryName(destinationPath);

            await _directoryUtil.CreateIfDoesNotExist(dir, true, cancellationToken).NoSync();

            // Per-entry info logs can be *very* noisy/slow on big archives.
            _logger.LogDebug("Extracting {entry} ({size})...", key, entry.Size);

            // Sync write (SharpCompress). Overwrite semantics depend on SharpCompress version;
            // keep default behavior to avoid unexpected changes.
            await entry.WriteToFileAsync(destinationPath, cancellationToken).NoSync();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception extracting entry: {entry}", entry.Key);
        }
    }

    private static string GetSevenZipExecutable()
    {
        if (RuntimeUtil.IsLinux())
            return "7zzs";

        if (RuntimeUtil.IsWindows())
            return "7za.exe";

        throw new PlatformNotSupportedException("7-Zip not supported on this OS.");
    }

    public async ValueTask<string> Extract(string archivePath, CancellationToken cancellationToken = default)
    {
        string executable = GetSevenZipExecutable();

        string tempDir = await _directoryUtil.CreateTempDirectory(cancellationToken).NoSync();
        _logger.LogInformation("Extracting file ({file}) to temp dir ({dir})...", archivePath, tempDir);

        // Only one string allocation here; fine.
        var args = $"x \"{archivePath}\" -o\"{tempDir}\" -y";

        _logger.LogInformation("Running 7-Zip extraction: {exe} {args}", executable, args);

        string executablePath = Path.Combine(AppContext.BaseDirectory, "Resources", executable);

        _ = await _processUtil.Start(executablePath, null, args, cancellationToken: cancellationToken).NoSync();

        _logger.LogInformation("7-Zip extraction complete");
        return tempDir;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string EnsureTrailingSeparator(string path)
    {
        if (path.Length == 0)
            return path;

        char last = path[^1];
        if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
            return path;

        return path + Path.DirectorySeparatorChar;
    }

    private static string GetSafeDestinationPath(string rootFullPath, string entryKey)
    {
        // Normalize separators (archives often use '/')
        string normalizedRelative = entryKey.Replace('/', Path.DirectorySeparatorChar);

        // Combine + fullpath, then verify it's still under root
        string combined = Path.Combine(rootFullPath, normalizedRelative);
        string full = Path.GetFullPath(combined);

        // Root already has trailing separator; this becomes a cheap prefix test.
        if (!full.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Archive entry path escapes destination directory: {entryKey}");

        return full;
    }
}
