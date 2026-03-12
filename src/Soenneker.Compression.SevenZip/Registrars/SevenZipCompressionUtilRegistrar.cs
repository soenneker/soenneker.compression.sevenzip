using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Compression.SevenZip.Abstract;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.Process.Registrars;

namespace Soenneker.Compression.SevenZip.Registrars;

/// <summary>
/// A utility library for 7zip compression related operations
/// </summary>
public static class SevenZipCompressionUtilRegistrar
{
    /// <summary>
    /// Adds <see cref="ISevenZipCompressionUtil"/> as a singleton service. <para/>
    /// </summary>
    public static IServiceCollection AddSevenZipCompressionUtilAsSingleton(this IServiceCollection services)
    {
        services.AddDirectoryUtilAsSingleton().AddProcessUtilAsSingleton().TryAddSingleton<ISevenZipCompressionUtil, SevenZipCompressionUtil>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="ISevenZipCompressionUtil"/> as a scoped service. <para/>
    /// </summary>
    public static IServiceCollection AddSevenZipCompressionUtilAsScoped(this IServiceCollection services)
    {
        services.AddDirectoryUtilAsScoped().AddProcessUtilAsScoped().TryAddScoped<ISevenZipCompressionUtil, SevenZipCompressionUtil>();

        return services;
    }
}