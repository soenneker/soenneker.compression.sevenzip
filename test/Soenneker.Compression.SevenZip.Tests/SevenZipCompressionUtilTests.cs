using Soenneker.Compression.SevenZip.Abstract;
using Soenneker.Tests.FixturedUnit;
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
}
