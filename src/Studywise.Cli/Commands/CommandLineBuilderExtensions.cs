using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;
using Studywise.Cli.Configuration;

namespace Studywise.Cli.Commands;

/// <summary>
/// Extensions for wiring an <see cref="IServiceProvider"/> into a System.CommandLine pipeline.
/// </summary>
public static class CommandLineBuilderExtensions
{
    /// <summary>
    /// Registers an <see cref="IServiceProvider"/> and the services it contains
    /// with the command-line binding context, so handlers can be resolved via DI.
    /// </summary>
    /// <param name="builder">The command line builder to extend.</param>
    /// <param name="serviceProvider">The DI container handlers will resolve from.</param>
    /// <param name="services">
    /// Optional service collection. When provided, every registered service and
    /// implementation type is added to the binding context so handler constructors
    /// can request them.
    /// </param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static CommandLineBuilder UseDependencyInjection(
        this CommandLineBuilder builder,
        IServiceProvider serviceProvider,
        ServiceCollection? services = null)
    {
        return builder.AddMiddleware(async (context, next) =>
        {
            var bindingContext = context.BindingContext;
            bindingContext.AddService<IServiceProvider>(_ => serviceProvider);

            var registeredTypes = services != null ? GetRegisteredServiceTypes(services) : [];
            foreach (var serviceType in registeredTypes)
            {
                bindingContext.AddService(
                    serviceType,
                    _ => serviceProvider.GetRequiredService(serviceType));
            }

            await next(context);
        });
    }

    private static Type[] GetRegisteredServiceTypes(ServiceCollection services)
    {
        var types = new List<Type>();

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType.IsGenericTypeDefinition)
            {
                var implementations = services.Where(d =>
                    d.ServiceType.IsGenericType &&
                    d.ServiceType.GetGenericTypeDefinition() == descriptor.ServiceType);
                foreach (var impl in implementations)
                {
                    if (!types.Contains(impl.ServiceType))
                        types.Add(impl.ServiceType);
                }
            }
            else if (!types.Contains(descriptor.ServiceType))
            {
                types.Add(descriptor.ServiceType);
            }

            if (descriptor.ImplementationType != null &&
                !descriptor.ImplementationType.IsGenericTypeDefinition &&
                !types.Contains(descriptor.ImplementationType))
            {
                types.Add(descriptor.ImplementationType);
            }
        }

        return [.. types];
    }
}
