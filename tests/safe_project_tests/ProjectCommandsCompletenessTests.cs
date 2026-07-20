using System;
using System.Linq;
using System.Reflection;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// R1 acceptance (T009): the <see cref="ProjectCommands"/> gateway is the <b>complete and exclusive</b>
    /// authoring vocabulary. Every concrete <see cref="ProjectCommand"/> in the SDK is reachable through exactly one
    /// published factory (D03), and <see cref="CompositeCommand"/> — composition infrastructure — is deliberately
    /// NOT (D04). A new command that ships without a factory, or a stray <c>CompositeCommand</c> factory, fails here.
    /// </summary>
    public class ProjectCommandsCompletenessTests
    {
        [Test]
        public void EveryConcreteCommand_ExceptCompositeCommand_IsReachableThroughAFactory()
        {
            Type commandBase = typeof(ProjectCommand);
            var concrete = commandBase.Assembly.GetTypes()
                .Where(t => commandBase.IsAssignableFrom(t) && !t.IsAbstract && t != typeof(CompositeCommand))
                .ToList();

            // A factory is a public instance method on ProjectCommands that returns a command type (nullable
            // reference annotations vanish at runtime, so `AddProduct?` and `AddProduct` share one System.Type).
            var factoryReturns = typeof(ProjectCommands)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.ReturnType)
                .ToHashSet();

            var unreachable = concrete.Where(t => !factoryReturns.Contains(t)).Select(t => t.Name).OrderBy(n => n).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(concrete, Is.Not.Empty, "sanity: the SDK assembly exposes concrete ProjectCommand types");
                Assert.That(unreachable, Is.Empty,
                    "every concrete ProjectCommand (except CompositeCommand) needs a ProjectCommands factory; missing: "
                    + string.Join(", ", unreachable));
                Assert.That(factoryReturns, Does.Not.Contain(typeof(CompositeCommand)),
                    "CompositeCommand is deliberately excluded from the published factory vocabulary (D04)");
            });
        }
    }
}
