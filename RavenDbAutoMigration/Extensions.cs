namespace RavenDbAutoMigration;

public static class Extensions
{
    public static bool IsGenericList(this Type type)
    {
        return (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(List<>)));
    }

    public static bool HasValue(this string? value, bool ignoreWhiteSpace = false)
    {
        return !(ignoreWhiteSpace ? string.IsNullOrWhiteSpace(value) : string.IsNullOrEmpty(value));
    }

    public static bool IsNullOrEmpty<T>(this IEnumerable<T> enumerable)
    {
        return enumerable == null || !enumerable.Any();
    }
}
