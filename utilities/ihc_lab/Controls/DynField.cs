using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;

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

    private String typeForControl = "string";

    /// <summary>
    /// Set/Get the type that this control hosts.
    /// Can be one of following: String, bool, int/byte/sbyte/short/long, double, DateTimeOffset, TimeSpan
    /// </summary>
    public String TypeForControl
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

            string typeLower = typeForControl.ToLower();

            if (control is TextBox textBox)
            {
                return textBox.Text;
            } else if (control is ComboBox comboBox)
            {
                return comboBox.SelectedItem;
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

            string typeLower = typeForControl.ToLower();
            if (control is TextBox textBox)
                textBox.Text = value?.ToString()?.Trim() ?? "";
            else if (control is ComboBox comboBox)
            {
                if (value != null && Tag is Ihc.FieldMetaData fieldMetaData && fieldMetaData.Type.IsEnum)
                {
                    // Try to select the enum value in the ComboBox
                    comboBox.SelectedItem = value;
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
        string typeLower = typeForControl.ToLower();
        if (typeLower == "string")
        {
            var textBox = new TextBox
            {
                Name = "ValueCtrl",
                Text = "",
                MinWidth = 100
            };
            ToolTip.SetTip(textBox, $"Enter {typeLower} value");
            parentPanel.Children.Add(textBox);
        } else if (typeLower == "enum")
        {
            var comboBox = new ComboBox
            {
                Name = "ValueCtrl",
                MinWidth = 100
            };

            // Get enum values from Tag if available
            if (Tag is Ihc.FieldMetaData fieldMetaData && fieldMetaData.Type.IsEnum)
            {
                comboBox.ItemsSource = Enum.GetValues(fieldMetaData.Type);
                if (comboBox.ItemsSource is Array enumValues && enumValues.Length > 0)
                {
                    comboBox.SelectedIndex = 0;
                }
            } else throw new Exception("No metdat found for enum type " + typeLower);

            ToolTip.SetTip(comboBox, $"Select {typeLower} value");
            parentPanel.Children.Add(comboBox);

        } else if (typeLower == "integer" || typeLower == "int32" || typeLower == "int64" || typeLower == "int16" || typeLower == "long" || typeLower == "ulong" || typeLower == "byte" || typeLower == "sbyte" || typeLower == "short" || typeLower == "ushort"
                || typeLower == "double" || typeLower == "single" || typeLower == "decimal"
                || typeLower == "timespan")
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
            ToolTip.SetTip(numericUpDown, $"Enter {typeLower} value");
            parentPanel.Children.Add(numericUpDown);
        }
        else if (typeLower == "bool" || typeLower == "boolean")
        {
            // Create a horizontal StackPanel to hold the radio buttons
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Name = "ValueCtrl"
            };

            ToolTip.SetTip(stackPanel, $"Select {typeLower} value");

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
        else if (typeLower == "datetimeoffset")
        {
            var datePicker = new DatePicker
            {
                Name = "ValueCtrl",
                MinWidth = 150,
                SelectedDate = DateTimeOffset.Now
            };

            ToolTip.SetTip(datePicker, $"Select {typeLower} value");
            parentPanel.Children.Add(datePicker);
        }
        else if (typeLower == "resourcevalue")
        {
            // Create a horizontal StackPanel to hold the radio buttons
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Name = "ValueCtrl"
            };

            ToolTip.SetTip(stackPanel, $"Select {typeLower} value");

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
        else throw new Exception("Unsupported type " + typeLower);
    }
}