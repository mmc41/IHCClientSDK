using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ihc.Vis;
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
        window.Populate(current);
        return window.ShowDialogForResult(owner);
    }

    /// <summary>Shows <paramref name="current"/> in the fields. Paired with <see cref="Collect"/>: the two are the
    /// dialog's whole read/write surface, so a field that is displayed but not returned is a test-visible bug.</summary>
    internal void Populate(ProjectInfoData current)
    {
        ProjNumberBox.Text = current.Number;
        ProjTypeBox.Text = current.Type;
        ProjProgrammerBox.Text = current.Programmer;
        ProjDrawingBox.Text = current.Drawing;
        ProjDescriptionBox.Text = current.Description;
        Fill(current.Customer, CustNameBox, CustAddressBox, CustCityBox, CustZipBox,
            CustCountryBox, CustPhoneBox, CustMobileBox, CustEmailBox);
        Fill(current.Installer, InstNameBox, InstAddressBox, InstCityBox, InstZipBox,
            InstCountryBox, InstPhoneBox, InstMobileBox, InstEmailBox);
    }

    /// <summary>Reads the fields back — what OK returns.</summary>
    internal ProjectInfoData Collect() => new(
        Val(ProjDescriptionBox), Val(ProjNumberBox), Val(ProjProgrammerBox),
        Val(ProjTypeBox), Val(ProjDrawingBox),
        Read(CustNameBox, CustAddressBox, CustCityBox, CustZipBox,
            CustCountryBox, CustPhoneBox, CustMobileBox, CustEmailBox),
        Read(InstNameBox, InstAddressBox, InstCityBox, InstZipBox,
            InstCountryBox, InstPhoneBox, InstMobileBox, InstEmailBox));

    // The contact grids share a fixed 8-field layout; the generated x:Name fields are passed in positionally so the
    // Cust*/Inst* boxes are addressed directly (no runtime string FindControl, so a renamed control is a compile error).
    private static void Fill(ContactInfo c, TextBox name, TextBox address, TextBox city, TextBox zip,
        TextBox country, TextBox phone, TextBox mobile, TextBox email)
    {
        name.Text = c.Name;
        address.Text = c.Address;
        city.Text = c.City;
        zip.Text = c.Zip;
        country.Text = c.Country;
        phone.Text = c.Phone;
        mobile.Text = c.Mobile;
        email.Text = c.Email;
    }

    private static ContactInfo Read(TextBox name, TextBox address, TextBox city, TextBox zip,
        TextBox country, TextBox phone, TextBox mobile, TextBox email) => new(
        Val(name), Val(address), Val(city), Val(zip), Val(country), Val(phone), Val(mobile), Val(email));

    private static string Val(TextBox box) => box.Text ?? string.Empty;

    private void OnOk(object? sender, RoutedEventArgs e) => Accept(Collect());
}
