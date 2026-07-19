using System.Collections.Generic;
using System.Linq;

namespace safe_project_tests;

/// <summary>
/// fablerefac W1-5 (API-D): the pure read projections moved down to the SDK — GetProjectInfo, GetDataTables,
/// GetUnlinkedWirelessProducts and GetModuleAddressMap over a <see cref="Project"/>. Equivalence classes: empty
/// project, populated metadata/contacts, user-texts vs system tables vs plain enums, unlinked wireless
/// present/absent (and a modem, which is not wireless), addressed vs unaddressed terminals.
/// </summary>
public class ProjectProjectionsTests
{
    private static Project Proj(params ProjectElement[] rootChildren) =>
        new(ProjectElement.Create("utcs_project", null, [], rootChildren));

    private static Project ProjWithProducts(params ProjectElement[] products) =>
        Proj(ProjectElement.Create("groups", null, [],
            [ProjectElement.Create("group", null, [], products)]));

    private static ProjectElement El(string tag, params (string Name, string Value)[] attrs) =>
        ProjectElement.Create(tag, null, attrs, []);

    private static ProjectElement El(string tag, (string Name, string Value)[] attrs, params ProjectElement[] children) =>
        ProjectElement.Create(tag, null, attrs, children);

    // ---- GetProjectInfo ----

    [Test]
    public void GetProjectInfo_ReadsMetadataAndContacts()
    {
        Project p = Proj(
            El("project_info", ("description", "D"), ("number", "N"), ("programmer", "P")),
            El("customer_info", ("name", "Cust"), ("address", "Addr"), ("email", "c@x")),
            El("installer_info", ("name", "Inst"), ("phone", "123")));

        ProjectInfoData info = p.GetProjectInfo();

        Assert.Multiple(() =>
        {
            Assert.That(info.Description, Is.EqualTo("D"));
            Assert.That(info.Number, Is.EqualTo("N"));
            Assert.That(info.Programmer, Is.EqualTo("P"));
            Assert.That(info.Customer.Name, Is.EqualTo("Cust"));
            Assert.That(info.Customer.Address, Is.EqualTo("Addr"));
            Assert.That(info.Customer.Email, Is.EqualTo("c@x"));
            Assert.That(info.Customer.Zip, Is.EqualTo(""), "an absent contact field is blank");
            Assert.That(info.Installer.Phone, Is.EqualTo("123"));
        });
    }

    [Test]
    public void GetProjectInfo_EmptyProject_IsAllBlank()
    {
        ProjectInfoData info = Proj().GetProjectInfo();

        Assert.Multiple(() =>
        {
            Assert.That(info.Description, Is.EqualTo(""));
            Assert.That(info.Customer, Is.EqualTo(ContactInfo.Empty));
            Assert.That(info.Installer, Is.EqualTo(ContactInfo.Empty));
        });
    }

    // ---- GetDataTables ----

    [Test]
    public void GetDataTables_ClassifiesUserTexts_SystemTables_AndSkipsPlainEnums()
    {
        ProjectElement Value(int n, string name) =>
            ProjectElement.Create("enum_value", new ElementId(n, 0x48), [("name", name)], []);

        Project p = Proj(El("enum_definitions", [],
            El("enum_definition", [("name", ProjectProjections.UserTextsTableName)], Value(1, "Reminder")),
            El("enum_definition", [("name", "Colors"), ("typeid", "_0x5")], Value(2, "Red"), Value(3, "Green")),
            El("enum_definition", [("name", "PlainEnum")], Value(4, "X"))));   // no typeid → neither system nor texts

        DataTablesModel dt = p.GetDataTables();

        Assert.Multiple(() =>
        {
            Assert.That(dt.UserTexts.Select(u => u.Text), Is.EqualTo(new[] { "Reminder" }));
            Assert.That(dt.SystemTables.Length, Is.EqualTo(1), "the plain (typeid-less) enum is not a system table");
            Assert.That(dt.SystemTables[0].Name, Is.EqualTo("Colors"));
            Assert.That(dt.SystemTables[0].Rows, Is.EqualTo(new[] { "Red", "Green" }));
        });
    }

    [Test]
    public void GetDataTables_EmptyProject_IsEmpty()
    {
        DataTablesModel dt = Proj().GetDataTables();

        Assert.Multiple(() =>
        {
            Assert.That(dt.SystemTables, Is.Empty);
            Assert.That(dt.UserTexts, Is.Empty);
        });
    }

    // ---- GetUnlinkedWirelessProducts ----

    [Test]
    public void GetUnlinkedWirelessProducts_ListsOnlyUnlinkedAirlinkProducts()
    {
        Project p = ProjWithProducts(
            El("product_airlink", ("name", "WL-unlinked")),                        // no serial → unlinked
            El("product_airlink", ("name", "WL-linked"), ("serialnumber", "999")), // commissioned → linked
            El("product_dataline", ("name", "Wired")),                             // wired → never wireless
            El("product_rs485_sms_modem", ("name", "Modem")));                     // modem → not wireless

        IReadOnlyList<string> unlinked = p.GetUnlinkedWirelessProducts();

        Assert.That(unlinked, Is.EqualTo(new[] { "WL-unlinked" }));
    }

    [Test]
    public void GetUnlinkedWirelessProducts_EmptyProject_IsEmpty()
    {
        Assert.That(Proj().GetUnlinkedWirelessProducts(), Is.Empty);
    }

    // ---- GetModuleAddressMap ----

    [Test]
    public void GetModuleAddressMap_MapsAddressedTerminals_SplitAndSortedByAddress()
    {
        DatalineAddress.TryEncode(2, 3, isOutput: true, out string outHi);
        DatalineAddress.TryEncode(1, 1, isOutput: true, out string outLo);
        DatalineAddress.TryEncode(1, 2, isOutput: false, out string inTok);

        Project p = ProjWithProducts(
            El("product_dataline", [("name", "Prod")],
                El("dataline_output", ("name", "OutHi"), ("address_dataline", outHi)),
                El("dataline_output", ("name", "OutLo"), ("address_dataline", outLo)),
                El("dataline_input", ("name", "In"), ("address_dataline", inTok)),
                El("dataline_output", ("name", "Unaddressed"))));   // no address → omitted

        ModuleAddressMap map = p.GetModuleAddressMap();

        Assert.Multiple(() =>
        {
            Assert.That(map.OutputModules.Select(e => e.Terminal), Is.EqualTo(new[] { "OutLo", "OutHi" }), "sorted by address");
            Assert.That(map.OutputModules[0].Address, Is.EqualTo("1.1"));
            Assert.That(map.OutputModules[0].Product, Is.EqualTo("Prod"));
            Assert.That(map.InputModules.Select(e => e.Terminal), Is.EqualTo(new[] { "In" }));
            Assert.That(map.InputModules[0].Address, Is.EqualTo("1.2"));
        });
    }

    [Test]
    public void GetModuleAddressMap_WirelessAndEmpty_ContributeNothing()
    {
        ModuleAddressMap map = ProjWithProducts(El("product_airlink", ("name", "WL"))).GetModuleAddressMap();

        Assert.Multiple(() =>
        {
            Assert.That(map.InputModules, Is.Empty);
            Assert.That(map.OutputModules, Is.Empty);
        });
    }
}
