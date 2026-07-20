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
        window.ProjDescriptionBox.Text = current.Description;
        window.ProjNumberBox.Text = current.Number;
        window.ProjProgrammerBox.Text = current.Programmer;
        Fill(current.Customer, window.CustNameBox, window.CustAddressBox, window.CustCityBox, window.CustZipBox,
            window.CustCountryBox, window.CustPhoneBox, window.CustMobileBox, window.CustEmailBox);
        Fill(current.Installer, window.InstNameBox, window.InstAddressBox, window.InstCityBox, window.InstZipBox,
            window.InstCountryBox, window.InstPhoneBox, window.InstMobileBox, window.InstEmailBox);
        return window.ShowDialogForResult(owner);
    }

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

    private void OnOk(object? sender, RoutedEventArgs e) =>
        Accept(new ProjectInfoData(
            Val(ProjDescriptionBox), Val(ProjNumberBox), Val(ProjProgrammerBox),
            Read(CustNameBox, CustAddressBox, CustCityBox, CustZipBox,
                CustCountryBox, CustPhoneBox, CustMobileBox, CustEmailBox),
            Read(InstNameBox, InstAddressBox, InstCityBox, InstZipBox,
                InstCountryBox, InstPhoneBox, InstMobileBox, InstEmailBox)));
}
