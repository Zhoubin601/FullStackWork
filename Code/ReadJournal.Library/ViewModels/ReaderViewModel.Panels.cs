namespace ReadJournal.Library.ViewModels;

public sealed partial class ReaderViewModel
{
    private bool _tocExpanded = true, _appearanceExpanded = true, _aiExpanded = true;
    public bool IsTocExpanded { get => _tocExpanded; set => SetProperty(ref _tocExpanded, value); }
    public bool IsAppearanceExpanded { get => _appearanceExpanded; set => SetProperty(ref _appearanceExpanded, value); }
    public bool IsAiExpanded { get => _aiExpanded; set => SetProperty(ref _aiExpanded, value); }
}
