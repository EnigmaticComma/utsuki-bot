using System.Reflection;
using App.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace App.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAnnotatedServices(this IServiceCollection services, Assembly assembly)
    {
        var typesWithAttribute = assembly.GetTypes()
            .Where(type => type is { IsClass: true,IsAbstract: false } && type.GetCustomAttribute<ServiceAttribute>() != null);

        Console.Write("Begin add services: ");

        foreach (var type in typesWithAttribute)
        {
            var attribute = type.GetCustomAttribute<ServiceAttribute>();

            if(attribute == null) continue;

            Console.Write($"'{type.Name}' ");

            if (attribute.IsActivatedSingleton)
            {
                services.AddActivatedSingleton(type);
            }
            else
            {
                switch (attribute.Lifetime)
                {
                    case ServiceLifetime.Transient:
                        services.AddTransient(type);
                        break;
                    case ServiceLifetime.Scoped:
                        services.AddScoped(type);
                        break;
                    default:
                        services.AddSingleton(type);
                        break;
                }
            }
        }

        Console.WriteLine("Finished add services");

        return services;
    }

    public static IServiceCollection AddActivatedSingleton(this IServiceCollection services, Type type)
    {
        services.AddSingleton(type, serviceProvider => ActivatorUtilities.CreateInstance(serviceProvider, type));

        return services;
    }
}
