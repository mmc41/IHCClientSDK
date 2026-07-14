using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal enumerator dialog (US-030): names an enum type and lists its ordered states (one per line). Creating a
/// new type leaves the name editable; editing an existing type shows it read-only and lets states be appended.
/// Returns the edited <see cref="EnumDefinitionResult"/>, or null on Cancel.
/// </summary>
public partial class EnumDefinitionWindow : ResultDialog<EnumDefinitionResult>
{
    public EnumDefinitionWindow()
    {
        InitializeComponent();
    }

    public static Task<EnumDefinitionResult?> ShowAsync(Window owner, EnumDefinitionInput input)
    {
        var window = new EnumDefinitionWindow { Title = input.Title };
        window.TypeNameBox.Text = input.TypeName;
        window.TypeNameBox.IsReadOnly = !input.IsNew;   // an existing type keeps its name; only states are appended
        window.StatesBox.Text = string.Join(Environment.NewLine, input.States);
        return window.ShowDialogForResult(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        List<string> states = (StatesBox.Text ?? string.Empty)
            .Split('\n')
            .Select(s => s.Trim('\r', ' ', '\t'))
            .Where(s => s.Length > 0)
            .ToList();
        Accept(new EnumDefinitionResult((TypeNameBox.Text ?? string.Empty).Trim(), states));
    }
}
