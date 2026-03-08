using System.Reflection;

namespace PdfFormFiller.Cli;

internal static class ReflectionPropertyHelper
{
    public static PropertyInfo? FindProperty(Type type, string propertyName)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            PropertyInfo? property = current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .FirstOrDefault(p => p.Name == propertyName && p.GetIndexParameters().Length == 0);
            if (property is not null)
            {
                return property;
            }
        }

        return null;
    }

    public static object? GetValue(object target, string propertyName) =>
        FindProperty(target.GetType(), propertyName)?.GetValue(target);

    public static bool TrySetValue(object target, string propertyName, object? value)
    {
        PropertyInfo? property = FindProperty(target.GetType(), propertyName);
        if (property is null || !property.CanWrite)
        {
            return false;
        }

        property.SetValue(target, value);
        return true;
    }
}
