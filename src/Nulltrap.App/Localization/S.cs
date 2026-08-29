using System.Windows.Markup;

using Nulltrap.Core.Localization;

namespace Nulltrap.App.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class SExtension : MarkupExtension
{
    public SExtension()
    {
    }

    public SExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        string.IsNullOrWhiteSpace(Key) ? string.Empty : Strings.Get(Key);
}
