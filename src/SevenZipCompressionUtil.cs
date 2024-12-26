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

namespace Soenneker.Compression.SevenZip;

/// <inheritdoc cref="ISevenZipCompressionUtil"/>
public class SevenZipCompressionUtil: ISevenZipCompressionUtil
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
        CancellationToken cancellation = default)
    {
        string tempDir = _directoryUtil.CreateTempDirectory();
        _logger.LogInformation("Extracting file ({file}) to temp dir ({dir})...", fileNamePath, tempDir);

        var createdDirectories = new HashSet<string>(); // To avoid redundant directory creation

        await using (Stream stream = File.OpenRead(fileNamePath))
        {
            using (SevenZipArchive archive = SevenZipArchive.Open(stream))
            {
                List<SevenZipArchiveEntry> entries = archive.Entries
                    .Where(entry => entry.Key != null && (!entry.IsDirectory || specificFileFilter == null || entry.Key.Contains(specificFileFilter)))
                    .ToList();

                await Task.WhenAll(entries.Select(async entry =>
                {
                    try
                    {
                        cancellation.ThrowIfCancellationRequested();

                        string entryPath = Path.Combine(tempDir, entry.Key!);

                        if (entry.IsDirectory)
                        {
                            // Ensure directory is created only once
                            if (createdDirectories.Add(entryPath))
                            {
                                _directoryUtil.CreateIfDoesNotExist(entryPath);
                            }
                        }
                        else
                        {
                            // Extract file
                            _logger.LogInformation("Extracting {message} ({size})...", entry.Key, entry.Size);
                            await Task.Run(() => entry.WriteToFile(entryPath), cancellation);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception extracting entry: {entry}", entry.Key);
                    }
                }));
            }
        }

        _logger.LogInformation("Finished extracting {fileName}", fileNamePath);

        string path = Path.Combine(tempDir, GetFirstDirectory(tempDir));
        return path;
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
