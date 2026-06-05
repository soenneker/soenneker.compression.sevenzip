using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Compression.SevenZip.Abstract;

/// <summary>
/// A utility library for 7zip compression related operations
/// </summary>
public interface ISevenZipCompressionUtil
{
    /// <summary>
    /// Executes the extract advanced operation.
    /// </summary>
    /// <param name="fileNamePath">The file name path.</param>
    /// <param name="specificFileFilter">The specific file filter.</param>
    /// <param name="isParallel">The is parallel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    ValueTask<string> ExtractAdvanced(string fileNamePath, string? specificFileFilter = null, bool isParallel = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the extract operation.
    /// </summary>
    /// <param name="archivePath">The archive path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    ValueTask<string> Extract(string archivePath, CancellationToken cancellationToken = default);
}