using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dograc.Core.Errors;

namespace Dograc.App.Controls;

public partial class ErrorCardControl : UserControl
{
    public static readonly DependencyProperty ErrorProperty =
        DependencyProperty.Register(nameof(Error), typeof(ApplicationError), typeof(ErrorCardControl), new PropertyMetadata(null));

    public static readonly DependencyProperty ActionCommandProperty =
        DependencyProperty.Register(nameof(ActionCommand), typeof(ICommand), typeof(ErrorCardControl), new PropertyMetadata(null));

    public static readonly DependencyProperty DismissCommandProperty =
        DependencyProperty.Register(nameof(DismissCommand), typeof(ICommand), typeof(ErrorCardControl), new PropertyMetadata(null));

    public static readonly DependencyProperty ShowDismissProperty =
        DependencyProperty.Register(nameof(ShowDismiss), typeof(bool), typeof(ErrorCardControl), new PropertyMetadata(true));

    public ErrorCardControl()
    {
        InitializeComponent();
    }

    public ApplicationError? Error
    {
        get => (ApplicationError?)GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public ICommand? DismissCommand
    {
        get => (ICommand?)GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }

    public bool ShowDismiss
    {
        get => (bool)GetValue(ShowDismissProperty);
        set => SetValue(ShowDismissProperty, value);
    }
}
