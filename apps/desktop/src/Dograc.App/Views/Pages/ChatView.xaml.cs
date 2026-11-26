using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dograc.App.ViewModels;

namespace Dograc.App.Views.Pages;

public partial class ChatView : UserControl
{
    private ChatViewModel? _attachedVm;
    private bool _isImeComposing;

    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Track Korean/CJK IME composition state so Enter during Hangul composition never submits prematurely.
        TextCompositionManager.AddPreviewTextInputStartHandler(ChatInputBox, OnImeCompositionStart);
        TextCompositionManager.AddPreviewTextInputHandler(ChatInputBox, OnImeCompositionEnd);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(ChatInputBox, OnImeCompositionUpdate);
    }

    private void OnImeCompositionStart(object sender, TextCompositionEventArgs e)
    {
        _isImeComposing = true;
    }

    private void OnImeCompositionUpdate(object sender, TextCompositionEventArgs e)
    {
        _isImeComposing = !string.IsNullOrEmpty(e.TextComposition?.CompositionText);
    }

    private void OnImeCompositionEnd(object sender, TextCompositionEventArgs e)
    {
        _isImeComposing = false;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_attachedVm is not null)
        {
            _attachedVm.MessagesUpdated -= OnMessagesUpdated;
            _attachedVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _attachedVm = DataContext as ChatViewModel;
        if (_attachedVm is not null)
        {
            _attachedVm.MessagesUpdated += OnMessagesUpdated;
            _attachedVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatViewModel.ActiveError) && _attachedVm?.ActiveError is not null)
        {
            Dispatcher.InvokeAsync(() => MessagesScrollViewer.ScrollToEnd());
        }
    }

    private void OnMessagesUpdated(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() => MessagesScrollViewer.ScrollToEnd());
    }

    private void OnChatInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ignore IME-processed keys (e.g., Hangul syllable composition commit) to prevent accidental send.
        if (e.Key == Key.ImeProcessed || _isImeComposing)
        {
            return;
        }

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
