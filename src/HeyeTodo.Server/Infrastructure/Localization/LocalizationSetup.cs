using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;

namespace HeyeTodo.Server.Infrastructure.Localization;

public static class LocalizationSetup
{
    public static readonly CultureInfo[] SupportedCultures =
    [
        new("en"),
        new("zh"),
    ];

    public static IServiceCollection AddHeyeLocalization(this IServiceCollection services)
    {
        services.AddLocalization(o => o.ResourcesPath = "Resources");
        return services;
    }

    public static WebApplication UseHeyeLocalization(this WebApplication app)
    {
        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture("en"),
        };
        options.SupportedCultures = SupportedCultures;
        options.SupportedUICultures = SupportedCultures;
        app.UseRequestLocalization(options);
        return app;
    }
}
