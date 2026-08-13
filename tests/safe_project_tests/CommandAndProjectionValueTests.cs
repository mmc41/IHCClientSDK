using System.Collections.Generic;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Step 5's contract for the public read projections and the command records: two independently materialized
    /// values with equal contents are equal and hash equal, order and element differences are not, and a command
    /// cannot change after it is minted even if the caller keeps mutating the list it was built from.
    /// </summary>
    public class CommandAndProjectionValueTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        // ---- Read projections compare by nested content ----

        [Test]
        public void IndependentlyMaterializedReadModels_WithEqualContent_AreEqualAndHashEqually()
        {
            DataTablesModel a = new([new DataTableView("Typer", ["Til", "Fra"])], [new UserText("_0x1", "Stue")]);
            DataTablesModel b = new([new DataTableView("Typer", ["Til", "Fra"])], [new UserText("_0x1", "Stue")]);

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b), "nested rows compare by content, not by backing-array identity");
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            });
        }

        [Test]
        public void ReadModels_DifferingInRowOrder_AreNotEqual()
        {
            DataTablesModel ascending = new([new DataTableView("Typer", ["Til", "Fra"])], []);
            DataTablesModel reversed = new([new DataTableView("Typer", ["Fra", "Til"])], []);

            Assert.That(ascending, Is.Not.EqualTo(reversed), "row order is content");
        }

        [Test]
        public void ReadModels_DifferingInOneNestedElement_AreNotEqual()
        {
            ModuleAddressMap a = new([new ModuleAddressEntry("1.1", "Lampe", "T1")], []);
            ModuleAddressMap b = new([new ModuleAddressEntry("1.1", "Lampe", "T2")], []);

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void EmptyProjections_EqualAnIndependentlyBuiltEmptyOne()
        {
            // `default` reads as empty through the wrapper, so the shared Empty singletons and a freshly built
            // empty model are the same value rather than merely behaving alike.
            Assert.Multiple(() =>
            {
                Assert.That(DataTablesModel.Empty, Is.EqualTo(new DataTablesModel([], [])));
                Assert.That(ModuleAddressMap.Empty, Is.EqualTo(new ModuleAddressMap([], [])));
            });
        }

        // ---- Commands compare by content and snapshot their inputs ----

        [Test]
        public void IndependentlyBuiltCommands_WithEqualContent_AreEqualAndHashEqually()
        {
            UpdateEnumStates a = new("Type", ["A", "B"]) { Relabels = [(new ElementId(1, 2), "Ny")] };
            UpdateEnumStates b = new("Type", ["A", "B"]) { Relabels = [(new ElementId(1, 2), "Ny")] };

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
                // Relabels is an init member with a default — the kind a handwritten Equals forgets.
                Assert.That(a, Is.Not.EqualTo(new UpdateEnumStates("Type", ["A", "B"])), "Relabels counts");
                Assert.That(a, Is.Not.EqualTo(new UpdateEnumStates("Type", ["B", "A"]) { Relabels = a.Relabels }),
                    "state order counts");
            });
        }

        [Test]
        public void CompositeCommand_ComparesByItsParts()
        {
            CompositeCommand a = new("Flyt", [new RenameLocality(new ElementId(1, 2), "Stue", "")]);
            CompositeCommand b = new("Flyt", [new RenameLocality(new ElementId(1, 2), "Stue", "")]);
            CompositeCommand other = new("Flyt", [new RenameLocality(new ElementId(1, 2), "Kontor", "")]);

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b), "independently built part lists compare by content");
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
                Assert.That(a, Is.Not.EqualTo(other));
            });
        }

        /// <summary>
        /// The gateway snapshots caller-owned input. Without it a command would keep a live reference to the
        /// list the GUI built it from, so a later edit of that list would silently rewrite an already-applied
        /// history entry.
        /// </summary>
        [Test]
        public async Task GatewayMintedCommand_DoesNotSeeLaterMutationOfTheCallersList()
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            List<string> callerOwned = ["Til", "Fra"];

            AddStandaloneEnumType command = App.Commands.AddStandaloneEnumType(project, "Type", callerOwned);
            callerOwned.Add("Ukendt");
            callerOwned[0] = "Ændret";

            Assert.Multiple(() =>
            {
                Assert.That(command.States.Count, Is.EqualTo(2), "the later Add must not reach the command");
                Assert.That(command, Is.EqualTo(new AddStandaloneEnumType("Type", ["Til", "Fra"])),
                    "the command still holds exactly what it was minted with");
            });
        }
    }
}
