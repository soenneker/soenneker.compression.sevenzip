using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Compression.SevenZip.Abstract;

/// <summary>
/// A utility library for 7zip compression related operations
/// </summary>
public interface ISevenZipCompressionUtil
{
    ValueTask<string> Extract(
        string fileNamePath,
        string? specificFileFilter = null,
        CancellationToken cancellation = default);
}