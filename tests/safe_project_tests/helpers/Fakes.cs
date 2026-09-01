using System;
using System.Linq;
using FakeItEasy;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Vis.Tests
{
    /// <summary>Shared fake-service arrangements (a peer of <see cref="Tree"/>/<see cref="TestData"/>).</summary>
    internal static class Fakes
    {
        /// <summary>
        /// The controller-bridge <see cref="ProjectAppService"/> recipe (a REAL app service per the test rules,
        /// with a fake <see cref="ICatalog"/> and a fixed clock). The bridge auto-authenticates before every
        /// controller call, so the default fake <see cref="IAuthenticationService"/> reports already-authenticated
        /// to keep that a no-op; pass <paramref name="auth"/> to exercise the authentication path itself, and
        /// <paramref name="clock"/> when a test pins re-stamped metadata. Pass <paramref name="validator"/> to
        /// substitute the executor every validation on the service runs through — the only way to reach a gate
        /// over the engine's fault channel, since no registered rule throws.
        /// </summary>
        /// <remarks>
        /// A caller that supplies no <paramref name="validator"/> is built through the SHIPPED public
        /// constructor, deliberately: that door is what most of this suite's bridge fixtures exercise, and
        /// routing them through an internal test-only factory instead would leave the public one uncovered.
        /// </remarks>
        public static ProjectAppService BridgeService(IControllerService controller, TimeProvider? clock = null,
            IAuthenticationService? auth = null, IWholeProjectValidator? validator = null)
        {
            if (auth is null)
            {
                auth = A.Fake<IAuthenticationService>();
                A.CallTo(() => auth.IsAuthenticated()).Returns(true);
            }
            return validator is null
                ? new ProjectAppService(TestSetup.Settings, A.Fake<ICatalog>(), Clock(clock), controller, auth)
                : ProjectAppService.CreateComposed(
                    TestSetup.Settings, A.Fake<ICatalog>(), Clock(clock), validator, controller, auth);
        }

        /// <summary>
        /// The same recipe with NO controller: a file-only service. Its reason to exist is
        /// <paramref name="validator"/> — see <see cref="BridgeService"/>.
        /// </summary>
        public static ProjectAppService FileService(IWholeProjectValidator validator, TimeProvider? clock = null) =>
            ProjectAppService.CreateComposed(TestSetup.Settings, A.Fake<ICatalog>(), Clock(clock), validator);

        /// <summary>A file-only service whose every validation reports <paramref name="run"/>, whatever it is handed.</summary>
        public static ProjectAppService FileServiceOver(StructuredValidationResult run) =>
            FileService(new StubValidator(run));

        /// <summary>The bridge recipe over a fixed run — <see cref="FileServiceOver"/> with a controller.</summary>
        public static ProjectAppService BridgeServiceOver(StructuredValidationResult run, IControllerService controller) =>
            BridgeService(controller, validator: new StubValidator(run));

        /// <summary>
        /// A run that found nothing and lost each of <paramref name="rules"/> to a crash — the state no shipped
        /// rule set can produce, since none of the registered rules throws.
        /// </summary>
        public static StructuredValidationResult FaultedRun(params string[] rules) =>
            new(EquatableArray<ValidationFinding>.Empty,
                EquatableArray.Create<InternalError>([.. rules.Select(RuleFailure)]));

        /// <summary>
        /// The fault a crashed rule produces, worded exactly as the executor's own report-and-continue net words
        /// it. One spelling of the <c>internal.rule-failed</c> sentence, so a fixture asserting on it is reading
        /// the catalogue's text rather than a retyped copy of it.
        /// </summary>
        public static InternalError RuleFailure(string rule) =>
            new(new ProblemCode("internal.rule-failed"),
                $"Valideringsreglen '{rule}' fejlede. Listen kan mangle fejl.",
                $"Rule '{rule}' threw during a validation pass; its findings are missing from the run.",
                InternalErrorOrigin.Sdk,
                $"Rule '{rule}' threw",
                DateTimeOffset.UnixEpoch);

        /// <summary>An executor that reports the given run whatever project it is handed.</summary>
        private sealed class StubValidator(StructuredValidationResult result) : IWholeProjectValidator
        {
            public StructuredValidationResult Validate(Project project, ValidationProfile profile) => result;
        }

        /// <summary>The fixed clock the recipes above share, so a test pinning metadata reads one date.</summary>
        private static TimeProvider Clock(TimeProvider? clock) =>
            clock ?? new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }
}
