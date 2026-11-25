using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dograc.Core.Theme;

namespace Dograc.App.ViewModels;

/// <summary>
/// Base class for top-level navigation page ViewModels.
/// </summary>
public abstract partial class PageViewModelBase : ObservableObject
{
    public abstract string PageTitle { get; }

    public abstract string PageDescription { get; }
}

public sealed class ModelsViewModel : PageViewModelBase
{
    public override string PageTitle => "Models";
    public override string PageDescription => "로컬 Ollama 모델 및 생성 엔드포인트를 구성하고 연결을 테스트합니다.";
}

public sealed partial class SettingsViewModel : PageViewModelBase
{
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private AppThemeMode _selectedThemeMode;

    public SettingsViewModel(IThemeService themeService)
    {
        _themeService = themeService;
        _selectedThemeMode = themeService.CurrentMode;
    }

    public override string PageTitle => "Settings";
    public override string PageDescription => "테마, 백엔드 연결 주소 및 애플리케이션 업데이트를 설정합니다.";

    public IReadOnlyList<AppThemeMode> ThemeModes { get; } =
    [
        AppThemeMode.System,
        AppThemeMode.Light,
        AppThemeMode.Dark
    ];

    partial void OnSelectedThemeModeChanged(AppThemeMode value)
    {
        _themeService.ApplyTheme(value);
    }

    [RelayCommand]
    private void SetTheme(AppThemeMode mode)
    {
        SelectedThemeMode = mode;
    }
}
