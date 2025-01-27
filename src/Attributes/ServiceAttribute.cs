using Microsoft.Extensions.DependencyInjection;

namespace App.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ServiceAttribute : Attribute
{
    public ServiceLifetime Lifetime { get; }
    public bool IsActivatedSingleton { get; }

    public ServiceAttribute(ServiceLifetime lifetime)
    {
        Lifetime = lifetime;
    }

    public ServiceAttribute()
    {
        IsActivatedSingleton = true;
    }
}
