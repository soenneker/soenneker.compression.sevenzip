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
using System.Text;
using Soenneker.Utils.PooledStringBuilders;

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

    public async ValueTask<string> ExtractAdvanced(string fileNamePath, string? specificFileFilter = null, bool isParallel = false,
        CancellationToken cancellationToken = default)
    {
        string tempDir = await _directoryUtil.CreateTempDirectory(cancellationToken)
                                             .NoSync();
        _logger.LogInformation("Extracting file ({file}) to temp dir ({dir})...", fileNamePath, tempDir);

        try
        {

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
        await using IAsyncArchive archive = await SevenZipArchive.OpenAsyncArchive(stream, cancellationToken: cancellationToken)
                                                          .NoSync();

        // Materialize matching entries once; SevenZipArchiveEntry is a reference type
        // and we need a stable snapshot before extracting.
        List<IArchiveEntry> entries = new(capacity: 32);
        var destinationPaths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        await foreach (IArchiveEntry archiveEntry in archive.EntriesAsync.WithCancellation(cancellationToken))
        {
            // Fast rejects
            if (archiveEntry.IsDirectory)
                continue;

            string? key = archiveEntry.Key;
            if (key.IsNullOrEmpty())
                continue;

            if (!archiveEntry.LinkTarget.IsNullOrEmpty())
                throw new InvalidDataException($"Archive entry is a symbolic link and cannot be extracted safely: {key}");

            if (specificFileFilter != null && !key.EndsWith(specificFileFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            string destinationPath = GetSafeDestinationPath(rootFullPath, key);
            if (!destinationPaths.Add(destinationPath))
                throw new InvalidDataException($"Multiple archive entries resolve to the same destination: {key}");

            entries.Add(archiveEntry);
        }

        if (entries.Count == 0)
        {
            _logger.LogWarning("No entries matched the specified filter '{filter}'.", specificFileFilter);
            return tempDir;
        }

        if (isParallel)
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

            await Task.WhenAll(tasks)
                      .NoSync();
        }
        else
        {
            for (var i = 0; i < entries.Count; i++)
                await ProcessEntryInline(entries[i], rootFullPath, cancellationToken)
                    .NoSync();
        }

        _logger.LogInformation("Finished extracting {fileName} to directory ({dir})", fileNamePath, tempDir);
        return tempDir;
        }
        catch
        {
            try
            {
                await _directoryUtil.DeleteIfExists(tempDir, CancellationToken.None).NoSync();
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarning(cleanupException, "Could not remove incomplete extraction directory {dir}", tempDir);
            }

            throw;
        }
    }

    private Task ProcessEntryBounded(IArchiveEntry entry, string rootFullPath, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        // Minimal async state: wait bounded, then run extraction on threadpool (SharpCompress is sync).
        return Task.Run(async () =>
        {
            await gate.WaitAsync(cancellationToken)
                      .NoSync();

            try
            {
                await ProcessEntryInline(entry, rootFullPath, cancellationToken)
                    .NoSync();
            }
            finally
            {
                gate.Release();
            }
        }, cancellationToken);
    }

    private async ValueTask ProcessEntryInline(IArchiveEntry entry, string rootFullPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string key = entry.Key!;

        // Compute safe destination path (blocks traversal)
        string destinationPath = GetSafeDestinationPath(rootFullPath, key);

        // Ensure containing directory exists (cheap if already exists)
        string dir = Path.GetDirectoryName(destinationPath)!;

        await _directoryUtil.Create(dir, true, cancellationToken)
                            .NoSync();

        // Per-entry info logs can be *very* noisy/slow on big archives.
        _logger.LogDebug("Extracting {entry} ({size})...", key, entry.Size);

        await entry.WriteToFileAsync(destinationPath, null, cancellationToken)
                   .NoSync();
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

        string tempDir = await _directoryUtil.CreateTempDirectory(cancellationToken)
                                             .NoSync();
        _logger.LogInformation("Extracting file ({file}) to temp dir ({dir})...", archivePath, tempDir);

        try
        {
            string args = $"x {QuoteProcessArgument(archivePath)} {QuoteProcessArgument($"-o{tempDir}")} -y -bso0 -bsp0";

            _logger.LogInformation("Running bundled 7-Zip extraction with {exe}", executable);

            string executablePath = Path.Combine(AppContext.BaseDirectory, "Resources", executable);

            _ = await _processUtil.Start(executablePath, null, args, cancellationToken: cancellationToken)
                                  .NoSync();

            _logger.LogInformation("7-Zip extraction complete");
            return tempDir;
        }
        catch
        {
            try
            {
                await _directoryUtil.DeleteIfExists(tempDir, CancellationToken.None).NoSync();
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarning(cleanupException, "Could not remove incomplete extraction directory {dir}", tempDir);
            }

            throw;
        }
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
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.StartsWith(rootFullPath, comparison))
            throw new InvalidOperationException($"Archive entry path escapes destination directory: {entryKey}");

        return full;
    }

    private static string QuoteProcessArgument(string argument)
    {
        using var builder = new PooledStringBuilder(argument.Length + 2);
        builder.Append('"');

        var backslashCount = 0;
        foreach (char character in argument)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', (backslashCount * 2) + 1);
                builder.Append('"');
            }
            else
            {
                builder.Append('\\', backslashCount);
                builder.Append(character);
            }

            backslashCount = 0;
        }

        builder.Append('\\', backslashCount * 2);
        builder.Append('"');
        return builder.ToString();
    }
}
