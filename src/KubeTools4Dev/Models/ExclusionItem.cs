using CommunityToolkit.Mvvm.ComponentModel;

namespace KubeTools4Dev.Models;

/// <summary>
/// Exclusion item wrapper.
/// </summary>
public partial class ExclusionItem(string value) : ObservableObject
{
    [ObservableProperty]
    private string _value = value;
}
