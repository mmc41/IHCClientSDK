using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FakeItEasy;
using Ihc.App;
using NJsonSchema;
using NJsonSchema.Validation;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <c>adminmodel.schema.json</c> must describe every admin-model file the SDK reads or writes. Each case reads a
    /// complete file with <see cref="AdminAppService.LoadFromJson(Stream)"/>, writes it again with
    /// <see cref="AdminAppService.SaveAsJson(MutableAdminModel, Stream)"/>, and checks both files two ways: against the
    /// published schema, with NJsonSchema, and against the model itself, by reading them under
    /// <see cref="JsonSerializerOptions.Strict"/>.
    ///
    /// <para>The files are written out whole, in the form <c>SaveAsJson</c> writes, because the written file is also
    /// compared with the one that was read: a renamed key or a changed value form fails here rather than when a backup
    /// is restored. Between them they put a null in every optional string and use every group the SDK reads, so
    /// neither a schema that refuses those nulls nor one that lacks a group can pass.</para>
    /// </summary>
    [TestFixture]
    public class AdminModelSchemaTests
    {
        /// <summary>
        /// The strict reading of an admin-model file: <see cref="JsonSerializerOptions.Strict"/>, which refuses a member
        /// the model lacks, a duplicate key and a null the model's annotations forbid, with the converter the SDK writes
        /// enumerations by name with.
        /// </summary>
        private static readonly JsonSerializerOptions StrictReading = new(JsonSerializerOptions.Strict)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        /// <summary>The metadata block for the model and SDK version under test, which <c>LoadFromJson</c> requires of
        /// every file.</summary>
        private static readonly string Metadata = JsonSerializer.Serialize(ModelMetadata.Current(typeof(MutableAdminModel)));

        private JsonSchema schema;

        [OneTimeSetUp]
        public async Task LoadSchema()
        {
            schema = await JsonSchema.FromJsonAsync(await File.ReadAllTextAsync(
                Path.Combine(TestContext.CurrentContext.TestDirectory, "schema", "adminmodel.schema.json")));
        }

        [SetUp]
        public void Setup()
        {
            // The passphrase file encryption derives its key from, as AdminServiceTests sets it.
            Environment.SetEnvironmentVariable("IHC_ENCRYPT_PASSPHRASE", "test-passphrase-for-unit-tests");
        }

        /// <summary>
        /// A file the schema describes is read and written back unchanged, and the written file is described by the
        /// schema too; the model reads both strictly.
        /// </summary>
        [TestCaseSource(nameof(Files))]
        public async Task LoadFromJson_AndSaveAsJson_ReadAndWriteFilesTheSchemaDescribes(string file)
        {
            using AdminAppService service = NewService(fileEncryption: false);
            AssertDescribed(file, "the file LoadFromJson reads");

            string written = await Save(service, await Load(service, file));

            AssertDescribed(written, "the file SaveAsJson writes");
            Assert.That(JsonNode.DeepEquals(UsersByName(written), UsersByName(file)), Is.True,
                $"SaveAsJson must write back the file LoadFromJson read, and wrote:{Environment.NewLine}{written}");
        }

        /// <summary>
        /// With file encryption on, the passwords and keys are written encrypted, and the file is still one the schema
        /// describes and the model reads strictly.
        /// </summary>
        [TestCaseSource(nameof(Files))]
        public async Task SaveAsJson_WithFileEncryption_WritesAFileTheSchemaDescribes(string file)
        {
            using AdminAppService reader = NewService(fileEncryption: false);
            using AdminAppService writer = NewService(fileEncryption: true);

            string written = await Save(writer, await Load(reader, file));

            Assert.That(JsonNode.DeepEquals(UsersByName(written), UsersByName(file)), Is.False,
                "the passwords and keys must be written encrypted, or this case checks nothing the plain one does not");
            AssertDescribed(written, "the encrypted file SaveAsJson writes");
        }

        /// <summary>
        /// A file without its account list, absent or null, is one the schema describes and <c>LoadFromJson</c> loads,
        /// and <c>SaveAsJson</c> writes the loaded model back as a file the schema describes. Only <c>Store</c> refuses
        /// such a model, as <see cref="AdminServiceTests.LoadFromJson_AcceptsAFileWithoutUsers_ButStoreRefusesIt"/>
        /// checks.
        /// </summary>
        [TestCase(false, TestName = "{m}(no Users member)")]
        [TestCase(true, TestName = "{m}(Users is null)")]
        public async Task AFileWithoutUsers_LoadsAndSavesAsAFileTheSchemaDescribes(bool nullInsteadOfAbsent)
        {
            JsonObject file = ParseFile(EverythingSetUp);
            if (nullInsteadOfAbsent)
                file["Users"] = null;
            else
                file.Remove("Users");
            string text = file.ToJsonString();
            using AdminAppService service = NewService(fileEncryption: false);
            AssertDescribed(text, "a file without users");

            MutableAdminModel loaded = await Load(service, text);
            string written = await Save(service, loaded);

            Assert.That(loaded.Users, Is.Null, "the file loads without users");
            AssertDescribed(written, "the file SaveAsJson writes for a model without users");
        }

        private static IEnumerable<TestCaseData> Files()
        {
            yield return new TestCaseData(NothingSetUp).SetArgDisplayNames("a controller with nothing set up");
            yield return new TestCaseData(EverythingSetUp).SetArgDisplayNames("a controller with everything set up");
        }

        /// <summary>
        /// A controller with nothing set up: no mailbox, no mail server, no DNS server and the WiFi client off. Every
        /// optional string is null, which is how the SDK reads the nil elements such a controller answers, and one
        /// account comes back with no group and no readable dates.
        /// </summary>
        private static string NothingSetUp => $$"""
            {
              "ModelMetadata": {{Metadata}},
              "Users": [
                { "Username": "admin", "Password": "test-password", "Email": null, "Firstname": null, "Lastname": null,
                  "Phone": null, "Group": "Administrators", "Project": null,
                  "CreatedDate": "2026-01-05T09:00:00+01:00", "LoginDate": "2026-11-03T16:19:00+01:00" },
                { "Username": "no-group", "Password": "test-password", "Email": null, "Firstname": null, "Lastname": null,
                  "Phone": null, "Group": "None", "Project": null,
                  "CreatedDate": "0001-01-01T00:00:00+00:00", "LoginDate": "0001-01-01T00:00:00+00:00" }
              ],
              "EmailControl": { "ServerIpAddress": null, "ServerPortNumber": 110, "Pop3Username": null, "Pop3Password": null,
                "EmailAddress": null, "PollInterval": 5, "RemoveEmailsAfterUsage": false, "Ssl": false },
              "SmtpSettings": { "Hostname": null, "Hostport": 25, "Username": null, "Password": null, "Ssl": false,
                "SendLowBatteryNotification": false, "SendLowBatteryNotificationRecipient": null,
                "SendLEDDimmerErrorNotification": false },
              "DnsServers": { "PrimaryDNS": null, "SecondaryDNS": null },
              "NetworkSettings": { "IpAddress": "192.168.1.3", "Netmask": "255.255.255.0", "Gateway": "192.168.1.1",
                "HttpPort": 80, "HttpsPort": 443 },
              "WebAccess": {
                "UsbLoginRequired": false,
                "AdministratorUsb": true, "AdministratorInternal": true, "AdministratorExternal": false,
                "TreeviewUsb": true, "TreeviewInternal": false, "TreeviewExternal": false,
                "SceneviewUsb": true, "SceneviewInternal": true, "SceneviewExternal": false,
                "ScenedesignUsb": true, "ScenedesignInternal": false, "ScenedesignExternal": false,
                "ServerstatusUsb": true, "ServerstatusInternal": false, "ServerstatusExternal": false,
                "IhcvisualUsb": true, "IhcvisualInternal": true, "IhcvisualExternal": false,
                "OnlinedocumentationUsb": true, "OnlinedocumentationInternal": true, "OnlinedocumentationExternal": false,
                "WebsceneviewUsb": false, "WebsceneviewInternal": true, "WebsceneviewExternal": false,
                "OpenapiUsb": false, "OpenapiInternal": false, "OpenapiExternal": false, "OpenapiUsed": false
              },
              "WLanSettings": { "Enabled": false, "Ssid": null, "Key": null, "SecurityType": "NONE", "EncryptionType": null,
                "IpAddress": "0.0.0.0", "Netmask": "0.0.0.0", "Gateway": "0.0.0.0" }
            }
            """;

        /// <summary>
        /// A controller with everything set up and every string filled: a mailbox, a mail server sending both
        /// notifications, two DNS servers, the WiFi client on WPA2, and an account in each group the controller has.
        /// </summary>
        private static string EverythingSetUp => $$"""
            {
              "ModelMetadata": {{Metadata}},
              "Users": [
                { "Username": "admin", "Password": "test-password-1", "Email": "admin@example.com", "Firstname": "Søren",
                  "Lastname": "Østergård", "Phone": "+45 12 34 56 78", "Group": "Administrators", "Project": "Hus",
                  "CreatedDate": "2025-02-03T08:15:00+01:00", "LoginDate": "2026-11-03T16:19:00+01:00" },
                { "Username": "family", "Password": "test-password-2", "Email": "family@example.com", "Firstname": "Åse",
                  "Lastname": "Bækgaard", "Phone": "12345678", "Group": "Users", "Project": "Hus",
                  "CreatedDate": "2025-02-03T08:20:00+01:00", "LoginDate": "2026-10-30T07:45:00+01:00" }
              ],
              "EmailControl": { "ServerIpAddress": "pop3.example.com", "ServerPortNumber": 995,
                "Pop3Username": "ihc@example.com", "Pop3Password": "test-password-3", "EmailAddress": "ihc@example.com",
                "PollInterval": 180, "RemoveEmailsAfterUsage": true, "Ssl": true },
              "SmtpSettings": { "Hostname": "smtp.example.com", "Hostport": 465, "Username": "ihc@example.com",
                "Password": "test-password-4", "Ssl": true, "SendLowBatteryNotification": true,
                "SendLowBatteryNotificationRecipient": "service@example.com", "SendLEDDimmerErrorNotification": true },
              "DnsServers": { "PrimaryDNS": "192.0.2.53", "SecondaryDNS": "198.51.100.53" },
              "NetworkSettings": { "IpAddress": "10.0.0.20", "Netmask": "255.255.0.0", "Gateway": "10.0.0.1",
                "HttpPort": 8080, "HttpsPort": 8443 },
              "WebAccess": {
                "UsbLoginRequired": true,
                "AdministratorUsb": true, "AdministratorInternal": true, "AdministratorExternal": true,
                "TreeviewUsb": true, "TreeviewInternal": true, "TreeviewExternal": true,
                "SceneviewUsb": true, "SceneviewInternal": true, "SceneviewExternal": true,
                "ScenedesignUsb": true, "ScenedesignInternal": true, "ScenedesignExternal": true,
                "ServerstatusUsb": true, "ServerstatusInternal": true, "ServerstatusExternal": true,
                "IhcvisualUsb": true, "IhcvisualInternal": true, "IhcvisualExternal": true,
                "OnlinedocumentationUsb": true, "OnlinedocumentationInternal": true, "OnlinedocumentationExternal": true,
                "WebsceneviewUsb": true, "WebsceneviewInternal": true, "WebsceneviewExternal": true,
                "OpenapiUsb": true, "OpenapiInternal": true, "OpenapiExternal": true, "OpenapiUsed": true
              },
              "WLanSettings": { "Enabled": true, "Ssid": "Home", "Key": "test-wifi-key", "SecurityType": "WPA2-PSK",
                "EncryptionType": "CCMP", "IpAddress": "192.168.2.3", "Netmask": "255.255.255.0", "Gateway": "192.168.2.1" }
            }
            """;

        /// <summary>A service for files only: neither method under test calls the controller services it is given.</summary>
        private static AdminAppService NewService(bool fileEncryption) => new(
            new IhcSettings
            {
                Endpoint = "http://admin-schema.invalid",
                UserName = "testuser",
                Password = "testpass",
                Application = Application.administrator,
                LogSensitiveData = false,
                AsyncContinueOnCapturedContext = false
            },
            fileEncryption, A.Fake<IAuthenticationService>(), A.Fake<IUserManagerService>(), A.Fake<IConfigurationService>());

        private static async Task<MutableAdminModel> Load(AdminAppService service, string file)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));
            return await service.LoadFromJson(stream);
        }

        private static async Task<string> Save(AdminAppService service, MutableAdminModel model)
        {
            using var stream = new MemoryStream();
            await service.SaveAsJson(model, stream);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>Asserts the file is described by the published schema and read by the model's strict reading.</summary>
        private void AssertDescribed(string file, string what)
        {
            Assert.Multiple(() =>
            {
                Assert.That(LeafErrors(schema.Validate(file)), Is.Empty, $"{what} breaks adminmodel.schema.json");
                Assert.That(() => JsonSerializer.Deserialize<MutableAdminModel>(file, StrictReading), Throws.Nothing,
                    $"{what} breaks the model's strict reading");
            });
        }

        /// <summary>
        /// The errors that name a field. NJsonSchema reports a bad array item as one error whose causes sit in its
        /// children, which alone would name the account but not the field.
        /// </summary>
        private static IEnumerable<string> LeafErrors(IEnumerable<ValidationError> errors) =>
            errors.SelectMany(error => error is ChildSchemaValidationError { Errors.Count: > 0 } parent
                ? LeafErrors(parent.Errors.Values.SelectMany(causes => causes))
                : [$"{error.Path}: {error.Kind}"]);

        /// <summary>
        /// The file with its accounts in name order. They are a set, so the order a save lists them in is no part of the
        /// file's content.
        /// </summary>
        private static JsonObject UsersByName(string file)
        {
            JsonObject root = ParseFile(file);
            if (root["Users"] is JsonArray users)
            {
                root["Users"] = new JsonArray([.. users
                    .OrderBy(user => user?["Username"]?.GetValue<string>(), StringComparer.Ordinal)
                    .Select(user => user?.DeepClone())]);
            }
            return root;
        }

        private static JsonObject ParseFile(string file) =>
            JsonNode.Parse(file) as JsonObject ?? throw new AssertionException("An admin-model file is a JSON object.");
    }
}
