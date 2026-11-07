using System.Reflection;

namespace Trail.Api.Infrastructure;

/// <summary>
/// A feature's HTTP surface. Implementations are discovered at startup and mapped under <c>/api/v1</c>;
/// each one creates its own sub-group, e.g. <c>app.MapGroup("sites").WithTags("Sites")</c>.
/// </summary>
public interface IEndpointGroup
{
    void Map(IEndpointRouteBuilder app);
}

internal static class EndpointGroupExtensions
{
    public static IEndpointRouteBuilder MapEndpointGroups(this IEndpointRouteBuilder app, Assembly assembly)
    {
        var api = app.MapGroup("/api/v1");

        var groups = assembly.DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IEndpointGroup).IsAssignableFrom(t))
            .Select(t => (IEndpointGroup)Activator.CreateInstance(t)!);

        foreach (var group in groups)
            group.Map(api);

        return app;
    }
}
