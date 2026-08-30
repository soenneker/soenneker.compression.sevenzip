using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Compression.SevenZip.Abstract;

/// <summary>
/// Extracts 7-Zip archives into caller-owned temporary directories.
/// </summary>
public interface ISevenZipCompressionUtil
{
    /// <summary>
    /// Extracts matching regular files with the managed SharpCompress reader.
    /// </summary>
    /// <param name="fileNamePath">Path of the 7-Zip archive.</param>
    /// <param name="specificFileFilter">Optional case-insensitive filename suffix. This is not a glob.</param>
    /// <param name="isParallel">Whether to extract entries concurrently. Sequential extraction is the safe default.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The temporary output directory. The caller is responsible for deleting it.</returns>
    ValueTask<string> ExtractAdvanced(string fileNamePath, string? specificFileFilter = null, bool isParallel = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts an archive with the bundled native 7-Zip executable.
    /// </summary>
    /// <param name="archivePath">Path of the 7-Zip archive.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The temporary output directory. The caller is responsible for deleting it.</returns>
    ValueTask<string> Extract(string archivePath, CancellationToken cancellationToken = default);
}
