using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class RoleSelectionViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly INavigationService _navigation;

    public ObservableCollection<RoleOption> Options { get; } = new()
    {
        new RoleOption(RoleType.Producer,      "Roles.Producer"),
        new RoleOption(RoleType.Designer,      "Roles.Designer"),
        new RoleOption(RoleType.Artist,        "Roles.Artist"),
        new RoleOption(RoleType.Programmer,    "Roles.Programmer"),
        new RoleOption(RoleType.SoundDesigner, "Roles.SoundDesigner"),
    };

    public RoleSelectionViewModel(AppSettings settings, INavigationService navigation)
    {
        _settings = settings;
        _navigation = navigation;
        // Pre-check options that are already saved.
        foreach (var o in Options)
        {
            o.IsSelected = ((RoleType)_settings.Roles).HasFlag(o.Value);
        }
    }

    [RelayCommand]
    private void Continue()
    {
        RoleType combined = RoleType.None;
        foreach (var o in Options)
            if (o.IsSelected) combined |= o.Value;

        _settings.Roles = (int)combined;
        SettingsStore.Save(_settings);

        _navigation.NavigateTo<ShellViewModel>();
    }

    [RelayCommand]
    private void Skip()
    {
        // User skipped; roles remain None. Can be edited later from Settings.
        _navigation.NavigateTo<ShellViewModel>();
    }
}

public sealed partial class RoleOption : ObservableObject
{
    public RoleType Value { get; }
    public string LabelKey { get; }

    [ObservableProperty] private bool _isSelected;

    public RoleOption(RoleType value, string labelKey)
    {
        Value = value;
        LabelKey = labelKey;
    }
}
