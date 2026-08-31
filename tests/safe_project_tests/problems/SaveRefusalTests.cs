using System;
using System.IO;
using System.Linq;

using Ihc.App;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The six conditions that stop a project being written, each now refusing with an identity.
    ///
    /// <para><b>No posture changed.</b> All six already failed the save; what they gained is a code and a Danish
    /// sentence. Each test asserts the ENGLISH diagnostic is still present and unchanged — it was joined, not
    /// replaced — and the byte-fidelity round-trip suite is the control that nothing which saves today stopped
    /// saving.</para>
    ///
    /// <para><b>Two base types, one contract.</b> A failed write stays an <see cref="IOException"/> and a schema
    /// guard stays an <see cref="InvalidOperationException"/>, because that is what every existing caller already
    /// catches. The identity therefore rides on <see cref="IProblemCarrier"/>, which any base can carry, instead
    /// of forcing one common base and calling a breaking change an improvement.</para>
    ///
    /// <para><b>The operation is the CALLER's fact.</b> Four of these six sites are shared helpers that also run
    /// outside a save — at edit commit, at edit-session open, when a definition is built — so the refusal
    /// identity is passed in rather than hard-coded. A guard that named <c>io.save</c> unconditionally would name
    /// the wrong operation at two of its three callers.</para>
    /// </summary>
    [TestFixture]
    public sealed class SaveRefusalTests
    {
        private static ProblemCatalog Catalog => ProblemCatalog.Current;

        private static RefusedOperationException RefusedBySerializer(Project project) =>
            Assert.Throws<RefusedOperationException>(() => ProjectSerializer.Serialize(project))!;

        [Test]
        public void ANonLatin1AttributeIsRefusedAsAttrLatin1() =>
            AssertRefusal(RefusedBySerializer(Tree.MinimalProject(("icon", "€"))),
                SaveRefusalCodes.AttrLatin1, "ISO-8859-1",
                "Tegn kan ikke gemmes i attributten 'icon' på <utcs_project>.");

        [Test]
        public void AMissingRequiredAttributeIsRefusedAsAttrRequired()
        {
            Project project = new(new ProjectElement("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2")], []));

            AssertRefusal(RefusedBySerializer(project), SaveRefusalCodes.AttrRequired, "#REQUIRED",
                "Den påkrævede attribut 'last_unique_id' mangler på <utcs_project>.");
        }

        [Test]
        public void AnUndeclaredAttributeIsRefusedAsAttrUndeclared() =>
            AssertRefusal(RefusedBySerializer(Tree.MinimalProject(("no_such_attribute", "x"))),
                SaveRefusalCodes.AttrUndeclared, "is not declared",
                "Ukendt attribut 'no_such_attribute' på <utcs_project>.");

        [Test]
        public void AnUndeclaredElementTypeIsRefusedAsElementUndeclared()
        {
            Project project = new(Tree.MinimalProject().Root with
            {
                Children = [new ProjectElement("no_such_element_type", null, [], [])],
            });

            AssertRefusal(RefusedBySerializer(project), SaveRefusalCodes.ElementUndeclared, "No schema for",
                "Ukendt elementtype <no_such_element_type>.");
        }

        /// <summary>
        /// The write self-check, at its owner. Handing <see cref="ProjectRoundTripVerifier.Verify"/> bytes that
        /// belong to a DIFFERENT project is the condition itself — bytes that do not re-parse to the model — and
        /// it is the only way to reach the site without a serializer defect to trigger it, which is exactly what
        /// the check exists to catch and therefore cannot be relied on to exist.
        /// </summary>
        [Test]
        public void BytesThatDoNotReproduceTheProjectAreRefusedAsSaveRoundtripMismatch()
        {
            byte[] other = ProjectSerializer.Serialize(Tree.MinimalProject(("icon", "_0x9")));

            RefusedOperationException refusal = Assert.Throws<RefusedOperationException>(
                () => ProjectRoundTripVerifier.Verify(Tree.MinimalProject(("icon", "_0x8")), other))!;

            AssertRefusal(refusal, SaveRefusalCodes.RoundTripMismatch, "Serialize/re-parse mismatch");
        }

        /// <summary>
        /// The atomic writer, at its owner, and STILL an <see cref="IOException"/> — a directory sitting on the
        /// target name fails the rename the same way on Windows and on POSIX. The existing save tests catch
        /// <see cref="IOException"/>; this one pins that they still can while the refusal now also carries a code.
        /// </summary>
        [Test]
        public async Task AnUnwritableTargetIsRefusedAsSaveTargetUnwritable()
        {
            var app = new ProjectAppService(new IhcSettings());
            string dir = Path.Combine(Path.GetTempPath(), "ihc-saverefusal-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "occupied.vis");
            Directory.CreateDirectory(path);
            try
            {
                RefusedWriteException refusal = Assert.ThrowsAsync<RefusedWriteException>(
                    async () => await app.Save(Tree.MinimalProject(), path, ProjectSaveOptions.PreserveExistingMetadata))!;

                Assert.Multiple(() =>
                {
                    Assert.That(refusal, Is.InstanceOf<IOException>(), "a failed write is still an IOException");
                    Assert.That(refusal.Problems!.Cause.Code, Is.EqualTo(SaveRefusalCodes.TargetUnwritable.Cause));
                    Assert.That(refusal.InnerException, Is.Not.Null, "the platform's own failure is kept");
                    Assert.That(Directory.GetFiles(dir), Is.Empty, "the refusal leaves no temp-file litter");
                });
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// The control every one of the six needs: a well-formed project still writes. A refusal that also caught
        /// a good project would be a far worse defect than the ones it prevents.
        /// </summary>
        [Test]
        public void AWellFormedProjectStillSerializes()
        {
            byte[] bytes = ProjectSerializer.Serialize(Tree.MinimalProject());

            Assert.That(bytes, Is.Not.Empty);
        }

        /// <summary>
        /// Every cause keeps its BARE published id and is a governed catalogue entry; the operation is the dotted
        /// one. A dotted <c>io.save-attr-latin1</c> anywhere would mean a published id had been renamed.
        /// </summary>
        [Test]
        public void EveryCauseKeepsItsPublishedIdAndIsGoverned()
        {
            Assert.Multiple(() =>
            {
                foreach (RefusalIdentity identity in SaveRefusalCodes.All)
                {
                    Assert.That(identity.Operation, Is.EqualTo(OperationCodes.Save), identity.Cause.Value);
                    Assert.That(identity.Cause.Family, Is.EqualTo(ProblemFamily.Validation), identity.Cause.Value);
                    Assert.That(identity.Cause.Value, Does.Not.StartWith("io."), identity.Cause.Value);
                    Assert.That(Catalog.TryGet(identity.Cause, out ProblemCatalogEntry entry), Is.True,
                        identity.Cause.Value);
                    Assert.That(entry.MessageTemplate, Is.Not.Empty, identity.Cause.Value);
                }

                Assert.That(Catalog.TryGet(OperationCodes.Save, out ProblemCatalogEntry head), Is.True);
                Assert.That(head.MessageTemplate, Is.EqualTo(OperationCodes.SaveLabel));
            });
        }

        /// <summary>
        /// The catalogue's Danish label and the sentence a refusing site hands over must be the same words. Three
        /// of the six sites sit below the validation engine and cannot read the catalogue, so this is what keeps
        /// the two in step — the same guard T035 put on the load family, applied to this one rather than assumed.
        /// </summary>
        [Test]
        public void EverySitesLabelIsTheCataloguesTemplate()
        {
            Assert.Multiple(() =>
            {
                foreach (RefusalIdentity identity in SaveRefusalCodes.All)
                {
                    Catalog.TryGet(identity.Cause, out ProblemCatalogEntry entry);
                    Assert.That(identity.CauseLabel, Is.EqualTo(entry.MessageTemplate), identity.Cause.Value);
                }
            });
        }

        /// <summary>
        /// The two operation heads are ONE code each, not one per raising layer. <c>io.load</c> is raised only
        /// from the reader, but <c>io.save</c> is raised from three namespaces with no dependency between them —
        /// which is why the heads live in the contract namespace and the load family's member forwards to it.
        /// </summary>
        [Test]
        public void TheOperationHeadsAreSharedNotDuplicated()
        {
            Assert.Multiple(() =>
            {
                Assert.That(LoadRefusalCodes.Operation, Is.EqualTo(OperationCodes.Load));
                Assert.That(OperationCodes.Load.Value, Is.EqualTo("io.load"));
                Assert.That(OperationCodes.Save.Value, Is.EqualTo("io.save"));
                Assert.That(OperationCodes.Load, Is.Not.EqualTo(OperationCodes.Save));
            });
        }

        /// <summary>
        /// The shared guard says the same thing whether or not the caller supplied an identity. The uncoded
        /// overload exists because edit commit and the insert transform refuse operations that have no head yet,
        /// and the whole point of one guard serving both is that the layers cannot be read as disagreeing — so a
        /// diverging message here would be the defect, not the missing code.
        /// </summary>
        [Test]
        public void TheCodedAndUncodedGuardsCarryTheSameEnglishSentence()
        {
            ProjectElement element = new("utcs_project", null, [("no_such_attribute", "x")], []);
            ElementSchema schema = ProjectSchemaView.RegistryOnly.Get("utcs_project");

            InvalidOperationException uncoded = Assert.Throws<InvalidOperationException>(
                () => SchemaGuards.GuardNoUnknownAttributes(element, schema))!;
            RefusedOperationException coded = Assert.Throws<RefusedOperationException>(
                () => SchemaGuards.GuardNoUnknownAttributes(element, schema, SaveRefusalCodes.AttrUndeclared))!;

            Assert.Multiple(() =>
            {
                Assert.That(coded.Message, Is.EqualTo(uncoded.Message));
                Assert.That(uncoded, Is.Not.InstanceOf<IProblemCarrier>(),
                    "the uncoded overload carries no identity — it does not carry a wrong one");
            });
        }

        /// <summary>
        /// Every carrier answers the same question the same way, whatever it derives from. This is what makes
        /// <c>catch (Exception ex) when (ex is IProblemCarrier c)</c> a complete catch rather than a hopeful one.
        /// </summary>
        [Test]
        public void EveryCarrierExposesItsChainThroughTheOneInterface()
        {
            IProblemCarrier[] carriers =
            [
                new RefusedOperationException(SaveRefusalCodes.AttrLatin1, "diagnostic"),
                new RefusedWriteException(SaveRefusalCodes.TargetUnwritable, "diagnostic"),
                new ProjectFormatException(LoadRefusalCodes.Empty, "diagnostic"),
            ];

            Assert.Multiple(() =>
            {
                foreach (IProblemCarrier carrier in carriers)
                {
                    Assert.That(carrier.Problems, Is.Not.Null, carrier.GetType().Name);
                    Assert.That(carrier.Problems!.Operation.Code.Value, Does.StartWith("io."),
                        carrier.GetType().Name + ": the operation is the dotted one");
                    Assert.That(carrier.Problems.Cause.Code.Value, Does.Not.StartWith("io."),
                        carrier.GetType().Name + ": the cause keeps its published id");
                }
            });
        }

        /// <param name="refusal">The exception the serializer raised.</param>
        /// <param name="identity">The registry member, carrying the TEMPLATE.</param>
        /// <param name="diagnosticFragment">A fragment of the English sentence for the log.</param>
        /// <param name="bound">
        /// The Danish sentence as the user reads it. Null for a row whose label declares no slots, where the
        /// template IS the sentence; a row that surfaces its arguments passes the bound form, so this asserts the
        /// raising site actually filled the slots instead of showing an installer a literal <c>{tag}</c>.
        /// </param>
        private static void AssertRefusal(
            RefusedOperationException refusal,
            RefusalIdentity identity,
            string diagnosticFragment,
            string? bound = null)
        {
            Assert.Multiple(() =>
            {
                Assert.That(refusal.Problems, Is.Not.Null, "a refused save carries its operation and its cause");
                Assert.That(refusal.Problems!.Operation.Code, Is.EqualTo(OperationCodes.Save));
                Assert.That(refusal.Problems.Cause.Code, Is.EqualTo(identity.Cause));
                Assert.That(refusal.Problems.Cause.Message, Is.EqualTo(bound ?? identity.CauseLabel),
                    "the Danish sentence the user reads");
                Assert.That(refusal.Problems.Cause.Message, Does.Not.Contain("{"),
                    "and no slot reaches the user still spelled as its own placeholder");
                Assert.That(refusal.Message, Does.Contain(diagnosticFragment).IgnoreCase,
                    "and the English diagnostic is unchanged — it was joined, not replaced");
            });
        }
    }
}
