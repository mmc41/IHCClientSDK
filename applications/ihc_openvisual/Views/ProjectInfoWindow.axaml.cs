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

    public static Task<ProjectInfoData?> ShowAsync(Window owner, ProjectInfoData current,
        ProjectInfoSuggestions suggestions)
    {
        var window = new ProjectInfoWindow();
        window.Offer(suggestions);
        window.Populate(current);
        window.FocusOnOpen(window.ProjNumberBox);   // the first field, selected and ready to overtype, as in every other editor dialog
        return window.ShowDialogForResult(owner);
    }

    /// <summary>Fills each contact field's drop-down from the data tables (US-049), which is what makes these
    /// fields the vendor's editable combos rather than plain boxes. Called BEFORE <see cref="Populate"/>: setting
    /// ItemsSource does not disturb an already-set Text, but doing it in this order keeps the two independent.</summary>
    internal void Offer(ProjectInfoSuggestions suggestions)
    {
        InstNameBox.ItemsSource = suggestions.InstallerNames;
        CustNameBox.ItemsSource = suggestions.CustomerNames;
        foreach (AutoCompleteBox box in new[] { InstAddressBox, CustAddressBox }) box.ItemsSource = suggestions.Streets;
        foreach (AutoCompleteBox box in new[] { InstPhoneBox, CustPhoneBox }) box.ItemsSource = suggestions.Phones;
        foreach (AutoCompleteBox box in new[] { InstZipBox, CustZipBox }) box.ItemsSource = suggestions.Zips;
        foreach (AutoCompleteBox box in new[] { InstMobileBox, CustMobileBox }) box.ItemsSource = suggestions.Mobiles;
        foreach (AutoCompleteBox box in new[] { InstCityBox, CustCityBox }) box.ItemsSource = suggestions.Cities;
        foreach (AutoCompleteBox box in new[] { InstEmailBox, CustEmailBox }) box.ItemsSource = suggestions.Emails;
        foreach (AutoCompleteBox box in new[] { InstCountryBox, CustCountryBox }) box.ItemsSource = suggestions.Countries;
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
    private static void Fill(ContactInfo c, AutoCompleteBox name, AutoCompleteBox address, AutoCompleteBox city,
        AutoCompleteBox zip, AutoCompleteBox country, AutoCompleteBox phone, AutoCompleteBox mobile,
        AutoCompleteBox email)
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

    private static ContactInfo Read(AutoCompleteBox name, AutoCompleteBox address, AutoCompleteBox city,
        AutoCompleteBox zip, AutoCompleteBox country, AutoCompleteBox phone, AutoCompleteBox mobile,
        AutoCompleteBox email) => new(
        Val(name), Val(address), Val(city), Val(zip), Val(country), Val(phone), Val(mobile), Val(email));

    private static string Val(TextBox box) => box.Text ?? string.Empty;

    private static string Val(AutoCompleteBox box) => box.Text ?? string.Empty;

    private void OnOk(object? sender, RoutedEventArgs e) => Accept(Collect());
}
