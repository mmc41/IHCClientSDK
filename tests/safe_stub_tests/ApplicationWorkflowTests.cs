using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Ihc.App;
using Ihc.Vis;
using Ihc.Vis.Projects;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// One real workflow per application service, each over a default stub and nothing else, so the stub's default
    /// scenario is shown to carry a positive path through the application layer, not only through each wrapper.
    /// </summary>
    [TestFixture]
    public class ApplicationWorkflowTests
    {
        [Test]
        public async Task Admin_StoresOneSettingAndOneUser_AndTheNextModelReturnsBoth()
        {
            using var sdk = StubbedSdk.Start();
            using var admin = new AdminAppService(sdk.Settings, fileEnryption: false, sdk.Authentication, sdk.UserManager, sdk.Configuration);

            MutableAdminModel model = await admin.GetModel();
            var users = model.Users!;
            IhcUser user = users.First();
            IhcUser editedUser = user with { Email = "edited@stub.invalid" };
            DNSServers editedDns = model.DnsServers! with { PrimaryDNS = "192.168.1.53" };
            model.Users = users.Where(other => other.Username != user.Username).Append(editedUser).ToHashSet();
            model.DnsServers = editedDns;

            Assert.That((await admin.Store(model)).ChangeCount, Is.EqualTo(2));

            MutableAdminModel stored = await admin.GetModel();
            Assert.That(stored.DnsServers, Is.EqualTo(editedDns));
            Assert.That(stored.Users!.Single(other => other.Username == user.Username).Email, Is.EqualTo(editedUser.Email));
        }

        [Test]
        public async Task Information_AggregatesTheDefaultScenario_WithNoEmptySection()
        {
            using var sdk = StubbedSdk.Start();
            using var information = new InformationAppService(sdk.Settings, sdk.Authentication, sdk.Configuration,
                sdk.Time, sdk.Controller, sdk.SmsModem);

            InformationModel model = await information.GetInformationModel();

            Assert.Multiple(() =>
            {
                foreach (PropertyInfo section in typeof(InformationModel).GetProperties().Where(property => property.CanRead))
                {
                    object? value = section.GetValue(model);
                    object? empty = section.PropertyType.IsValueType ? Activator.CreateInstance(section.PropertyType) : null;
                    Assert.That(value, Is.Not.EqualTo(empty).And.Not.EqualTo(string.Empty), section.Name);
                }
            });
        }

        [Test]
        public async Task Project_DownloadsTheDefaultProject_AndAnUploadedEditDownloadsBack()
        {
            using var sdk = StubbedSdk.Start();
            ProjectAppService projects = ProjectAppService.CreateWithControllerBridge(sdk.Settings);
            using var bridgeSession = (IDisposable)projects.BridgeAuthentication!;

            Project downloaded = await projects.DownloadFrom();
            Assert.That(downloaded.CustomerName, Is.EqualTo("Stub Customer"));

            ProjectInfoData info = downloaded.GetProjectInfo();
            ProjectInfoData edited = info with { Customer = info.Customer with { Name = "Edited Customer" } };
            ProjectApplyResult applied = projects.Apply(downloaded, projects.Commands.UpdateProjectInfo(downloaded, edited));

            Assert.That(await projects.UploadTo(applied.Project), Is.True);
            Assert.That((await projects.DownloadFrom()).CustomerName, Is.EqualTo("Edited Customer"));
        }

        [Test]
        public async Task Lab_InvokesOneOperation_ThroughTheDynamicEngine()
        {
            using var sdk = StubbedSdk.Start();
            var lab = new LabAppService(null);
            lab.Configure(sdk.Settings, [sdk.Controller]);
            LabAppService.ServiceItem service = lab.Services.Single();
            lab.SelectedOperation = service.OperationItems[service.LookupFirstOperationIndexByDisplayName(fullDisplayName: nameof(IControllerService.GetControllerState))];

            LabAppService.OperationResult result = await lab.DynCallSelectedOperation();

            Assert.That(result.RawResult, Is.EqualTo(ControllerState.Ready));
            Assert.That(sdk.Stub.Journal.Calls<IControllerService>(nameof(IControllerService.GetControllerState)), Has.Count.EqualTo(1));
        }
    }
}
