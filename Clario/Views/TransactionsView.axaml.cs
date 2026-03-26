using Avalonia.Controls;
using Clario.Messages;
using CommunityToolkit.Mvvm.Messaging;

namespace Clario.Views;

public partial class TransactionsView : UserControl
{
    public TransactionsView()
    {
        InitializeComponent();
        WeakReferenceMessenger.Default.Register<TransactionsScrollToTop>(this, (s, m) =>
        {
            TransactionsScrollViewer.ScrollToHome();
        });
    }
}