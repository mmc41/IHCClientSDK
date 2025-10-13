using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;

namespace ihc_lab;

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
                return textBox.Text;
            else if (control is NumericUpDown numericUpDown)
                return (int)(numericUpDown.Value ?? 0);
            else if (control is StackPanel stackPanel)
            {
                var radioButtonTrue = stackPanel.Children.OfType<RadioButton>().FirstOrDefault(rb => rb.Content?.ToString() == "True");
                return radioButtonTrue?.IsChecked ?? false;
            }

            return null;
        }
        set
        { 
            var control = GetControl();
            if (control == null)
                return;

            string typeLower = typeForControl.ToLower();
            if (control is TextBox textBox)
               textBox.Text = value?.ToString() ?? "";
            else if (control is NumericUpDown numericUpDown)
            {
                if (value is int intValue)
                    numericUpDown.Value = intValue;
                else if (int.TryParse(value?.ToString(), out int parsedValue))
                    numericUpDown.Value = parsedValue;
            } else if (control is StackPanel stackPanel) {
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
            parentPanel.Children.Add(textBox);
        }
        else if (typeLower == "integer")
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
            parentPanel.Children.Add(numericUpDown);
        }
        else if (typeLower == "bool")
        {
            // Create a horizontal StackPanel to hold the radio buttons
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Name = "ValueCtrl"
            };

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
    }
}