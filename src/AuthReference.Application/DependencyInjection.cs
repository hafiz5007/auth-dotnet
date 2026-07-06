using System.Reflection;
using AuthReference.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AuthReference.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every MediatR handler, every FluentValidation validator, and the
    /// validation pipeline behavior. The API and Infrastructure layers only need
    /// this one call to wire the whole application layer.
    /// </summary>
    public static IServiceCollection AddAuthReferenceApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
