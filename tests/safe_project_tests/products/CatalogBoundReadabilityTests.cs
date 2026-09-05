using System.Linq;
using System.Threading.Tasks;

using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// A catalog bound the engine cannot read must not become "no bound".
    ///
    /// <para><c>ParseBound</c> answered <c>null</c> for a <c>minimum</c>/<c>maximum</c> that fails to parse, and
    /// <c>null</c> is the same value that means "the catalog declares none". Both readers of
    /// <see cref="ElementView.DeclaredBounds"/> then saw an unconstrained field — so a limit the catalog STATES
    /// disappeared silently, on a path that writes to a <c>.vis</c>.</para>
    ///
    /// <para>The three states are now distinct: no declaration, a readable one, and one that is declared and
    /// unreadable. The last is a defect in the DEFINITION file rather than in the project, so the finding lives
    /// with the other catalog-definition rows, and the dialog stops offering the field at all rather than
    /// offering it without the limit the catalog asked for.</para>
    /// </summary>
    [TestFixture]
    public sealed class CatalogBoundReadabilityTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        /// <summary>The placed SMS modem, whose PIN field is the one shipped product field with declared bounds.</summary>
        private static async Task<(Project Project, ElementId ProductId, DialogDescriptorField Pin)> WithModem()
        {
            (ProjectDocumentSession session, ElementId id, DialogDescriptorField pin) = await PlacedModem.Open();
            return (session.Current!, id, pin);
        }

        /// <summary>The same project with one attribute overwritten on one element — what a hand edit or a
        /// definition file carrying a typo produces.</summary>
        private static Project With(Project project, ElementId target, string attribute, string value) =>
            project with { Root = Rewrite(project.Root, target, attribute, value) };

        private static ProjectElement Rewrite(ProjectElement element, ElementId target, string attribute, string value) =>
            element.Id == target
                ? element.WithAttribute(attribute, value)
                : element with { Children = [.. element.Children.Select(c => Rewrite(c, target, attribute, value))] };

        // ── the read view: three states, not two ────────────────────────────────────────────────────

        [Test]
        public async Task DeclaredBoundsTellsAnUnreadableBoundApartFromAnAbsentOne()
        {
            (Project project, ElementId productId, DialogDescriptorField pin) = await WithModem();
            ProjectElement declared = project.Root.FindDescendantOrSelf(e => e.Id == pin.Target)!;

            Project broken = With(project, pin.Target, "minimum", "x");
            ProjectElement unreadable = broken.Root.FindDescendantOrSelf(e => e.Id == pin.Target)!;

            ProjectElement none = project.Root.FindDescendantOrSelf(e => e.Id == productId)!;

            Assert.Multiple(() =>
            {
                Assert.That(project.View(declared).DeclaredBounds.Unreadable, Is.False);
                Assert.That(project.View(declared).DeclaredBounds.Minimum, Is.EqualTo(0));
                Assert.That(project.View(declared).DeclaredBounds.Maximum, Is.EqualTo(9999));

                Assert.That(broken.View(unreadable).DeclaredBounds.Unreadable, Is.True,
                    "a bound the catalog states but the engine cannot read is not an absent bound");
                Assert.That(broken.View(unreadable).DeclaredBounds.Minimum, Is.Null,
                    "and there is no number to offer for it");

                Assert.That(project.View(none).DeclaredBounds.Unreadable, Is.False,
                    "an element declaring no bound at all is not unreadable — it is unbounded");
                Assert.That(project.View(none).DeclaredBounds, Is.EqualTo(default(DeclaredNumericBounds)));
            });
        }

        // ── the write path: the field is not offered unbounded ──────────────────────────────────────

        [Test]
        public async Task AFieldWhoseDeclaredBoundCannotBeReadIsNotOfferedForEditing()
        {
            (Project project, ElementId productId, DialogDescriptorField pin) = await WithModem();
            Project broken = With(project, pin.Target, "maximum", "9999x");

            DialogDescriptorField offered = App.GetProductDialog(broken, productId)
                .AllFields.Single(f => f.AutomationId == pin.AutomationId);

            Assert.Multiple(() =>
            {
                Assert.That(offered.ReadOnly, Is.True,
                    "a bound the catalog states must not become 'no limit' on a path that writes to a .vis");
                Assert.That(App.GetProductDialog(project, productId)
                    .AllFields.Single(f => f.AutomationId == pin.AutomationId).ReadOnly, Is.False,
                    "and a readable bound leaves the field editable, as it always was");
            });
        }
    }
}
