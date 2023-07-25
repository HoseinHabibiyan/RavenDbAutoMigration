using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Session;

namespace RavenDbAutoMigration;
public static class RavenDbAutoMigration
{
    public static void AddRavenDbAutoMigration(this IServiceCollection services)
    {
        using var scope = services.BuildServiceProvider().CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        var types = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type => typeof(IRavenDocument).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract).ToList();

        using var session = store.OpenSession();
        session.Advanced.MaxNumberOfRequestsPerSession = int.MaxValue;
        foreach (var type in types)
        {
            if (!session.Advanced.DocumentQuery<object>(collectionName: $"{type.Name}s").Any())
            {
                var doc = Activator.CreateInstance(type);
                session.Store(doc);
            }

        }
        session.SaveChanges();

        types.ForEach(type => NavigateNestedNodes(session, store, type));
    }


    public static void NavigateNestedNodes(IDocumentSession session, IDocumentStore store, Type docType, Stack<string> nodes = default!, Type? childType = null)
    {
        string docName = $"{docType.Name}s";

        Type type = childType ?? docType;
        var properties = type.GetProperties().Where(x =>
          (x.PropertyType.IsGenericList() && x.PropertyType.GetGenericArguments().Any())
          ||
          (!x.PropertyType.IsGenericList() && !x.PropertyType.Namespace.StartsWith("System"))).ToList();

        string? strNodes = default;
        if (!nodes.IsNullOrEmpty())
        {
            strNodes = string.Join(".", nodes.Reverse());
        }

        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            string nodePath = strNodes.HasValue() ? $"{strNodes}.{property.Name}" : property.Name;

            bool exist = session.Advanced.RawQuery<object>($"from {docName} where (exists({nodePath}) or {nodePath}.length == 0) and {nodePath} != null select {nodePath}").Any();

            if (!exist)
            {
                string value = property.PropertyType.IsGenericList() ? "[]" : "{}";
                store.Operations.Send(new PatchByQueryOperation($"from {docName} update {{ this.{nodePath} = {value} }}"));
            }

            if (!property.PropertyType.GetGenericArguments().Any())
            {
                nodes = nodes ?? new Stack<string>();
                nodes.Push(property.Name);
                NavigateNestedNodes(session, store, docType, nodes, property.PropertyType);
            }

            if (!nodes.IsNullOrEmpty() && i == properties.Count - 1)
            {
                nodes.Pop();
                nodes.Pop();
            }
        }
    }
}
