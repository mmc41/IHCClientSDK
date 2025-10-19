using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Ihc;

namespace IhcLab;

 

public partial class DynField : UserControl
{
    private WrapPanel? parentPanel;

    public DynField()
    {
        InitializeComponent();
        DataContext = this;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Find the WrapPanel
        parentPanel = this.FindControl<WrapPanel>("parent");

        // Create the initial dynamic control
        CreateDynamicControl();
    }

    private Type typeForControl = typeof(string);

    /// <summary>
    /// Set/Get the type that this control hosts.
    /// Can be one of following: String, bool, int/byte/sbyte/short/long, double, DateTimeOffset, TimeSpan
    /// </summary>
    public Type TypeForControl
    {
        get { return typeForControl; }
        set
        {
            if (typeForControl != value)
            {
                typeForControl = value;
                CreateDynamicControl();
            }
        }
    }

    public object? Value
    {
        get
        {
            var control = GetControl();
            if (control == null)
                return null;

            string typeLower = typeForControl.Name.ToLower();

            if (control is TextBox textBox)
            {
                return textBox.Text;
            }
            else if (control is ComboBox comboBox)
            {
                return (comboBox.SelectedItem as EnumItem)?.Value;
            }
            else if (control is NumericUpDown numericUpDown)
            {
                decimal val = (numericUpDown.Value ?? 0);
                if (typeLower == "timespan")
                    return TimeSpan.FromMilliseconds((long)val);
                else if (typeLower == "double" || typeLower == "single" || typeLower == "decimal")
                    return (double)val;
                else if (typeLower == "long")
                    return (long)val;
                else if (typeLower == "sbyte")
                    return (sbyte)val;
                else if (typeLower == "byte")
                    return (byte)val;
                else return (int)val;
            }
            else if (control is DatePicker datePicker)
            {
                return datePicker.SelectedDate.HasValue ? datePicker.SelectedDate.Value : DateTimeOffset.MinValue;
            }
            else if (control is BinaryFilePicker binaryFilePicker)
            {
                return (BinaryFile)binaryFilePicker;
            }
            else if (control is TextFilePicker textFilePicker)
            {
                return (TextFile)textFilePicker;
            }
            else if (control is StackPanel stackPanel)
            {
                var radioButtonTrue = stackPanel.Children.OfType<RadioButton>().FirstOrDefault(rb => rb.Content?.ToString() == "True");
                return radioButtonTrue?.IsChecked ?? false;
            }
            else throw new Exception("Unexpected control: " + control.GetType().Name);
        }
        set
        {
            var control = GetControl();
            if (control == null)
                return;

            string typeLower = typeForControl.Name.ToLower();

            if (control is BinaryFilePicker binaryFilePicker)
            {
                var val = value as BinaryFile;
                if (val != null)
                {
                    binaryFilePicker.Filename = val.Filename;
                    binaryFilePicker.Data = val.Data;
                }
            }
            else if (control is TextFilePicker textFilePicker)
            {
                var val = value as TextFile;
                if (val != null)
                {
                    textFilePicker.Filename = val.Filename;
                    textFilePicker.Data = val.Data;
                }
            }
            else if (control is TextBox textBox)
                textBox.Text = value?.ToString()?.Trim() ?? "";
            else if (control is ComboBox comboBox)
            {
                if (value != null && Tag is Ihc.FieldMetaData fieldMetaData && fieldMetaData.Type.IsEnum)
                {
                    // Find the EnumItem that matches the value
                    var itemToSelect = comboBox.ItemsSource
                        ?.Cast<EnumItem>()
                        .FirstOrDefault(item => Equals(item.Value, value));

                    if (itemToSelect != null)
                    {
                        comboBox.SelectedItem = itemToSelect;
                    }
                }
            }
            else if (control is NumericUpDown numericUpDown)
            {
                if (value == null)
                {
                    numericUpDown.Value = 0;
                }
                else if (value is long longValue)
                    numericUpDown.Value = longValue;
                else if (value is int intValue)
                    numericUpDown.Value = intValue;
                else if (value is byte byteValue)
                    numericUpDown.Value = byteValue;
                else if (value is sbyte sbyteValue)
                    numericUpDown.Value = sbyteValue;
                else if (value is decimal decimalValue)
                    numericUpDown.Value = decimalValue;
                else if (value is double doubleValue)
                    numericUpDown.Value = (decimal)doubleValue;
                else if (value is float floatValue)
                    numericUpDown.Value = (decimal)floatValue;
                else if (value is TimeSpan timespan)
                {
                    numericUpDown.Value = timespan.Milliseconds;
                }
                else if (int.TryParse(value?.ToString(), out int parsedValue))
                {
                    numericUpDown.Value = parsedValue;
                }
                else throw new Exception("Unexpected type: " + value?.GetType().Name);
            }
            else if (control is DatePicker datePicker)
            {
                if (value is DateTimeOffset dateTimeOffset)
                    datePicker.SelectedDate = dateTimeOffset;
                else if (value is DateTime dateTime)
                    datePicker.SelectedDate = new DateTimeOffset(dateTime, TimeSpan.Zero);
                else if (DateTimeOffset.TryParse(value?.ToString(), out DateTimeOffset parsedDateTimeOffset))
                    datePicker.SelectedDate = parsedDateTimeOffset;
                else if (DateTime.TryParse(value?.ToString(), out DateTime parsedDateTime))
                    datePicker.SelectedDate = new DateTimeOffset(parsedDateTime, TimeSpan.Zero);
            }
            else if (control is StackPanel stackPanel)
            {
                bool boolValue = false;
                if (value is bool b)
                    boolValue = b;
                else if (bool.TryParse(value?.ToString(), out bool parsedBool))
                    boolValue = parsedBool;

                var radioButtonTrue = stackPanel.Children.OfType<RadioButton>().FirstOrDefault(rb => rb.Content?.ToString() == "True");
                var radioButtonFalse = stackPanel.Children.OfType<RadioButton>().FirstOrDefault(rb => rb.Content?.ToString() == "False");

                if (radioButtonTrue != null)
                    radioButtonTrue.IsChecked = boolValue;
                if (radioButtonFalse != null)
                    radioButtonFalse.IsChecked = !boolValue;
            }
        }
    }

    private Control? GetControl()
    {
        if (parentPanel == null)
            return null;

        // Find the control named "ValueCtrl" in the parentPanel's children
        return parentPanel.Children.FirstOrDefault(c => c.Name == "ValueCtrl");
    }

    private void CreateDynamicControl()
    {
        if (parentPanel == null)
            return;

        // Clear existing controls
        parentPanel.Children.Clear();

        // Create control based on type
        string typeNameLower = typeForControl.Name.ToLower();

        Ihc.FieldMetaData? fieldMetaData = Tag as Ihc.FieldMetaData;
        if (fieldMetaData == null)
            throw new Exception("Missing tag with FieldMetaData for type " + typeNameLower);

        if (fieldMetaData.IsFile)
        {
            // Determine file type based on whether it implements BinaryFile or TextFile
            if (typeof(Ihc.TextFile).IsAssignableFrom(typeForControl))
            {
                // Get the static Encoding property from the concrete TextFile type using reflection
                var encodingProperty = typeForControl.GetProperty("Encoding", BindingFlags.Public | BindingFlags.Static);
                var encoding = encodingProperty?.GetValue(null) as System.Text.Encoding;
                if (encoding == null)
                    throw new Exception("Could not lookup Encoding for type " + typeForControl.Name);

                // Create TextFilePicker for text files
                var textFilePicker = new TextFilePicker
                {
                    Name = "ValueCtrl",
                    TextEncoding = encoding,
                    MinWidth = 100
                };
                ToolTip.SetTip(textFilePicker, $"Upload text file");
                parentPanel.Children.Add(textFilePicker);
            }
            else if (typeof(Ihc.BinaryFile).IsAssignableFrom(typeForControl))
            {
                // Create BinaryFilePicker for binary files
                var binaryFilePicker = new BinaryFilePicker
                {
                    Name = "ValueCtrl",
                    MinWidth = 100
                };
                ToolTip.SetTip(binaryFilePicker, $"Upload binary file");
                parentPanel.Children.Add(binaryFilePicker);
            }
            else
            {
                throw new Exception("Unsupported element type for file element " + typeNameLower);
            }
        } else if (typeForControl.IsEnum)
        {
            var comboBox = new ComboBox
            {
                Name = "ValueCtrl",
                MinWidth = 100
            };

            // Get enum values from Tag if available
            if (fieldMetaData!=null && fieldMetaData.Type.IsEnum)
            {
                var enumItems = Enum.GetValues(fieldMetaData.Type)
                    .Cast<object>()
                    .Select(e => new EnumItem(e))
                    .ToArray();

                comboBox.ItemsSource = enumItems;
                comboBox.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(EnumItem.DisplayName));

                if (enumItems.Length > 0)
                {
                    comboBox.SelectedIndex = 0;
                }
            }
            else throw new Exception("No metadata found for enum type " + typeNameLower);

            ToolTip.SetTip(comboBox, $"Select {typeNameLower} value");
            parentPanel.Children.Add(comboBox);

        }
        else if (typeNameLower == "string")
        {
            var textBox = new TextBox
            {
                Name = "ValueCtrl",
                Text = "",
                MinWidth = 100
            };
            ToolTip.SetTip(textBox, $"Enter {typeNameLower} value");
            parentPanel.Children.Add(textBox);
        }
        else if (typeNameLower == "integer" || typeNameLower == "int32" || typeNameLower == "int64" || typeNameLower == "int16" || typeNameLower == "long" || typeNameLower == "ulong" || typeNameLower == "byte" || typeNameLower == "sbyte" || typeNameLower == "short" || typeNameLower == "ushort"
                || typeNameLower == "double" || typeNameLower == "single" || typeNameLower == "decimal"
                || typeNameLower == "timespan")
        {
            var numericUpDown = new NumericUpDown
            {
                Name = "ValueCtrl",
                FormatString = "N0",
                ParsingNumberStyle = System.Globalization.NumberStyles.Integer,
                Increment = 1,
                MinWidth = 50,
                Value = 0
            };
            ToolTip.SetTip(numericUpDown, $"Enter {typeNameLower} value");
            parentPanel.Children.Add(numericUpDown);
        }
        else if (typeNameLower == "bool" || typeNameLower == "boolean")
        {
            // Create a horizontal StackPanel to hold the radio buttons
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Name = "ValueCtrl"
            };

            ToolTip.SetTip(stackPanel, $"Select {typeNameLower} value");

            var radioButtonTrue = new RadioButton
            {
                GroupName = "rButtonGrup",
                Content = "True",
                IsChecked = true
            };

            var radioButtonFalse = new RadioButton
            {
                GroupName = "rButtonGrup",
                Content = "False",
                IsChecked = false
            };

            stackPanel.Children.Add(radioButtonTrue);
            stackPanel.Children.Add(radioButtonFalse);

            parentPanel.Children.Add(stackPanel);
        }
        else if (typeNameLower == "datetimeoffset")
        {
            var datePicker = new DatePicker
            {
                Name = "ValueCtrl",
                MinWidth = 150,
                SelectedDate = DateTimeOffset.Now
            };

            ToolTip.SetTip(datePicker, $"Select {typeNameLower} value");
            parentPanel.Children.Add(datePicker);
        }
        else if (typeNameLower == "resourcevalue")
        {
            // Create a horizontal StackPanel to hold the radio buttons
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Name = "ValueCtrl"
            };

            ToolTip.SetTip(stackPanel, $"Select {typeNameLower} value");

            var resourceIdUpDown = new NumericUpDown
            {
                Name = "ValueCtrl.ResourceID",
                FormatString = "N0",
                ParsingNumberStyle = System.Globalization.NumberStyles.Integer,
                Increment = 1,
                MinWidth = 50,
                Value = 0
            };

            var valueKindDropDown = new ComboBox
            {
                Name = "ValueCtrl.ValueKind",
                ItemsSource = System.Enum.GetNames(typeof(Ihc.ResourceValue.ValueKind))
            };

            stackPanel.Children.Add(resourceIdUpDown);
            stackPanel.Children.Add(valueKindDropDown);

            parentPanel.Children.Add(stackPanel);
        }
        else throw new Exception("Unsupported type " + typeNameLower);
    }

    /// <summary>
    /// Helper class to wrap enum values with display names for ComboBox binding.
    /// </summary>
    private class EnumItem
    {
        public object Value { get; }
        public string DisplayName { get; }

        public EnumItem(object enumValue)
        {
            Value = enumValue;
            DisplayName = enumValue.ToString() ?? string.Empty;
        }
    }
}