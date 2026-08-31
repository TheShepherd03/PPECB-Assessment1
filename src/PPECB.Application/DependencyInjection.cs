using Microsoft.Extensions.DependencyInjection;
using PPECB.Application.Abstractions;
using PPECB.Application.Services;

namespace PPECB.Application;

/// <summary>
/// Registers the Application layer's services. Each layer owns its own registration so
/// the API's startup does not need to know the internals of the layers beneath it.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductCodeGenerator, ProductCodeGenerator>();

        return services;
    }
}
