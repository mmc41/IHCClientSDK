using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that can swap its whole contents in ONE notification.
/// <para>Plain <c>ObservableCollection</c> raises a change per <c>Add</c>, and every one of those costs a UI
/// update — which both official Avalonia sources name as the thing to avoid on a bulk update: <i>"replacing the
/// entire collection is significantly faster than adding items individually"</i> (performance review BP-22 /
/// architecture AP-20). The documented remedy is to assign a new collection, but a get-only property that XAML
/// binds once cannot be reassigned, so the equivalent is done in place here.</para>
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>Replaces every item with <paramref name="items"/>, raising a single <see cref="NotifyCollectionChangedAction.Reset"/>
    /// (plus the two count/indexer property changes a Reset carries) however many items are involved.</summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (T item in items)
            Items.Add(item);

        // Raised by hand because the base class only notifies from its own mutating members, which is exactly what
        // was bypassed above by writing through Items.
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
