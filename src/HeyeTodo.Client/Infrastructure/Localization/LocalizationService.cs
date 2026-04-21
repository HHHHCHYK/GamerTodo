using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;

namespace HeyeTodo.Client.Infrastructure.Localization;

/// <summary>
/// Runtime-switchable localization backed by <c>Strings.resx</c> plus culture variants.
/// Consume strings via <c>LocalizationService.Instance["Key"]</c> or the <c>{loc:T Key=...}</c>
/// markup extension (see <see cref="TranslateExtension"/>).
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    private static readonly ResourceManager Manager = new(
        "HeyeTodo.Client.Resources.Strings",
        typeof(LocalizationService).Assembly);

    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            if (Equals(_culture, value)) return;
            _culture = value;
            Thread.CurrentThread.CurrentUICulture = value;
            CultureInfo.DefaultThreadCurrentUICulture = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        }
    }

    /// <summary>Indexer access so bindings can use <c>{Binding [Key], Source={x:Static loc:LocalizationService.Instance}}</c>.</summary>
    public string this[string key] =>
        Manager.GetString(key, _culture) ?? key;

    public string Get([CallerMemberName] string key = "") => this[key];

    /// <summary>
    /// Detect system UI language and pick a supported culture: zh-* → zh, everything else → en.
    /// </summary>
    public static CultureInfo DetectSystemCulture()
    {
        var name = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return name.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? new CultureInfo("zh")
            : new CultureInfo("en");
    }
}
