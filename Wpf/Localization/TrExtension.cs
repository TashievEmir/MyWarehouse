using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Localization;

/// <summary>
/// Разметочное расширение <c>{loc:Tr Ключ}</c>. Разворачивается в привязку к
/// индексатору <see cref="Loc"/>, поэтому текст меняется прямо на экране,
/// как только переключили язык.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
