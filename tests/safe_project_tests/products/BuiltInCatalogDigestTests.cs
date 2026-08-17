#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Ihc.Vis.Catalog;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Reference-free integrity gate for the code-authored <see cref="BuiltInCatalog"/> definitions: each one is
    /// encoded canonically and SHA-256'd against a digest recorded below. Unlike the reference-catalog differential
    /// — which <c>Assert.Ignore</c>s without an IHC Visual install, and so is skipped in CI — this always runs, so a
    /// manual edit of the definition sources cannot silently change what they produce.
    /// <para><b>What is hashed: serialized output plus catalog metadata.</b> The on-disk <c>.def</c>/<c>.ifb</c>
    /// bytes <see cref="CatalogFileWriter"/> emits — body structure, attribute values and order, id tokens, the
    /// DOCTYPE/inline-DTD header rendered from the structured grammar, and the file's own byte encoding — plus the
    /// identity, display name and category path the vendor install supplies.
    /// <see cref="DefinitionDocumentation"/> is deliberately <b>excluded</b>: it is never written into a
    /// <c>.vis</c>, <c>.def</c> or <c>.ifb</c>, and the vendor files it would be compared against carry no help text
    /// at all. <see cref="DefinitionSurfaceIsFullyHashed"/> fails if a member is added to either definition record
    /// without a decision being taken here.</para>
    /// <para><b>What this does NOT certify.</b> The recorded digests were taken from a revision that also passed the
    /// reference-catalog differential, but that differential is a <i>semantic</i> comparison — it renumbers ids and
    /// drops DTD-default attributes before comparing. So these digests pin the SDK's exact current output; they do
    /// not prove those exact bytes equal the vendor's bytes. This gate detects <i>change</i>, not <i>wrongness</i>;
    /// the differential remains the authority on whether the catalog is right.</para>
    /// <para><b>Recording</b> is a one-off performed at bootstrap, from a tree where the differential is green; see
    /// <see cref="Record"/>. Rebaselining after a deliberate catalog change is out of scope by owner ruling — the
    /// <c>[Explicit]</c> recorder ships anyway so whoever eventually needs it has the means.</para>
    /// <para><b>Identity is the key, never the position.</b> Rows are keyed and compared by
    /// <c>(CategoryPath, DisplayName)</c>, and catalog list order is pinned separately over those same keys, so
    /// nothing depends on where a row sits in the table. <see cref="AssertCatalog"/> has the why.</para>
    /// </summary>
    public class BuiltInCatalogDigestTests
    {
        /// <summary>
        /// The compared unit. <c>(CategoryPath, Name)</c> is the key — a typed pair, never a concatenation, so
        /// there is no separator to be injective about. Both fields are inside the hashed tail, so a row's key
        /// cannot silently disagree with its own digest.
        /// <para><c>LookupKey</c> is the lookup key the definition publishes: <c>product_identifier</c> for a
        /// product, empty for a function block, whose identity is already carried in <c>Name</c>. It is recorded
        /// because it is what makes the eight shared product identifiers legible in the table.</para>
        /// </summary>
        private readonly record struct DigestRow(string CategoryPath, string Name, string LookupKey, string Digest);

        // Sections are source layout only — the compared value is the flat row list they splice into. The two
        // overloads are selected by tuple arity, so writing a product row as a 2-tuple by mistake is caught
        // rather than silent: it records an empty LookupKey where the catalog produces a real identifier, and
        // fails the content assertion BY NAME.
        private static DigestRow[] Section(string categoryPath,
            params (string Name, string Identifier, string Digest)[] rows) =>
            [.. rows.Select(r => new DigestRow(categoryPath, r.Name, r.Identifier, r.Digest))];

        private static DigestRow[] Section(string categoryPath, params (string Name, string Digest)[] rows) =>
            [.. rows.Select(r => new DigestRow(categoryPath, r.Name, string.Empty, r.Digest))];

        // 100 products, one section per catalog folder, in registration order.
        private static readonly DigestRow[] ProductDigests =
        [
            .. Section("Bus Produkter",
                ("SMS Modem",                "_0x3103",     "A88CB14272647E8866252005680BE6095D6A5F730A02EDE4EC969EB90DFA44DC"),
                ("IHC LED Dimmer 2 kanaler", "_0x4409",     "E633A97CB59F0F545E0A66CDEE739997FB133A5ACFB3DD86529752A5886AF2CE")),
            .. Section("Datalinie produkter\\01#Input\\01#LK FUGA",
                ("LK FUGA Tryk 2 tast",                    "_0x2101",     "DDE4A0FDEC33A3F0B04083321D119F559A3C08AE7DB47E0433F990A24901D626"),
                ("LK FUGA Tryk 4 tast",                    "_0x2102",     "56B43714DA498EE1576F4F617C8DB94AEE2C022076BC2EDD94DE4526C579E18B"),
                ("LK FUGA Tryk 6 tast",                    "_0x2103",     "8E179389D4BC6BA49DCCC235CF9BDBE2AFE846BA35AA6E9E2CC2E1A0AB3597A7"),
                ("LK FUGA Tryk 2 tast 1 diode",            "_0x2104",     "0AB2BE60F061DBC39576724A7EDADCAB62C855A3E20D486B364DF587F13F5176"),
                ("LK FUGA Tryk 4 tast 2 dioder",           "_0x2105",     "C36F8433CFFA00902DB15C1AAB50926D83F61C198ACF015D15587727BDA9756E"),
                ("LK FUGA Tryk 6 tast 3 dioder",           "_0x2107",     "9EC2AC40EE1F7BCB3BDE44BA7B1B958EE0E314C9EB295C3CEAE4569377A5C3A8"),
                ("LK FUGA Statustryk 4 tast 4 dioder",     "_0x2108",     "3DB4E3FF2B6E14E58C2E8DA3BA7039EC74A4A301C24352DC76A98508E1091A53"),
                ("LK FUGA Betjeningstryk 2 tast 4 dioder", "_0x2130",     "439108ADBF72842A65E41D979B5754FB330538E678D454759E849FB13EF4C7CE"),
                ("LK FUGA Betjeningstryk 3 tast 3 dioder", "_0x2132",     "BBAAAE7581FD0640FC7A1AEBA1BB1507FE0BAFBEB8FB0A813BF47ACB8AAFE5A0")),
            .. Section("Datalinie produkter\\01#Input\\02#LK OPUS",
                ("LK OPUS Tryk 4 tast",                    "_0x2102",     "1BC45A5C7D5FA9743D2648ECB00CAE592888B191CD3637C943CC533D37243371"),
                ("LK OPUS Tryk 4 tast 4 dioder",           "_0x2106",     "C2E3769FCC158D6DD061A3B21F91655C8959BFA95D386DF64F9247E76831AAC0"),
                ("LK OPUS Statustryk 4 tast 4 dioder",     "_0x2108",     "244138C45D27457D543342D54B7D2F62668EC297B9A1608F1D3D5871E985986B"),
                ("LK OPUS Betjeningstryk 2 tast 4 dioder", "_0x2131",     "CDD01A47225167218E2B8D79195F1FE7BF442DE4FA621C9ED5359C0FE11747E1")),
            .. Section("Datalinie produkter\\01#Input\\06#IR fjernbetjeninger",
                ("Beolink 4",                   "_0x21000001", "6788DA72814595DF71F71D61F06EB1D339A0BF37C7910348B2DB5A69B8332D57"),
                ("Beolink1000",                 "_0x21000003", "7E1C418198588F3B080034E9F340F06FC50C918753BC5E95B28634EAFDB4B714"),
                ("Beolink5000",                 "_0x21000002", "40CAD9C6A986C8593D1FA3FFE33C77A9BAE93728F31E754603E4BA1B02FA9BFA"),
                ("IR fjernbetjening - 16 Tryk", "_0x210d",     "FE9628CD9357BD67D7AAA70F2C105EEC607D8627BBC6731AAE7D26B2E5DB0235"),
                ("IR fjernbetjening - 8 Tryk",  "_0x211f",     "BD7CBCDE7A98D18A35B8650CF3F835F2909A7D69E41E877255E559F530BFD3E8")),
            .. Section("Datalinie produkter\\01#Input\\07#Mini Modul",
                ("Mini Modul 1 tryk", "_0x104",      "0FB921B90DDB249B52590C877DA28E3E8DBCFF9272B675312A0611460B14F56A"),
                ("Mini Modul 2 tryk", "_0x105",      "1DEB7792F5047BA544358A93695AC7A3F1BC33E73EAFA3506446BCD57368FB0E"),
                ("Mini Modul 3 tryk", "_0x106",      "DBEE0D4F84AC50368AD2A96E001043698151AEB3EBFFC917F3A6AD53AB0A9D0B")),
            .. Section("Datalinie produkter\\01#Input",
                ("Magnetkontaktsæt",                     "_0x2109",     "6E01232DA7D05CF09E85ABA81E7EA69A9FFCC891D35403D6DE7540A6076C013A"),
                ("Røgsensor",                            "_0x210a",     "CB11EE9D6BD352F60B06E1D505AE22AC778CD0BD28DEA222524EA0F28F4E24C5"),
                ("Gassensor",                            "_0x210b",     "5CA43DA4B3E29E83A44A7EA833C418E7C5E8CA66213C040ED490EE98A5F541F7"),
                ("Vandsensor",                           "_0x210c",     "5213D18FDF55BB594E70B5F875683AA3C2F5318EB1AEBCEAA221C66C8D343A6A"),
                ("PIR",                                  "_0x210e",     "15E1EE277F35603AF62CF1735315971166C051C7BA7E01BDDD4A332F44AA1D18"),
                ("PIR alarm",                            "_0x210f",     "A76E450BCA0CC279163BDC9374BB298653E814E3160E76DAB946F04D11A9E1D1"),
                ("PIR med skumringsrelæ",                "_0x210g",     "E19D12A721D2BC01F09D80D9102D1C3C41E1D837E327E1DB4BDE338FFB3C0245"),
                ("Skumringsrelæ",                        "_0x2110",     "3B17F78367525013570A00752464198739E33964B1134D22DE855CB584C9E0AE"),
                ("Kodetastatur",                         "_0x2111",     "803A3686D964FDC3748BF8264E3B56884311157DD2C35DA4B56953BFE9539216"),
                ("Sabotagekreds",                        "_0x2112",     "88E87E009BC13569EC635D6EDE14088CDC2988E81B0E25BEBD98685BFDD978E6"),
                ("Ringetryk",                            "_0x2113",     "63DF234A3421474B78AC160E8A01E35AE0E21D2E23D0AA3AE9A478813E03C1DE"),
                ("Backup modul",                         "_0x2115",     "9B5B92775ADD4A3D5E2DA8076C17A30367161CCC555C2D9190B2FC50C2D4EB0D"),
                ("Temperatur sensor",                    "_0x2124",     "9615BC2FBB0F23E75F986E49BFC0157D122451E06FBA4560B36AB1108BF8494E"),
                ("Temperatur sensor med logning",        "_0x2125",     "E30321B5B0D99959CDE08120BAF7337DD0FD14447ED7DC76F55DBD89077B7A64"),
                ("Fugt / Temperatur sensor",             "_0x2135",     "56D7C116F3BB963A299612A968E4598BFDDD45191FC82AB45A858B199D50F713"),
                ("Lux / Temperatur sensor",              "_0x2136",     "E16DA981DFB706B6D834414B19DF9D049A3BA69E071E10B44188D89EC0B87F4F"),
                ("Fugt / Temperatur sensor med logning", "_0x2138",     "D9ECC4A0B64E554B00F044C0EFEAF842AD40F31FD556CBC7CE2C92E0A860B055"),
                ("Lux / Temperatur sensor med logning",  "_0x2139",     "62462D3207A070B63C0EF91F62B2022B3724A9EE1E2ADF8EB8F200F3B9E30721")),
            .. Section("Datalinie produkter\\02#Output",
                ("Diode",               "_0x107",      "1740877D39658AF2C6E7B9D67B58251923E0C49C247C9865942CE94A18BA87B4"),
                ("Stikkontakt",         "_0x2201",     "4335DE34215923B0B1E28A946E3A5BCCFAE156F1B584438DEE5BC8D2A91FC721"),
                ("Lampeudtag",          "_0x2202",     "45620DB7C12FEE88A99DFB3B448CB9C1454BC792F289BFD7A444AEEFB61F22ED"),
                ("Lydgiver intern",     "_0x2203",     "43D113D3B5FC3B2211ABC16B08AADA2B39E1B555D2C12B2A9D2EED98D56E771D"),
                ("Lydgiver ekstern",    "_0x2204",     "55A2B28F463B879FD6CDC0712639711CD4BF4C3D6A98414BE32469DEAAF0A17B"),
                ("Magnetventil NC",     "_0x2205",     "2F5B65283C5BF565E7CC1C0715488F3C07165956C9949AB0EA41AB38F982257F"),
                ("Magnetventil NO",     "_0x2206",     "F7D55CA0103A0D9EFE6FC4B211D5762AE9DF2E4517017922F9CBC6929B72E2A8"),
                ("Output 1-10V",        "_0x2207",     "3BBE322CEDE2287C89E4571F93A75DE2A5A27A269492D5FDBC37EB7BE762C71B"),
                ("Dørlås",              "_0x2208",     "32F984D6D85873B6243559C7D0FE4BA8D42A4F5BDCD2654C51A9A063BAC24AF6"),
                ("Ringeklokke",         "_0x2209",     "00C52C1DC966C35E9BC061E9689CBE35A3C17B2362EAC65882F3439E7040758F"),
                ("Telestat",            "_0x220a",     "28EDDCEA27E1CEF0501270A3DEFEAFFFE52D8A59AA38D560F99785F38201326A"),
                ("Cirkulationspumpe",   "_0x220b",     "4A64F04A3AD5ED912CF7173418DD6409E325062C7256C084892846BAA71ADAE6"),
                ("Ventilator",          "_0x220c",     "4287863FA2B17F2944571418BECC0E7BAB8AAB1600D4FDE606C49DA6D8B9E5F1"),
                ("Vandvarmer",          "_0x220d",     "D02E9689753B9B0B56F0CB5A1027FAF49E296E9301EF8A3F7168E70945C7CCBA"),
                ("El-radiator",         "_0x220e",     "97206237F22545249EC3CFCAD8097ABD51E4C590FB4DB7F83FB167FFB00B0856"),
                ("El-gulvvarme",        "_0x220f",     "1C1AF497EE9A3FFD3C6D9E281A574961FFAF08189B8721F279B52B59835905A9"),
                ("Output 1-10V IHC/SA", "_0x2302",     "AB264528579954FFF9E383A939EA8ADA478995E3C826D19F52F80FA238AC864A")),
            .. Section("Datalinie produkter\\03#Dimmer",
                ("UniDimmer touch",           "_0x2301",     "C27E27C886370D74D49FB59EB5F05815D78E04EF9A9716F9DD48EC530E61A28C"),
                ("UniDimmer 2-tast betjent",  "_0x2302",     "B82DBBA0D539B9CC47604AC033376E6795986DE0777C0C7AB6F01F3D8E1887B6"),
                ("Dimmer touch",              "_0x2303",     "5B48A5AD9E719587145C1B73864CBFED66C021A6330BABFD8A8E3AD09131A58E"),
                ("Dimmer 2-tast betjent",     "_0x2304",     "589A1FFEA684A1C5BC8389171EB1E694E76570CF4ABB2CB2430F285AD094C9B2"),
                ("Dimmer 350LR/600CR/1000LR", "_0x21000007", "5181503EB06E017324CFDA4EC3BF73D966F6ADDFF73494BA63F85F9808018A20")),
            .. Section("Datalinie produkter\\04#Generelle",
                ("Brugerdefineret indgangsprodukt",             "_0x2701",     "D2E3EDE6085F318D9DA9C57CCED36D07E3FBA87585110790F99F47EB9829FE3D"),
                ("Brugerdefineret udgangsprodukt med scenarie", "_0x2702",     "4692F9FD641EA6D036CB37C54EE806AEB3536E2B34B11EDC5B8EC318687059D4"),
                ("Brugerdefineret udgangsprodukt",              "_0x2703",     "3DDEEE38F7E52954E8C842EF1FEF0AB27734E307E6C0171465131EA87B8C89E6"),
                ("Brugerdefineret indgangsprodukt med logning", "_0x2706",     "5A80240FDA578B93F0E95C7167FB1649006BFCE7BA7575B0CEA0E47280D375FE"),
                ("Brugerdefineret udgangsprodukt med logning",  "_0x2707",     "2C24F670D7C44AA231CA0E19F1EEB5CE8D9C2B62A306FC86714E88156A564BBE")),
            .. Section("LK IHC Wireless produkter\\01#Input",
                ("Tryk 2 tast",    "_0x4101",     "80CB6F027B2EC5FE73933D48C329A18E57C6DF7A7A783518819BD9C0581DF37C"),
                ("Tryk 4 tast",    "_0x4102",     "BE2B7B8506776D3E6BDB2DC19CC674278725273AB0FB7D2F5F0022368921EDA7"),
                ("Tryk 6 tast",    "_0x4103",     "AA8ED45473A3EA68803ACF990D364EAB259B5AB01392B587F6E670EB4B4FE962"),
                ("Fjernbetjening", "_0x4104",     "377DB865AE8240B9A82799C81B21FEAC7F4CA5E4244BE54578D87927098EBFEB"),
                ("Nøglering",      "_0x4105",     "566F8C92D559C24852A3AB1D6D13644234CE9595CC9DF68E5F89506EA73BB2BF"),
                ("Puck input",     "_0x4106",     "38F5D12A066E69A38E0D46E3193F802500AC40BAFAFC321DEBB1211D086A2BE3")),
            .. Section("LK IHC Wireless produkter\\02#Output",
                ("Stikkontakt",            "_0x4201",     "FE5E1168C6B90FEC94191F943F7760BEF1E145B342A95115E8054BA0D49A8B6B"),
                ("Lampeudtag",             "_0x4202",     "CFE181569F16ED15214AE87E76EE95B9F3EAAA2027AEE553023C2886A641830D"),
                ("Modtager relæ",          "_0x4203",     "02E81BE15E6339EF20F8D47FCB5524A75082718452CB6672D1D9063D7DC1F4DF"),
                ("Mobil stikkontakt relæ", "_0x4204",     "1E35126870DEF2D5A443419D4BFA865042AE06DAE997774388BC1A251A474156"),
                ("Puck relæ",              "_0x4205",     "F5F0D804CE7B04CB8DB4A712D5EF649FA530A8D2C28600B3D03B7FBE9D864395")),
            .. Section("LK IHC Wireless produkter\\03#Dimmer",
                ("Mobil stikkontakt dimmer", "_0x4303",     "431A691A58E785D24567E0102DAC380A4CF06343A0858DE9CDC92D15AD6C9DB3"),
                ("Lampeudtag dimmer",        "_0x4304",     "3CE2B424F173A32AEBE082B9DDF7D9388924921CC2266B35832644879EF15A0C"),
                ("Dimmer Universal",         "_0x4306",     "799C784268F7AC904DCE315A59F48EB0987F5890B2262C4E9A3E1BD0BA97F19B")),
            .. Section("LK IHC Wireless produkter\\04#Kombi",
                ("Kombi relæ 4 tast",   "_0x4404",     "2156952ECD433791C655E0CA36D02D455953623D1CD340A504454735034744E8"),
                ("Kombi dimmer 4 tast", "_0x4406",     "30E810580DAC4BC43C641DD72F7D8D30FC09B87AF25537BC27DC6BA1AB5BC952")),
            .. Section("LK IHC Wireless produkter\\05#Jalousi",
                ("Jalousi 2 tast (lokal lås)", "_0x4501",     "C7666FD971153F95C87F3B9DA4C3A7F18AC1285F13980933D567BA880A58A50F"),
                ("Jalousi 4 tast",             "_0x4502",     "18C2B59FED0AC1545AF37DB03FE7ADA33D34D2037AC87A7CE63C8FCBA41F0A5A")),
            .. Section("LK IHC Wireless produkter\\06#1-10v Converter",
                ("1-10v converter - Lampeudtag dimmer",   "_0x4304",     "974E15C22A8C91CC5B8BE59527D031806F0EB8C23D0CDDB6DAB9902B937E8AC0"),
                ("1-10v converter - Dimmer Universal",    "_0x4306",     "82A534BF9199354CBD012A1B9B9EA4D4A1FDCF38D7634AC72B86C9A6A0E305E5"),
                ("1-10v converter - Kombi dimmer 4 tast", "_0x4406",     "902000685683BA39A0B17AC7009DFC1AF3283EC5C64387BFA3E09090CE77CD5A")),
            .. Section("Specielle produkter\\01#Modificeret Wireless produkter",
                ("Mod. kombi Wireless relæ",   "_0x4407",     "B2D2FE14012C1273C44B6457CA6257C700F84A01FB1E2310DBC647E02A685DDC"),
                ("Mod. kombi Wireless 4 tast", "_0x4408",     "6D9791A6AB8122C052E4C339160BE3C2E435F9D00EEE3CC81495126BB59BCDE1")),
            .. Section("Specielle produkter\\02#Vinduer",
                ("Velux KLF-100",        "_0x21000007", "6BFCA57301BC35D2B5D25F60D26C817ED67C47D1025BF8F29040E9785135890A"),
                ("WindowMaster WUC 101", "_0x4408",     "E2836B4D01F88D9CFECE1DD97B3F280B327304B35E62301E79A90D1C056891C7"),
                ("WindowMaster WUC 102", "_0x4408",     "4EBC3180FBBD1C49F055B7FC3397C14929D8436127AF64D2F129E27B48BBEE25")),
            .. Section("Specielle produkter\\03#Udgaet produkter",
                ("Opus 66 Pir 24V",             "_0x21000004", "8F0A3481039D279DA249BEE7046F6C31B5FA9A14DA3032E6B0209F37C3B89CFB"),
                ("Skumringsrelæ med solsensor", "_0x2114",     "91B27D31DBA9ADD170875AC0DF812F061E1A33B1ED68857E8E71471703AA5C7F")),
            .. Section("Specielle produkter",
                ("S0 Device",                               "_0x2313",     "B94919EABABD7E354B20FBCD449A8B025AAC19D57E1BEE640CCF4CA757B045DE"),
                ("Controller Link OUT",                     "_0x2704",     "261811B9778029F801E0E0050DE70B2B1747DA7C0F3792F75B3DCD2DF2AF7FF3"),
                ("Controller Link IN",                      "_0x2705",     "854E00F0240C95897D069CF82DB464C3DA1A44425A1A7672BB542E568C7B249C"),
                ("LK IHC Wireless signalstyrke testudstyr", "_0x4801",     "DCE120F1EB9B34FE5E8BADA9C1AD701B8A772665CDF724543FD757C556711114")),
        ];

        // 73 blocks across 18 catalog folders, plus a final uncategorized section for the keyless user-saved one.
        private static readonly DigestRow[] FunctionBlockDigests =
        [
            .. Section("00. Foretrukne",
                ("1.1.01.e. Kip tænd sluk",           "88117737BF2691583AE63603E1F38879FC0BAFF98A4B67B8BAA0EA13CDC5D8E0"),
                ("1.1.02.g. Puls (Tænd / Sluk Alt)",  "81FBEE47AEE77832DA689A0D87B58198C51F3C2113CEE70D384B64A712180CBF"),
                ("1.2.04.e. Trådløs / Bus lysdæmper", "07079507028DCE1FF742E224E8DF5789ED209E06263775C4F2DAF276B2D0EF7C"),
                ("1.4.02.a. PIR styring ",            "31BC05D3C9200BBB49A352EF9E0A4999330D4E9DB75B15CB389BCDF43DD9426C")),
            .. Section("01. Lysstyring\\1.1 Generelt",
                ("1.1.01.e. Kip tænd sluk",              "2DC5B1931E33B1C4D5C3354A653AF02F9B11CB58CDE3E83504A257103A8DE79A"),
                ("1.1.02.g. Puls (Tænd / Sluk Alt)",     "E089DEA6F5554D5172316878178C5F6700BBBCD6DF744CF0A8C1159186D4E96F"),
                ("1.1.03.g. Fremkald / gem scenarie",    "7CEEE7E75D59FCFC3201B2803CBF24DCEF7FC6279DABAEA60D86F736F58BEC7A"),
                ("1.1.04.c. Følg / Invertering ",        "94C7E1998F6C2014858D27FF304FFB35D7D3BA21E351582D608E1AD452449CD7"),
                ("1.1.05. Beolink Fjernbetjening",       "256E95EF8DCAA897294B16CEE80105A704D7DCB29D76439D6AA991D259C9F2D3"),
                ("1.1.06. Kip / Touch",                  "1F15A12F3CF97E190F9EA754374920590BE0E9056CDD94FA4E44819D77C7A336"),
                ("1.1.12. Lysstyring (luxsensor)",       "0E0F5ED0C01DE0D0E3D062923869822A16FE64F1178B22778FFD7D9B306B38F7"),
                ("1.1.13.a. Dagslysstyring (luxsensor)", "8E8D6AB22C4CF67E3A7DC02ED53D9780073FAFC3192539775A474E30B04686B5")),
            .. Section("01. Lysstyring\\1.2 Lysdaempning",
                ("1.2.02.c. Fortrådet lysdæmper ",           "80E838F9D7E5F190E9721D81A00DF903BFDF3959D7EB615196A9EB3522D2D882"),
                ("1.2.04.e. Trådløs / Bus lysdæmper",        "D6785A94CCB645E30CBCE62062230BA23897EE7DC4ABFF8C2B816E94B0C52E3A"),
                ("1.2.05.c. Trådløs / Bus lysdæmperstyring", "676ECB4ED9A0EC900EE9AEB1446B54088A64B7C8DFED43AB7DB2678A00E4908D"),
                ("1.2.06. Fortrådet lysdæmperstyring",       "EBFFC17A258658936410F03A6C022407AA2AA39232553A982640D3CA970C4266"),
                ("1.2.07.b. Synkroniseringskontakt",         "2BC96F010E2E236A308EBB818500CF203514A8CF7C30D7C37E6D774BD0C1E760")),
            .. Section("01. Lysstyring\\1.3 Lys og ventilation",
                ("1.3.01.c. Ventilatorstyring ",       "44F4558E7AF6310D5CDC73169F836AA5CD400BF53458B9D1C2D1FE893FC42898"),
                ("1.3.05.b. Lys og ventilatorstyring", "7E799A454126D8DE3A089457B92EBB24714CA5447AF5C511E5E95ABBB94FFE58"),
                ("1.3.06. Ventilatorstyring - PIR",    "2A2A171484D297226802ACF764D752836EBE9383352D6167C1697B642CC7E509"),
                ("1.3.07. Fugtstyret ventilation",     "A1BAB47412A355C7F0A9933022B5FC938C671EAFCECAF4192FB78B6F88DD7048"),
                ("1.3.08. Dugpunktstyret ventilation", "8E7E969C4E07A78B4B8C45ED5A8F120FA2EFC13A6BC5ADFC7DE0F673406EE730")),
            .. Section("01. Lysstyring\\1.4 PIR og Timer",
                ("1.4.02.a. PIR styring ",            "D447F6D3D3162EFFDAFE19B9CC075EED6BB9894AF5FE5157E4DC6C4D1B0A18A7"),
                ("1.4.03.a. Udendørslys",             "2B6BFAF815BA1D99C6F5A85FF91351109552878C16F9B728375354AB10B5D8A4"),
                ("1.4.08.a. Neddrosling af PIR",      "AEC782B4F646AF1E0857D8D105F144D9B8D3BB727A1D4768EB55EC19EEF4A082"),
                ("1.4.09. PIR og tryk styret udgang", "86FACEDD5B85C4D09C6F312DCF10A68CEDB13537A9EF192C32C4E5C576F1BCCB")),
            .. Section("02. Tid, ur og kalender\\2.1 Generelt",
                ("2.1.01.a. Ur, med 1 tidspunkt", "A89528C2FE9103C6E179C35DE46D90FAA4C6196675CDE892EA4DD93B578885EE"),
                ("2.1.02.c. Uge ur",              "7FFC1D3DFCA1FB0CAA386F6666A415F3D4615959D8444DF0803DAE03838327C0"),
                ("2.1.04.a. Kalender",            "1C25C0DB80D9494234E84C45AAD07A619F79BA14E468F3A4237050883919C88B")),
            .. Section("03. Persienne og vindue\\3.1 Persienne og jalousi",
                ("3.1.01. Persiennestyring",              "CFA90306638E939B9886B6D5BC734A25341E97A6E12FFE46C8B2BBE5E1A6D62F"),
                ("3.1.02. Jalousistyring",                "6A324B602F582F4BAED04D17FF408F2F752E8F6AF198BE1DB096D1B55BDE8BD3"),
                ("3.1.03.a. Persiennestyring (Wireless)", "CC7E28CFA03C3A9A438A45DD396D969E0446B6FF5DE71536BAD54585FD044632")),
            .. Section("04. Specielle funktioner\\4.1 Generelt",
                ("4.1.01. AND (\"Og\"- blok)",           "17DC2B3E59D6D5533F4576A73D2FCC093DA3F6815D0EC7667E0768177FB16E47"),
                ("4.1.02. OR (\"Eller\"- blok)",         "02A86B74F0228F104D47DE98486BACFF96314C5A69CE51C75B738F2594A750EB"),
                ("4.1.03.a. Forsinket tiltræk/frafald",  "CD5ABDA6364152EDBBF8E9ADFB49D1BA0424EE298B7BC229E7C915BFE368811F"),
                ("4.1.04. Driftstimetæller",             "085F7E6F94306C547B5BEB24DE5CF752649939D5BD70D2D712AAA7B423E5FD53"),
                ("4.1.05.a. Driftstimetæller vist i år", "1725E7005EAA73318F609A632BEC626BA428C74118BF08957C3E01ED7D9BEE2B"),
                ("4.1.08.a. Betinget Følg/Kip",          "8428F513C13D61DCB8288989514E5C8720C6F868F7C5A7923B76DD4452E990D1"),
                ("4.1.09. Forsinkelsesblok",             "CE38B8E0033FE3E43E797AE8AC9487562110C5CA10B4BB7A8E2DB3272DAB4829"),
                ("4.1.14.a. Log af 10 indgange",         "44C1D21955297D5737A2DA0480B970F9180320E4115D608327104AD3350A3559")),
            .. Section("04. Specielle funktioner\\4.2 Udvidet",
                ("4.2.01. Fjernbetjening",  "8DB6D6F99892055E9A4C4DBC6C6CE1482D67BFF803408E24DF27FEBD84C0B2D0"),
                ("4.2.03.b. Pulsopsamling", "B806E0B17F0761439F33EEAE847EC0C521FBBD556DFB7C0CAD3D111F34972801"),
                ("4.2.04. Diode grøn/rød",  "530004D8A7BF2B9FAF875C38017B590E8831A82B37F90A02AA1566CC8192838A"),
                ("4.2.06. Dag og Nat",      "67AB2C4FE2EB1427B6CD7FEA0BD1C98FDFBF4235507236040C0E5474007F36A5")),
            .. Section("05. Klimastyring\\5.1 Generelt",
                ("5.1.01. Varmesænkning",                        "B649A265D228EC573D1DAFE1B18045639736134A3DCFF05AE87C94EBAAB3F7A1"),
                ("5.1.02. Styring af cirkulationspumpe med PIR", "95FFAED28E532FC93549691A8EB6A5B30502CB50AC552E267AC6C39D01D9166A")),
            .. Section("05. Klimastyring\\5.2 Udvidet",
                ("5.2.01.h. Betjeningspanel",              "C3C68CD8AB2F192F67B66012D962AED5E72EDDEF054466C606C549D215A628A7"),
                ("5.2.03.e. Pumpestyring",                 "2811E16FFF9593C353BAE34563B56E4C5349FD475BF9226B2724483A9B683038"),
                ("5.2.04.d. Ur til nat- og dagtemperatur", "66522518BF5C8617E862ECAD1DB3FF28281CE615E7C291F30EC903D7DCF9EB13"),
                ("5.2.05.d Varmestyring - avanceret",      "FE4122A2E06957AA1F60241F7632B17566513C8A591F3364D4BB5670A2B5785A"),
                ("5.2.06. Varmestyring - simpel",          "C586757C0B4F0AD00DA749A5D5505E8A57D8512E6A77C6B757385899018FC415")),
            .. Section("05. Klimastyring\\5.3 Indregulering",
                ("5.3.01. Log af Temperatur i 24 timer", "50E3CE12E2FF6BCB7D034FA11CEB31C1351BE70FA61869DE2D3730B9B1B2558A"),
                ("5.3.02. Log af temperatur",            "0784D8492E08AC0A13AF89C4A9928496CDBF379E660DE974C4CBF599D523EE21"),
                ("5.3.03.a. Max - Min Temp",             "B5DF6873BCEE6F3A37C673938BF6A1A4A0953588D1701E86A532B9CBC38C8DF7")),
            .. Section("05. Klimastyring\\5.4 Ventilation",
                ("5.4.01.c. Ventilationsstyring",     "8BF88564D1B73099AE28D846E80E3D63771BBDD9B6DCD0AAA2D4C68ABEDBAD10"),
                ("5.4.02. NILAN Gateway",             "8AB39B0D60FDBDE584AAF5F1DA5316EA9065B4005CE58A1A3F577E01DA40F840"),
                ("5.4.03. Hustilstand - Ventilation", "F8AE8FFBC5BC38505571666A1A37662F47F0F63F52193114255975C9250748C6")),
            .. Section("06. Alarm\\6.1 Tastatur",
                ("6.1.01.c. Fuga/Opus Kodetastatur uden memory",             "A973C45760B053C2CDC89735F35B5E39E07E8495F31ADF152D2F335AA02999C9"),
                ("6.1.03.b. LK Kodetastatur med 6 brugerkoder og 1 udgang",  "F493931E3DB3B64C1D55C0B01F7F909981DCC56D948426EF78E49FF98894119C"),
                ("6.1.04.b. LK Kodetastatur med 6 brugerkoder og 6 udgange", "AA16ED48FB9698E01B662B4DD0AE6B9A83FB33EEB1E3A5A3EF4FBF47CB7CE373"),
                ("6.1.05. LK Kodetastatur testblok",                         "F094671DF820F9CC301FD4D3B2056D5A190ACAD2328A0485D769C6F0584E3615")),
            .. Section("06. Alarm\\6.2 Overvaagning",
                ("6.2.01.i. Tyverialarm med 11 stk PIR", "49B69E6F99DD3097B71194B0826569065FD6B2A399BDBE2BD0AA18B57741D67E")),
            .. Section("06. Alarm\\6.3 Sikkerhed",
                ("6.3.01.a. Røg-Gas-Vand-El-alarm med logfunktion", "6E2D62EFC5CC43F1114BD79D329C35EE70DEA3D6FDEDCE243AF809E8641ED818"),
                ("6.3.02.g. Hjemmesimulering med 4 udgange",        "2B9B9108B462238BFA9E433B324DAEB76EDC7CE30624EFFB04863DAA2DD32029"),
                ("6.3.03.a. Overfaldstryk",                         "ACF1DAD594AF90A977D53F741C28DC214CC4E3752D67A6F237CF4B93DF00417D"),
                ("6.3.05. Røgalarm med logfunktion",                "0E684BCE63988281A392D1F1BDD30FD17818D05660408093D464C3779CDD8FA8")),
            .. Section("06. Alarm\\6.4 Lydblokke",
                ("6.4.01.a. Lyd-/lysblok med variabel puls",                           "93EEABD990D4FDD46CAFC8D1DB77CC40ED70199FE67F2DA59E3C9F61C0486020"),
                ("6.4.02.a. Lyd-/lysblok med 2 pulsperioder",                          "E06194EAF995349AA08A31F396AC49A127E453BBBD6C63EA252BF0F4469208CC"),
                ("6.4.03.a. Lyd-/lysblok med X antal pulser efterfulgt af lang pause", "C397D2BBF222E163B4B4B6DDAC79279493B79EC8E1023905675E5DDF1A81A655")),
            .. Section("08. Viewer\\8.1 Generelt",
                ("8.1.01.a. Ur og dato",                 "DAB5832B3DC7EF015FB2529550E6493D0F98D78F55DC3DF61CE2F7064ED58E35"),
                ("8.1.02. Sikker touch",                 "6A800FC653215DEF3F32D0E7E22455BDEB4BABCB3C870EAC40559519F60F672B"),
                ("8.1.03. Log af solopgang og -nedgang", "1A44CD3C29CBF0561C75E7EE94B0E3A1D02DAFAF344AAF3B42AF686D806946F4")),
            .. Section("",   // the keyless user-saved block — no catalog folder
                ("AutoProof", "DD928C18D0DF0B50D0581A077DEB4898042ADBD8B64BF1F3DFBE11246B919E94")),
        ];

        private const string NewProjectSkeletonDigest = "D81F8D45FD411759FA0944336BC7DCB64FB68B3A99194349D0E5FBD14F6BA14A";
        private const string BuiltInEnumeratorsDigest = "A752DBF9469889ACF4017957B8602186679290CC788DB9AA9FD4F03ED86F526B";
        private const string EmptyFunctionBlockTemplateDigest = "D1826501E901D903438F76C36D4FC7A7D289304CC949AD89CFBC79011620A461";

        // Every public member of the two definition records, and whether it is hashed. A member added to either
        // record fails DefinitionSurfaceIsFullyHashed until it is listed here, so nothing can silently escape.
        // Grammar and SourceEncoding are hashed THROUGH the writer's bytes (header text; BOM and byte encoding);
        // SourceEncoding is additionally appended to the tail, because a Latin1<->Utf8 flip is invisible in the
        // bytes of a definition whose text happens to be pure ASCII.
        private static readonly string[] HashedProductMembers =
            ["ProductIdentifier", "DisplayName", "CategoryPath", "Body", "Grammar", "SourceEncoding"];

        private static readonly string[] UnhashedProductMembers = ["Documentation", "Resources"];

        private static readonly string[] HashedFunctionBlockMembers =
            ["MasterType", "MasterVersion", "MasterName", "DisplayName", "CategoryPath", "Body", "Grammar",
             "SourceEncoding", "ExplicitCloseIds", "IsEmptyTemplate"];

        // Inputs/Outputs/Settings/InternalVariables are computed projections OF Body, which is hashed.
        private static readonly string[] UnhashedFunctionBlockMembers =
            ["Documentation", "Inputs", "Outputs", "Settings", "InternalVariables"];

        // The three committed definition files, scanned as source text by NoSourceEolBearingLiterals.
        private static readonly string[] DefinitionFiles =
        [
            "BuiltInCatalog.Products.g.cs", "BuiltInCatalog.FunctionBlocks.g.cs", "BuiltInCatalog.Grammar.g.cs",
        ];

        [Test]
        public void EveryProduct_MatchesRecordedDigest() =>
            AssertCatalog(ProductDigests, ProductRows(new BuiltInCatalog()));

        [Test]
        public void EveryFunctionBlock_MatchesRecordedDigest() =>
            AssertCatalog(FunctionBlockDigests, FunctionBlockRows(new BuiltInCatalog()));

        /// <summary>
        /// Three invariants, asserted and named separately so a failure says which one broke.
        /// <para><b>Uniqueness, on BOTH sides.</b> The premise the keying rests on, and the only real duplicate-key
        /// guard: comparing two collections can never be one, since two identical duplicates on both sides compare
        /// equal.</para>
        /// <para><b>Content, order-independent</b> — an added, removed or changed definition reports as the named
        /// row it is. See <see cref="Differences"/>.</para>
        /// <para><b>Order, over the KEYS only.</b> Catalog list order is contractual: the two reference-catalog
        /// differentials position-pair <c>built.Products[i]</c> against <c>reference.Products[i]</c>, and
        /// <see cref="MaterializedCatalog"/>'s last-wins index resolves by list position. Those differentials
        /// <c>Assert.Ignore</c> without an IHC Visual install, so this is the only guard on that contract that
        /// runs in CI. Asserting it over the keys makes a reorder read AS a reorder, and sharing one
        /// <c>Assert.Multiple</c> with the content check means an insertion reports both what was inserted and
        /// where the sequence diverged.</para>
        /// </summary>
        private static void AssertCatalog(DigestRow[] recorded, DigestRow[] produced) =>
            Assert.Multiple(() =>
            {
                Assert.That(recorded.Select(Key), Is.Unique, "recorded rows: (CategoryPath, Name) must be unique");
                Assert.That(produced.Select(Key), Is.Unique, "catalog rows: (CategoryPath, Name) must be unique");
                Assert.That(Differences(recorded, produced), Is.Empty, "recorded digests");
                Assert.That(produced.Select(Key), Is.EqualTo(recorded.Select(Key)), "catalog registration order");
            });

        private static (string CategoryPath, string Name) Key(DigestRow row) => (row.CategoryPath, row.Name);

        /// <summary>
        /// The content comparison, as a keyed diff: one line per affected definition, nothing for the rest.
        /// <c>Is.EquivalentTo</c> was measured here first — it does name the right rows, but only after dumping
        /// BOTH hundred-element collections in full (~19 KB each), so its output grows with the catalog rather
        /// than with the damage.
        /// <para>Everything outside the key is compared, not just the digest: a product's <c>LookupKey</c> is
        /// inside its own hashed tail, so the catalog side cannot disagree with its digest — but the RECORDED
        /// side's is a hand-written cell, where a typo would otherwise pass unnoticed.</para>
        /// <para><c>ToLookup</c>, never <c>ToDictionary</c>: a duplicate key must report as a diff line, not
        /// throw out of the enclosing <c>Assert.Multiple</c> and take the order assertion with it.</para>
        /// </summary>
        private static string[] Differences(DigestRow[] recorded, DigestRow[] produced)
        {
            ILookup<(string CategoryPath, string Name), string> was = recorded.ToLookup(Key, Value);
            ILookup<(string CategoryPath, string Name), string> now = produced.ToLookup(Key, Value);
            return
            [
                .. was.Select(g => g.Key).Union(now.Select(g => g.Key))
                    .Where(key => Rendered(was, key) != Rendered(now, key))
                    .Select(key => $"'{key.Name}' in '{key.CategoryPath}': "
                        + $"recorded [{Rendered(was, key)}], produced [{Rendered(now, key)}]"),
            ];

            static string Value(DigestRow row) => $"{row.LookupKey} {row.Digest}".TrimStart();

            static string Rendered(ILookup<(string, string), string> rows, (string, string) key) =>
                string.Join(" | ", rows[key].Order(StringComparer.Ordinal));
        }

        // The one projection each side is both asserted and recorded through, so Record() cannot drift from
        // what the assertions read.
        private static DigestRow[] ProductRows(ICatalog catalog) =>
            [.. catalog.Products.Select(p =>
                new DigestRow(p.CategoryPath, p.DisplayName, p.ProductIdentifier, Digest(p)))];

        private static DigestRow[] FunctionBlockRows(ICatalog catalog) =>
            [.. catalog.FunctionBlocks.Select(f =>
                new DigestRow(f.CategoryPath, f.DisplayName, string.Empty, Digest(f)))];

        [Test]
        public void EveryTemplate_MatchesRecordedDigest()
        {
            ICatalog catalog = new BuiltInCatalog();
            Assert.Multiple(() =>
            {
                Assert.That(Digest(catalog.NewProjectSkeleton), Is.EqualTo(NewProjectSkeletonDigest),
                    nameof(ICatalog.NewProjectSkeleton));
                Assert.That(Digest(catalog.BuiltInEnumerators), Is.EqualTo(BuiltInEnumeratorsDigest),
                    nameof(ICatalog.BuiltInEnumerators));
                Assert.That(DigestTemplate(catalog.EmptyFunctionBlockTemplate),
                    Is.EqualTo(EmptyFunctionBlockTemplateDigest), nameof(ICatalog.EmptyFunctionBlockTemplate));
            });
        }

        /// <summary>
        /// A member added to either definition record must be classified as hashed or deliberately unhashed, or it
        /// escapes this gate silently. Two-way set equality against reflection, the idiom
        /// <c>BuilderGrammarSurfaceTests</c> already uses for the builder verb surface.
        /// </summary>
        [Test]
        public void DefinitionSurfaceIsFullyHashed()
        {
            Assert.Multiple(() =>
            {
                AssertSurface(typeof(ProductDefinition), HashedProductMembers, UnhashedProductMembers);
                AssertSurface(typeof(FunctionBlockDefinition), HashedFunctionBlockMembers,
                    UnhashedFunctionBlockMembers);
            });
        }

        /// <summary>
        /// The digests must reproduce on every platform, and today they do only because the transcribed data holds
        /// every newline as an escaped <c>\r\n</c> in a regular string literal. A raw string literal
        /// (<c>"""…"""</c>) or a multi-line verbatim literal (<c>@"…"</c>) would instead inherit the <b>checkout's</b>
        /// line endings — C# normalizes neither — and these files carry no <c>.gitattributes</c> entry, so they
        /// check out LF on Linux and CRLF on Windows. Digests recorded on one would then fail on the other, with a
        /// baffling failure. Both constructs are absent today and are banned here, in the file that depends on their
        /// absence. (A <c>.gitattributes</c> pin was considered: it makes the checkout deterministic but does not
        /// stop the construct being introduced, which is the actual hazard.)
        /// </summary>
        [Test]
        public void DefinitionSources_CarryNoSourceEolBearingLiterals()
        {
            string dir = Path.Combine(TestRepository.RequireRoot(), "ihcclient", "src", "vis", "catalog",
                "definitions");
            (string Token, string Construct)[] banned =
                [("\"\"\"", "raw string literal"), ("@\"", "verbatim string literal")];

            var offenders = new List<string>();
            foreach (string file in DefinitionFiles)
            {
                string text = File.ReadAllText(Path.Combine(dir, file));
                offenders.AddRange(banned.Where(b => text.Contains(b.Token, StringComparison.Ordinal))
                    .Select(b => $"{file}: contains a {b.Construct} ({b.Token})"));
            }

            Assert.That(offenders, Is.Empty,
                "the transcribed catalog data must hold newlines as escaped \\r\\n in regular string literals, so "
                + "the recorded digests reproduce on every platform:\n" + string.Join("\n", offenders));
        }

        private static void AssertSurface(Type definition, string[] hashed, string[] unhashed)
        {
            string[] declared = definition
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .Where(n => n != "EqualityContract")
                .Order(StringComparer.Ordinal)
                .ToArray();
            Assert.That(declared, Is.EquivalentTo(hashed.Concat(unhashed)),
                $"{definition.Name}: every member must be classified hashed or deliberately unhashed — see "
                + "the class remarks for what this gate covers");
        }

        /// <summary>
        /// Records every digest as paste-ready C#, in the same sectioned shape as the tables above. Run once, at
        /// bootstrap, from a tree where the reference-catalog differential is green (i.e. <c>IhcVisualInstallDir</c>
        /// is configured and <c>BuiltInCatalog*DifferentialTests</c> pass, not skip); paste the emitted text over
        /// the tables above. Re-recording rebaselines BY CONSTRUCTION — it hashes whatever the catalog currently
        /// produces — so it can only be run from a tree already known good by other means.
        /// <code>
        /// dotnet test tests/safe_project_tests/safe_project_tests.csproj \
        ///   --filter "FullyQualifiedName~BuiltInCatalogDigestTests.Record"
        /// </code>
        /// </summary>
        [Test, Explicit("Recorder — bootstrap only; see the class remarks.")]
        public void Record()
        {
            var catalog = new BuiltInCatalog();
            var text = new StringBuilder();

            AppendTable(text, nameof(ProductDigests), ProductRows(catalog), withLookupKey: true);
            text.AppendLine();
            AppendTable(text, nameof(FunctionBlockDigests), FunctionBlockRows(catalog), withLookupKey: false);
            text.AppendLine();
            text.AppendLine(
                $"        private const string NewProjectSkeletonDigest = \"{Digest(catalog.NewProjectSkeleton)}\";");
            text.AppendLine(
                $"        private const string BuiltInEnumeratorsDigest = \"{Digest(catalog.BuiltInEnumerators)}\";");
            text.AppendLine("        private const string EmptyFunctionBlockTemplateDigest = "
                + $"\"{DigestTemplate(catalog.EmptyFunctionBlockTemplate)}\";");

            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "catalog-digests.cs.txt");
            File.WriteAllText(path, text.ToString());
            TestContext.AddTestAttachment(path);
            TestContext.Out.WriteLine(path);
        }

        /// <summary>
        /// Emits one table in the recorded shape: a <c>Section(...)</c> per RUN of same-category rows, column
        /// padded within each. Runs, not a <c>GroupBy</c> — a category returning later in the registration list
        /// must open a second section rather than fold into the first, which would reorder rows against the
        /// order assertion that reads them back.
        /// </summary>
        private static void AppendTable(StringBuilder text, string field, DigestRow[] rows, bool withLookupKey)
        {
            text.AppendLine($"        private static readonly DigestRow[] {field} =");
            text.AppendLine("        [");
            for (int start = 0; start < rows.Length;)
            {
                int end = start;
                while (end < rows.Length && rows[end].CategoryPath == rows[start].CategoryPath)
                {
                    end++;
                }

                DigestRow[] section = rows[start..end];
                int width = section.Max(r => Literal(r.Name).Length) + 1;
                // An empty category is not a recording glitch, so the emitted table says so itself — otherwise
                // the annotation is lost the first time someone pastes a rebaseline over it.
                string note = section[0].CategoryPath.Length == 0
                    ? "   // the keyless user-saved block — no catalog folder" : string.Empty;
                text.AppendLine($"            .. Section({Literal(section[0].CategoryPath)},{note}");
                for (int i = 0; i < section.Length; i++)
                {
                    DigestRow row = section[i];
                    string key = withLookupKey ? (Literal(row.LookupKey) + ",").PadRight(14) + " " : string.Empty;
                    string close = i == section.Length - 1 ? ")," : ",";   // the last row closes Section( too
                    text.AppendLine(
                        $"                ({(Literal(row.Name) + ",").PadRight(width)} {key}\"{row.Digest}\"){close}");
                }

                start = end;
            }

            text.AppendLine("        ];");
        }

        /// <summary>
        /// A C# regular string literal. Escaping only <c>\</c> and <c>"</c> is not a general encoder, so rather
        /// than write one this REFUSES what it cannot encode: display names and category paths are printable
        /// text, and a control character in one is an anomaly that should fail loudly rather than be encoded
        /// into a table nobody re-reads. <c>char.IsControl</c> covers the C1 block too, which is what a
        /// mis-decoded Latin-1 definition would produce.
        /// </summary>
        private static string Literal(string value)
        {
            Assert.That(value.Any(char.IsControl), Is.False,
                $"refusing to emit a control character in '{value}' — extend the encoder deliberately");
            return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        }

        // ---- canonical forms ----

        private static string Digest(ProductDefinition d) =>
            Written(ms =>
            {
                CatalogFileWriter.Write(d, ms);   // body + ids + attribute order + DOCTYPE/DTD header + encoding
                AppendTail(ms, [d.ProductIdentifier, d.DisplayName, d.CategoryPath, d.SourceEncoding.ToString()]);
            });

        private static string Digest(FunctionBlockDefinition d) =>
            Written(ms =>
            {
                CatalogFileWriter.Write(d, ms);
                AppendTail(ms, [
                    d.MasterType, d.MasterVersion, d.MasterName, d.DisplayName, d.CategoryPath,
                    d.SourceEncoding.ToString(),
                    d.IsEmptyTemplate ? "1" : "0",
                    CloseIds(d)]);
            });

        /// <summary>
        /// Hashes what <paramref name="emit"/> writes — or, when the writer refuses the definition outright,
        /// yields a sentinel instead of propagating. A definition that can no longer be serialized at all (its
        /// grammar removed, an unencodable character introduced) is exactly the regression this gate exists to
        /// catch, and a thrown exception would abort the enclosing <c>Assert.Multiple</c> at the FIRST definition
        /// — reporting one row where the affected set may be thirty. The sentinel cannot collide with a real
        /// digest, which is always 64 hex characters.
        /// </summary>
        private static string Written(Action<MemoryStream> emit)
        {
            using var ms = new MemoryStream();
            try
            {
                emit(ms);
            }
            catch (Exception ex) when (ex is CatalogFormatException or InvalidOperationException)
            {
                return $"NOT WRITABLE: {ex.GetType().Name}: {ex.Message}";
            }
            return Hex(ms);
        }

        // The two bare-element templates: not definitions, so CatalogFileWriter has no overload for them.
        private static string Digest(ProjectElement element)
        {
            using var ms = new MemoryStream();
            using (BinaryWriter w = Framed(ms))
            {
                Encode(w, element);
            }
            return Hex(ms);
        }

        // The empty-FB template: a definition, but with Grammar = CatalogGrammar.Empty on purpose (see
        // BuiltInCatalog.Templates.cs), which CatalogFileWriter rejects — it has no on-disk form. Every opted-in
        // property is therefore encoded explicitly instead.
        private static string DigestTemplate(FunctionBlockDefinition d)
        {
            using var ms = new MemoryStream();
            using (BinaryWriter w = Framed(ms))
            {
                Encode(w, d.Body);
                w.Write(d.MasterType);
                w.Write(d.MasterVersion);
                w.Write(d.MasterName);
                w.Write(d.DisplayName);
                w.Write(d.CategoryPath);
                w.Write(d.IsEmptyTemplate);
                w.Write(d.SourceEncoding.ToString());
                w.Write(CloseIds(d));
                // Empty by design here. Encoded structurally so a grammar appearing, or changing shape, is caught;
                // a change WITHIN a declaration is not — if this template ever gains a real grammar, give it one
                // properly and route it through the writer like everything else.
                w.Write(d.Grammar.IsEmpty);
                w.Write(d.Grammar.DeclaredEncoding);
                w.Write(d.Grammar.DoctypeRoot ?? string.Empty);
                w.Write(d.Grammar.Declarations.Length);
            }
            return Hex(ms);
        }

        // A deterministic, length-framed encoding of an element tree: Tag, the nullable Id (which is distinct from
        // the id ATTRIBUTE — BuiltInCatalog.Templates.cs derives it via ElementId.ParseOrNull, so a token that stops
        // parsing changes Id while leaving the attribute intact), every attribute name and value in order, and every
        // child in order. Length framing makes the encoding injective: no value can imitate a structural boundary.
        private static void Encode(BinaryWriter w, ProjectElement e)
        {
            w.Write(e.Tag);
            w.Write(e.Id is not null);
            w.Write(e.Id?.ToToken() ?? string.Empty);
            w.Write(e.Attrs.Length);
            foreach ((string name, string value) in e.Attrs)
            {
                w.Write(name);
                w.Write(value);
            }
            w.Write(e.Children.Length);
            foreach (ProjectElement child in e.Children)
            {
                Encode(w, child);
            }
        }

        // ExplicitCloseIds is an unordered set, so it is ordered before framing to keep the digest deterministic.
        private static string CloseIds(FunctionBlockDefinition d) =>
            string.Join(',', d.ExplicitCloseIds.Select(id => id.ToToken()).Order(StringComparer.Ordinal));

        // The members CatalogFileWriter cannot see, in a fixed order.
        private static void AppendTail(Stream stream, string[] parts)
        {
            using BinaryWriter w = Framed(stream);
            foreach (string part in parts)
            {
                w.Write(part);
            }
        }

        // BinaryWriter length-prefixes each string, so all framing here is injective and needs no separator.
        private static BinaryWriter Framed(Stream stream) => new(stream, Encoding.UTF8, leaveOpen: true);

        private static string Hex(MemoryStream ms) => Convert.ToHexString(SHA256.HashData(ms.ToArray()));
    }
}
