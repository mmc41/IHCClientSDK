using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IhcLab.ViewModels
{
    /// <summary>
    /// Base class for all View Models implementing the MVVM (Model-View-ViewModel) pattern in Avalonia.
    ///
    /// <para><b>MVVM Pattern Overview for Senior Developers:</b></para>
    /// <para>
    /// The MVVM pattern separates presentation logic (ViewModel) from UI markup (View) and business logic (Model).
    /// This class provides the foundational infrastructure for the "ViewModel" component, enabling declarative
    /// data binding between UI controls and application state.
    /// </para>
    ///
    /// <para><b>How INotifyPropertyChanged Enables Data Binding:</b></para>
    /// <para>
    /// The <see cref="INotifyPropertyChanged"/> interface is the core mechanism that powers WPF/Avalonia data binding.
    /// When a ViewModel property changes, raising <see cref="PropertyChanged"/> notifies the UI framework to
    /// refresh bound controls. This creates a reactive UI that automatically updates when underlying data changes,
    /// without explicit UI update code.
    /// </para>
    ///
    /// <para><b>Usage Pattern:</b></para>
    /// <code>
    /// public class MyViewModel : ViewModelBase
    /// {
    ///     private string _name = "";
    ///
    ///     // Property automatically notifies UI on changes via SetProperty
    ///     public string Name
    ///     {
    ///         get => _name;
    ///         set => SetProperty(ref _name, value); // Raises PropertyChanged if value differs
    ///     }
    /// }
    /// </code>
    ///
    /// <para><b>Key Benefits:</b></para>
    /// <list type="bullet">
    ///   <item>Automatic UI synchronization - No manual control.Text = value assignments needed</item>
    ///   <item>Testability - ViewModels are plain C# classes, easily unit-testable without UI framework</item>
    ///   <item>Separation of Concerns - UI designer can work on AXAML views while developer works on ViewModels</item>
    ///   <item>Type Safety - Compile-time checking of property bindings when using strongly-typed bindings</item>
    /// </list>
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Event raised when any property value changes. The Avalonia binding system subscribes to this event
        /// to know when to refresh UI controls that are bound to ViewModel properties.
        ///
        /// <para>
        /// <b>Framework Behavior:</b> Avalonia automatically subscribes to this event when you set a ViewModel
        /// as the DataContext of a Window or Control. You rarely need to manually subscribe to this event
        /// unless implementing custom binding logic.
        /// </para>
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Helper method to set a property value and automatically raise <see cref="PropertyChanged"/> if the value differs.
        /// This is the standard pattern for ViewModel property setters.
        ///
        /// <para><b>How It Works:</b></para>
        /// <list type="number">
        ///   <item>Compares new value with current backing field value using <see cref="EqualityComparer{T}"/></item>
        ///   <item>If values are equal, returns false immediately (no notification needed)</item>
        ///   <item>If values differ, updates backing field and raises <see cref="PropertyChanged"/> event</item>
        ///   <item>Returns true to indicate a change occurred</item>
        /// </list>
        ///
        /// <para><b>CallerMemberName Magic:</b></para>
        /// <para>
        /// The <see cref="CallerMemberNameAttribute"/> automatically captures the calling property's name at compile time,
        /// so you don't need to pass "Name" as a string (avoiding magic strings and typos). The compiler fills this in for you.
        /// </para>
        ///
        /// <para><b>Performance Note:</b></para>
        /// <para>
        /// The equality check prevents unnecessary UI updates. If you assign the same value repeatedly, the UI won't be
        /// notified to refresh, avoiding wasted rendering cycles.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The type of the property being set (inferred from parameters).</typeparam>
        /// <param name="field">Reference to the private backing field (passed by ref to allow modification).</param>
        /// <param name="value">The new value to assign.</param>
        /// <param name="propertyName">Property name - automatically filled by compiler via CallerMemberName. Do not pass manually.</param>
        /// <returns>True if the value changed and PropertyChanged was raised; false if value was already equal.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Manually raises the <see cref="PropertyChanged"/> event for a specific property.
        ///
        /// <para><b>When to Use:</b></para>
        /// <list type="bullet">
        ///   <item>When a property's value is computed from other properties (no backing field)</item>
        ///   <item>When you need to force UI refresh without changing the underlying value</item>
        ///   <item>When multiple properties need to be notified after a batch operation</item>
        /// </list>
        ///
        /// <para><b>Example - Computed Property:</b></para>
        /// <code>
        /// private string _firstName = "";
        /// private string _lastName = "";
        ///
        /// public string FirstName
        /// {
        ///     get => _firstName;
        ///     set
        ///     {
        ///         if (SetProperty(ref _firstName, value))
        ///             OnPropertyChanged(nameof(FullName)); // Notify computed property changed
        ///     }
        /// }
        ///
        /// public string LastName
        /// {
        ///     get => _lastName;
        ///     set
        ///     {
        ///         if (SetProperty(ref _lastName, value))
        ///             OnPropertyChanged(nameof(FullName)); // Notify computed property changed
        ///     }
        /// }
        ///
        /// public string FullName => $"{FirstName} {LastName}"; // Computed from other properties
        /// </code>
        /// </summary>
        /// <param name="propertyName">Name of the property that changed - automatically filled by compiler via CallerMemberName when called from property setter.</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
