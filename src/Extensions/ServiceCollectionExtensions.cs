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

        Console.WriteLine("Begin add services:");

        foreach (var type in typesWithAttribute)
        {
            var attribute = type.GetCustomAttribute<ServiceAttribute>();

            if(attribute == null) continue;

            if (attribute.IsActivatedSingleton)
            {
                Console.Write($"'{type.Name} (Activated Singleton)' ");
                services.AddActivatedSingleton(type);
            }
            else
            {
                switch (attribute.Lifetime)
                {
                    case ServiceLifetime.Transient:
                        Console.Write($"'{type.Name} (Transient)' ");
                        services.AddTransient(type);
                        break;
                    case ServiceLifetime.Scoped:
                        Console.Write($"'{type.Name} (Scoped)' ");
                        services.AddScoped(type);
                        break;
                    default:
                        Console.Write($"'{type.Name} (Singleton)' ");
                        services.AddSingleton(type);
                        break;
                }
            }
        }

        Console.WriteLine("Finished add services");

        return services;
    }

}
