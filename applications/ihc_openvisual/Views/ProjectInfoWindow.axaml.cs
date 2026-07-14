using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal project-information dialog (US-039): captures project metadata plus the customer and installer contact
/// details that identify the installation in the reports. Returns the edited <see cref="ProjectInfoData"/>, or null
/// on Cancel.
/// </summary>
public partial class ProjectInfoWindow : ResultDialog<ProjectInfoData>
{
    public ProjectInfoWindow()
    {
        InitializeComponent();
    }

    public static Task<ProjectInfoData?> ShowAsync(Window owner, ProjectInfoData current)
    {
        var window = new ProjectInfoWindow();
        window.ProjDescriptionBox.Text = current.Description;
        window.ProjNumberBox.Text = current.Number;
        window.ProjProgrammerBox.Text = current.Programmer;
        window.Fill("Cust", current.Customer);
        window.Fill("Inst", current.Installer);
        return window.ShowDialogForResult(owner);
    }

    private void Fill(string prefix, ContactInfo c)
    {
        Box($"{prefix}NameBox").Text = c.Name;
        Box($"{prefix}AddressBox").Text = c.Address;
        Box($"{prefix}CityBox").Text = c.City;
        Box($"{prefix}ZipBox").Text = c.Zip;
        Box($"{prefix}CountryBox").Text = c.Country;
        Box($"{prefix}PhoneBox").Text = c.Phone;
        Box($"{prefix}MobileBox").Text = c.Mobile;
        Box($"{prefix}EmailBox").Text = c.Email;
    }

    private ContactInfo Read(string prefix) => new(
        Text($"{prefix}NameBox"), Text($"{prefix}AddressBox"), Text($"{prefix}CityBox"), Text($"{prefix}ZipBox"),
        Text($"{prefix}CountryBox"), Text($"{prefix}PhoneBox"), Text($"{prefix}MobileBox"), Text($"{prefix}EmailBox"));

    private TextBox Box(string name) => this.FindControl<TextBox>(name)!;
    private string Text(string name) => Box(name).Text ?? string.Empty;

    private void OnOk(object? sender, RoutedEventArgs e) =>
        Accept(new ProjectInfoData(
            Text("ProjDescriptionBox"), Text("ProjNumberBox"), Text("ProjProgrammerBox"),
            Read("Cust"), Read("Inst")));
}
