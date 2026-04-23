using Soenneker.Compression.SevenZip.Abstract;
using Soenneker.Tests.Attributes.Local;
using Soenneker.Tests.HostedUnit;
using System.Threading.Tasks;

namespace Soenneker.Compression.SevenZip.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public class SevenZipCompressionUtilTests : HostedUnitTest
{
    private readonly ISevenZipCompressionUtil _util;

    public SevenZipCompressionUtilTests(Host host) : base(host)
    {
        _util = Resolve<ISevenZipCompressionUtil>(true);
    }

    [Test]
    public void Default()
    {

    }

    [LocalOnly]
    public async ValueTask Extract()
    {
        string result = await _util.Extract(@"C:\7zip\test.7z.exe", System.Threading.CancellationToken.None);

    }
}

