using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dograc.App.ViewModels;

namespace Dograc.App.Views.Pages;

public partial class ChatView : UserControl
{
    private ChatViewModel? _attachedVm;

    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_attachedVm is not null)
        {
            _attachedVm.MessagesUpdated -= OnMessagesUpdated;
        }

        _attachedVm = DataContext as ChatViewModel;
        if (_attachedVm is not null)
        {
            _attachedVm.MessagesUpdated += OnMessagesUpdated;
        }
    }

    private void OnMessagesUpdated(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() => MessagesScrollViewer.ScrollToEnd());
    }

    private void OnChatInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            if (DataContext is ChatViewModel vm && vm.SendMessageCommand.CanExecute(null))
            {
                vm.SendMessageCommand.Execute(null);
            }
        }
    }
}
