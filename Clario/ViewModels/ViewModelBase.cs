using System;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Clario.ViewModels;

public abstract class ViewModelBase : ObservableObject, IDisposable
{
    private readonly System.Collections.Generic.List<Action> _cleanup = new();

    /// <summary>
    /// Subscribes to a CollectionChanged event and registers automatic unsubscription on Dispose.
    /// </summary>
    protected void Track(INotifyCollectionChanged collection, NotifyCollectionChangedEventHandler handler)
    {
        collection.CollectionChanged += handler;
        _cleanup.Add(() => collection.CollectionChanged -= handler);
    }

    /// <summary>
    /// Registers an arbitrary cleanup action to run on Dispose.
    /// </summary>
    protected void OnDispose(Action action) => _cleanup.Add(action);

    protected virtual void DisposeManaged() { }

    public void Dispose()
    {
        DisposeManaged();
        foreach (var action in _cleanup) action();
        _cleanup.Clear();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        GC.SuppressFinalize(this);
    }
}
