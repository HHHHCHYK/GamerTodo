using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using HeyeTodo.Client.Infrastructure.Localization;

namespace HeyeTodo.Client.Infrastructure.Localization;

/// <summary>
/// XAML markup extension: <c>{loc:T Key=Auth.Login}</c>.
/// Returns a binding against the <see cref="LocalizationService"/> indexer, so switching the
/// active culture instantly refreshes all bound text.
/// </summary>
public sealed class TExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public TExtension() { }
    public TExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding;
    }
}
