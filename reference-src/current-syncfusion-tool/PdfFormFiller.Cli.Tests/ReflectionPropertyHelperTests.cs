namespace PdfFormFiller.Cli.Tests;

public sealed class ReflectionPropertyHelperTests
{
    [Fact]
    public void FindProperty_PrefersMostDerivedProperty_WhenNameIsHidden()
    {
        var property = ReflectionPropertyHelper.FindProperty(typeof(DerivedPropertyContainer), "SelectedValue");

        Assert.NotNull(property);
        Assert.Equal(typeof(DerivedPropertyContainer), property!.DeclaringType);
        Assert.Equal(typeof(string[]), property.PropertyType);
    }

    [Fact]
    public void GetValue_ReadsMostDerivedProperty_WhenBaseAndDerivedShareName()
    {
        DerivedPropertyContainer target = new()
        {
            SelectedValue = ["A", "B"],
        };

        object? value = ReflectionPropertyHelper.GetValue(target, "SelectedValue");

        Assert.NotNull(value);
        Assert.IsType<string[]>(value);
        Assert.Equal(["A", "B"], (string[])value);
    }

    [Fact]
    public void TrySetValue_WritesMostDerivedProperty_WhenBaseAndDerivedShareName()
    {
        DerivedPropertyContainer target = new();

        bool success = ReflectionPropertyHelper.TrySetValue(target, "SelectedValue", new[] { "X" });

        Assert.True(success);
        Assert.Equal(["X"], target.SelectedValue);
    }

    private class BasePropertyContainer
    {
        public string SelectedValue { get; set; } = string.Empty;
    }

    private sealed class DerivedPropertyContainer : BasePropertyContainer
    {
        public new string[] SelectedValue { get; set; } = [];
    }
}
