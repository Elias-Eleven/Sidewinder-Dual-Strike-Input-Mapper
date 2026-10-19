using CommunityToolkit.Mvvm.ComponentModel;
using DualStrikeMapper.Core.Controller;

namespace DualStrikeMapper.App.ViewModels;

/// <summary>One labelled button chip around the controller drawing.</summary>
public partial class CalloutViewModel : ViewModelBase
{
    public CalloutViewModel(ControlId id)
    {
        Id = id;
        Chip = ControlNames.Short(id);
        ControlName = ControlNames.Get(id);
    }

    public ControlId Id { get; }
    public string Chip { get; }
    public string ControlName { get; }

    [ObservableProperty]
    public partial string ActionText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsPressed { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
