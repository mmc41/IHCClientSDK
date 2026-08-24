using System;
using System.Linq;
using System.Threading.Tasks;

using Ihc.Vis.Products;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T043: the three doors that used to hand back a bare <c>bool</c> or an empty result now hand back a CODED
    /// PROBLEM — identity plus the Danish sentence — so the message a user reads is the SDK's, not one a frontend
    /// invented for a rule it does not own.
    ///
    /// <para>The three: no library function block with that master type, no catalog product with that identifier,
    /// and the at-most-one-modem rule. Each looked host-owned in the GUI only because the SDK said nothing more
    /// than "no" (D7's evidence table, last three rows).</para>
    ///
    /// <para>Note what is NOT here: a rule about whether the GUI shows them. The presentation path is the shell's
    /// (T040), and which sites use which code is T042's per-site ruling. What this suite pins is that the SDK has
    /// something to say at all, and that what it says agrees with its catalogue entry.</para>
    /// </summary>
    public class CodedMintRefusalTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        [Test]
        public async Task NoLibraryBlockWithThatMasterType_RefusesWithACodeAndItsOwnDanishSentence()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;

            bool minted = app.Commands.TryAddFunctionBlock(project, locality, "not-a-real-block",
                out AddFunctionBlock? command, out Problems.Problem? refusal);

            Assert.Multiple(() =>
            {
                Assert.That(minted, Is.False);
                Assert.That(command, Is.Null, "there is nothing to apply");
                Assert.That(refusal!.Code, Is.EqualTo(EditRefusalCodes.LibraryBlockMissing));
                Assert.That(refusal.Message, Does.Contain("not-a-real-block"),
                    "the master type asked for is IN the sentence, so the user can see what was not found");
                Assert.That(refusal.Message, Does.Not.Contain("{"), "and it arrives bound, not as a template");
                Assert.That(refusal.Arguments.Select(a => a.Name), Is.EqualTo(new[] { "masterType" }).AsCollection,
                    "the datum is also carried structurally, for a log or a filter");
                Assert.That(refusal.Diagnostic, Is.Not.Null.And.Not.Empty,
                    "the English engine sentence travels beside the Danish one, never inside it");
            });
        }

        [Test]
        public async Task ALibraryBlockThatExists_MintsTheCommandAndRefusesNothing()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            string masterType = app.GetAvailableFunctionBlocks().First().MasterType;

            bool minted = app.Commands.TryAddFunctionBlock(project, locality, masterType,
                out AddFunctionBlock? command, out Problems.Problem? refusal);

            Assert.Multiple(() =>
            {
                Assert.That(minted, Is.True);
                Assert.That(refusal, Is.Null);
                Assert.That(command, Is.EqualTo(app.Commands.AddFunctionBlock(project, locality, masterType)),
                    "the two shapes are one implementation — the Try door adds the reason, not a second lookup");
            });
        }

        [Test]
        public void NoCatalogProductWithThatIdentifier_RefusesWithACode()
        {
            ProjectAppService app = App;

            bool resolved = app.TryResolveProduct("_0xdeadbeef", null,
                out ProductDefinition? product, out Problems.Problem? refusal);

            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.False);
                Assert.That(product, Is.Null);
                Assert.That(refusal!.Code, Is.EqualTo(EditRefusalCodes.CatalogProductMissing));
                Assert.That(refusal.Message, Does.Contain("_0xdeadbeef"));
                Assert.That(refusal.Message, Does.Not.Contain("{"));
            });
        }

        [Test]
        public void AProductThatExists_ResolvesAndRefusesNothing()
        {
            ProjectAppService app = App;
            ProductDefinition any = app.GetAvailableProducts()
                .First(p => app.ResolveProduct(p.ProductIdentifier) is not null);   // an unambiguous identifier

            bool resolved = app.TryResolveProduct(any.ProductIdentifier, null,
                out ProductDefinition? product, out Problems.Problem? refusal);

            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.True);
                Assert.That(refusal, Is.Null);
                Assert.That(product!.ProductIdentifier, Is.EqualTo(any.ProductIdentifier));
            });
        }

        /// <summary>
        /// The at-most-one-modem rule, from both sides: open in a modem-less project, refused with its code once a
        /// modem is placed. The rule was always the SDK's; what it lacked was anything a caller could act on
        /// beyond a bool.
        /// </summary>
        [Test]
        public async Task TheSecondModem_IsRefusedWithACodeAndTheFirstOneIsNot()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            ProductDefinition modem = app.GetAvailableProducts().First(p => ProductClassifier.IsModem(p.Body.Tag));

            Problems.Problem? beforeAny = app.Commands.ModemLimitRefusal(project, modem.ProductIdentifier);
            Project withModem = app.Apply(project, app.Commands.AddProduct(project, locality, modem)).Project!;
            Problems.Problem? afterOne = app.Commands.ModemLimitRefusal(withModem, modem.ProductIdentifier);

            Assert.Multiple(() =>
            {
                Assert.That(beforeAny, Is.Null, "the gate is open while the project holds no modem");
                Assert.That(afterOne!.Code, Is.EqualTo(EditRefusalCodes.ModemLimit));
                Assert.That(afterOne.Message, Does.Contain("ét modem").And.Contain("Fjern det eksisterende modem"),
                    "the rule AND its remedy, which is the registered difference from the reference application");
                Assert.That(afterOne.Arguments, Is.Empty, "the rule needs no datum to state itself");
            });
        }

        /// <summary>
        /// The agreement assertion every family in this run carries: the sentence the SITE produces is the
        /// catalogue TEMPLATE bound with the problem's own arguments. It is checked rather than assumed because the
        /// session layer cannot read the catalogue — the layer rules forbid it — so the words are necessarily
        /// written in two places and only a test can keep them the same.
        /// </summary>
        [Test]
        public void EachMintRefusalsSentenceIsItsCataloguesTemplateBound()
        {
            (Problems.Problem Problem, string Code)[] cases =
            [
                (EditRefusalProblems.LibraryBlockMissing("1.2.03"), "edit.library-block-missing"),
                (EditRefusalProblems.CatalogProductMissing("_0x4304"), "edit.catalog-product-missing"),
                (EditRefusalProblems.ModemLimit(), "edit.modem-limit"),
            ];

            Assert.Multiple(() =>
            {
                foreach ((Problems.Problem problem, string code) in cases)
                {
                    Assert.That(problem.Code.Value, Is.EqualTo(code));
                    Assert.That(Validation.ProblemCatalog.Current.TryGet(problem.Code,
                        out Validation.ProblemCatalogEntry entry), Is.True, $"{code} is governed");
                    Assert.That(entry.BindTemplate(problem), Is.EqualTo(problem.Message),
                        $"{code}: the site's sentence and the entry's template are the same words");
                    Assert.That(entry.Slots.Select(s => s.Name),
                        Is.EqualTo(problem.Arguments.Select(a => a.Name)).AsCollection,
                        $"{code}: the declared slots are exactly the arguments the factory binds");
                }
            });
        }
    }
}
