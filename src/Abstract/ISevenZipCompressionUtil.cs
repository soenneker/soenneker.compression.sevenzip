using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Compression.SevenZip.Abstract;

/// <summary>
/// A utility library for 7zip compression related operations
/// </summary>
public interface ISevenZipCompressionUtil
{
    ValueTask<string> ExtractAdvanced(string fileNamePath, string? specificFileFilter = null, bool isParallel = true, CancellationToken cancellationToken = default);

    ValueTask<string> Extract(string archivePath, CancellationToken cancellationToken = default);
}