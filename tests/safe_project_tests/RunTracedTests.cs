using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// M2/D02 (T012): <see cref="ProjectAppService"/>'s public methods run inside
    /// <c>AppServiceBase.RunTraced</c>, so each emits an activity named <c>&lt;ServiceType&gt;.&lt;method&gt;</c>
    /// and — when the body throws — tags the activity's status <see cref="ActivityStatusCode.Error"/>. That is the
    /// StartActivity + try/catch + SetError scaffold, defined once, instead of copied into every method body.
    /// </summary>
    public class RunTracedTests
    {
        [Test]
        public void ProjectAppServiceMethods_EmitNamedActivity_AndTagErrorWhenBodyThrows()
        {
            var exported = new List<(string Name, ActivityStatusCode Status)>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == Telemetry.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => exported.Add((activity.OperationName, activity.Status)),
            };
            ActivitySource.AddActivityListener(listener);

            var app = new ProjectAppService(TestSetup.Settings);

            // A successful call emits an activity named for the method.
            app.GetAvailableProducts();

            // A body that throws (a missing file inside Load's RunTracedAsync) tags the activity Error and rethrows.
            Assert.CatchAsync(async () => await app.Load("does-not-exist-" + nameof(RunTracedTests) + ".vis"));

            Assert.Multiple(() =>
            {
                Assert.That(exported.Any(a => a.Name == "ProjectAppService.GetAvailableProducts"), Is.True,
                    "a successful method emits an activity named <service>.<method>");
                (string Name, ActivityStatusCode Status) load = exported.Last(a => a.Name == "ProjectAppService.Load");
                Assert.That(load.Status, Is.EqualTo(ActivityStatusCode.Error),
                    "a throwing body is tagged Error via RunTraced's SetError, not left Unset");
            });
        }
    }
}
