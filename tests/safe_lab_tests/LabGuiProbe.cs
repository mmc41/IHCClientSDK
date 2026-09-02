using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Ihc.App;

namespace Ihc.Tests
{
    /// <summary>
    /// Reads and drives the Lab window's live controls: locating a named control in the visual tree, resolving a
    /// service or operation to its combo-box index, and writing a value into whichever control type is there.
    /// </summary>
    /// <remarks>
    /// Pure probes over Avalonia controls — no assertions and no suite state, so a fixture that uses them keeps
    /// its own arrange visible. Import with <c>using static Ihc.Tests.LabGuiProbe;</c>.
    /// </remarks>
    internal static class LabGuiProbe
    {
        /// <summary>The first descendant (or <paramref name="parent"/> itself) carrying <paramref name="name"/>.</summary>
        public static Control? FindControlByNameRecursive(Control parent, string name)
        {
            if (parent.Name == name)
                return parent;

            if (parent is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is Control childControl)
                    {
                        var found = FindControlByNameRecursive(childControl, name);
                        if (found != null)
                            return found;
                    }
                }
            }

            return null;
        }

        /// <summary>The index of the named service in the services combo box, or -1.</summary>
        public static int FindServiceIndexByName(ComboBox servicesComboBox, string serviceName)
        {
            var items = servicesComboBox.Items.Cast<LabAppService.ServiceItem>().ToArray();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].DisplayName == serviceName)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// The index of the named operation in the operations combo box, or -1. An operation name can be
        /// overloaded, so <paramref name="parameterCount"/> picks between same-named entries when supplied.
        /// </summary>
        public static int FindOperationIndexByName(ComboBox operationsComboBox, string operationName, int? parameterCount = null)
        {
            var items = operationsComboBox.Items.Cast<LabAppService.OperationItem>().ToArray();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].DisplayName == operationName)
                {
                    if (parameterCount.HasValue)
                    {
                        if (items[i].OperationMetadata.Parameters.Length == parameterCount.Value)
                            return i;
                    }
                    else
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// Writes <paramref name="value"/> into <paramref name="control"/> the way a user would, and reports
        /// whether the control was one of the types handled here. A caller with its own fallback — radio-button
        /// groups, say — hangs it off a <c>false</c> return.
        /// </summary>
        public static bool TrySetControlValue(Control control, object? value)
        {
            switch (control)
            {
                case TextBox textBox:
                    textBox.Text = value?.ToString() ?? string.Empty;
                    return true;
                case NumericUpDown numeric:
                    if (value != null)
                        numeric.Value = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    return true;
                case ComboBox combo:
                    combo.SelectedItem = value;
                    return true;
                case DatePicker datePicker:
                    if (value is DateTimeOffset dto)
                        datePicker.SelectedDate = dto;
                    else if (value is DateTime dt)
                        datePicker.SelectedDate = new DateTimeOffset(dt);
                    return true;
                default:
                    return false;
            }
        }
    }
}
