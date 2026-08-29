using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Compression.SevenZip.Abstract;

/// <summary>
/// A utility library for 7zip compression related operations
/// </summary>
public interface ISevenZipCompressionUtil
{
    /// <summary>
    /// Extracts advanced.
    /// </summary>
    /// <param name="fileNamePath">Path of the file name to use.</param>
    /// <param name="specificFileFilter">Specific File Filter for the extract advanced operation.</param>
    /// <param name="isParallel">Whether parallel.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the text returned by extract Advanced.</returns>
    ValueTask<string> ExtractAdvanced(string fileNamePath, string? specificFileFilter = null, bool isParallel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts seven Zip Compression.
    /// </summary>
    /// <param name="archivePath">Path of the archive to use.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the text returned by extract.</returns>
    ValueTask<string> Extract(string archivePath, CancellationToken cancellationToken = default);
}
