using Soenneker.Compression.SevenZip.Abstract;
using Soenneker.Facts.Local;
using Soenneker.Tests.FixturedUnit;
using System.Threading.Tasks;
using Xunit;

namespace Soenneker.Compression.SevenZip.Tests;

[Collection("Collection")]
public class SevenZipCompressionUtilTests : FixturedUnitTest
{
    private readonly ISevenZipCompressionUtil _util;

    public SevenZipCompressionUtilTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
        _util = Resolve<ISevenZipCompressionUtil>(true);
    }

    [Fact]
    public void Default()
    {

    }

    [LocalFact]
    public async ValueTask Extract()
    {
        var result = await _util.Extract(@"C:\7zip\test.7z.exe", CancellationToken);

    }
}
