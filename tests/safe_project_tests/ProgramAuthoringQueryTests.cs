using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Programs;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// D-1: which program operations an armed variable may be given inside a container is the SDK's question, not
    /// the GUI's. The three facts here are the ones a frontend used to derive from the raw method tables — the pin's
    /// operator family, the commands that need a bool OUTPUT, and whether a case may switch on this variable — and
    /// each is asked of the facade instead.
    /// <para>
    /// The offers are descriptors, not menu items: an identifier a caller can key on, the vendor method token the
    /// matching command takes, and the persisted name/note templates. Nothing about presentation is decided here.
    /// </para>
    /// </summary>
    public class ProgramAuthoringQueryTests : SessionCommandFixture
    {
        private static ProjectElement Block(Project project, string name) =>
            project.Root.Descendants().First(e => e.Tag == "functionblock" && e.GetAttribute("name") == name);

        private static ElementId ContainerIn(Project project, string blockName, string tag) =>
            Block(project, blockName).DescendantsAndSelf().First(e => e.Tag == tag).Id!.Value;

        /// <summary>Adds a variable of <paramref name="tag"/> to the block's variables section and returns its id.</summary>
        private static ElementId AddVariable(IProjectDocument document, string blockName, string tag, string name)
        {
            ProjectElement section = Block(document.Current!, blockName).Children
                .First(c => c.Tag is "variables" or "settings");
            document.Apply(App.Commands.AddVariable(document.Current!, section.Id!.Value, tag, name)!);
            return Block(document.Current!, blockName).Descendants()
                .First(e => e.Tag == tag && e.GetAttribute("name") == name).Id!.Value;
        }

        private static ElementId EnumNamed(Project project, string name) =>
            project.Root.Descendants()
                .First(e => e.Tag == "resource_enum" && e.GetAttribute("name") == name).Id!.Value;

        /// <summary>
        /// The offers ARE the grid: for a numeric target, the operators offered are exactly the operator/operand
        /// cells the SDK's arithmetic table declares authorable. Asserted here rather than in the shell, so the
        /// property holds for every caller of the query and not only for the menu one frontend renders.
        /// </summary>
        [Test]
        public async Task ArithmeticOffersAreExactlyTheGridsLiveCells()
        {
            (IProjectDocument document, ElementId counter, ElementId actions) = await CounterWithNumericOperand();
            Project project = document.Current!;

            var offered = App.GetProgramOperations(project, actions, counter)
                .Where(o => o.Kind == ProgramOperationKind.Arithmetic && o.NeedsOperand)
                .SelectMany(o => o.Operands.Select(operand => (o.OperatorSymbol, operand.Method)))
                .ToHashSet();

            // The same cells, read straight off the table the offers are built from.
            var live = new HashSet<(string?, string)>();
            foreach (ProjectElement operand in project.Root.Descendants()
                         .Where(e => ProgramMethodCatalog.NumericVariableTags.Contains(e.Tag) && e.Id is not null))
            {
                foreach (ProgramMethod op in ProgramMethodCatalog.Arithmetic)
                    if (ProgramMethodCatalog.ArithmeticToken(op.OperatorSymbol!, "resource_counter", operand.Tag) is { } token)
                        live.Add((op.OperatorSymbol, token));
            }

            Assert.Multiple(() =>
            {
                Assert.That(live, Is.Not.Empty, "the grid really has live cells for a counter target");
                Assert.That(offered, Is.EquivalentTo(live),
                    "every live cell is offered, and nothing dead is");
            });
        }

        /// <summary>
        /// The published family is TOTAL over the engine's own classification: every <c>ProgramPinType</c> maps to a
        /// distinct <see cref="ProgramPinFamily"/>. A family added to the engine without a counterpart here would
        /// otherwise reach a caller as some other family's operations, which is precisely the drift the enum
        /// replaced a stringified name to prevent.
        /// </summary>
        [Test]
        public void EveryEngineePinTypeHasItsOwnPublishedFamily()
        {
            var mapped = System.Enum.GetValues<ProgramPinType>()
                .ToDictionary(type => type, ProgramAuthoring.FamilyOf);

            Assert.Multiple(() =>
            {
                Assert.That(mapped.Values, Is.Unique, "two pin types must not collapse onto one published family");
                Assert.That(mapped.Values.Distinct().Count(),
                    Is.EqualTo(System.Enum.GetValues<ProgramPinFamily>().Length),
                    "every published family is reachable, and every pin type publishes one");
            });
        }

        /// <summary>A counter target with one numeric operand beside it, on a commands container — the arrangement
        /// every arithmetic assertion needs, and the only one they share.</summary>
        private static async Task<(IProjectDocument Document, ElementId Counter, ElementId Actions)> CounterWithNumericOperand()
        {
            Project project = await Load("project2-CustomBlock.vis");
            IProjectDocument document = Session(project);
            ElementId counter = AddVariable(document, "Custom blok", "resource_counter", "Tæller");
            AddVariable(document, "Custom blok", "resource_integer", "Tal");
            return (document, counter, ContainerIn(document.Current!, "Custom blok", "actions"));
        }

        private static ElementId AnyOutputPin(Project project) =>
            project.Root.Descendants().First(e => e.IsOutputPin).Id!.Value;

        /// <summary>Pin classification: a timer pin is offered the TIMER events, never the bool ones.</summary>
        [Test]
        public async Task EventOffers_FollowTheArmedPinsOperatorFamily()
        {
            Project project = await Load("project2-CustomBlock.vis");
            IProjectDocument document = Session(project);
            ElementId timer = AddVariable(document, "Custom blok", "resource_timer", "Ur");
            ElementId events = ContainerIn(document.Current!, "Custom blok", "events");

            var offers = App.GetProgramOperations(document.Current!, events, timer);

            Assert.Multiple(() =>
            {
                Assert.That(offers.Select(o => o.Method), Is.EquivalentTo(new[] { "_0xa", "_0x9b" }),
                    "a timer offers its own two triggers, not the bool family's six");
                Assert.That(offers.Select(o => o.Kind), Has.All.EqualTo(ProgramOperationKind.Event));
                Assert.That(offers.Select(o => o.Id), Is.Unique, "an offer's id is a caller's key, so it identifies one offer");
            });
        }

        /// <summary>
        /// The output-only rule: the toggle command sets an output to its opposite, so it is offered on a bool
        /// OUTPUT pin and withheld from a variable that has no output to drive.
        /// </summary>
        [Test]
        public async Task ToggleIsOfferedOnlyOnABoolOutputPin()
        {
            Project project = await Load("project2-CustomBlock.vis");
            IProjectDocument document = Session(project);
            ElementId output = AnyOutputPin(document.Current!);
            ElementId flag = AddVariable(document, "Custom blok", "resource_flag", "Flag");
            ElementId actions = ContainerIn(document.Current!, "Custom blok", "actions");

            var onOutput = App.GetProgramOperations(document.Current!, actions, output);
            var onFlag = App.GetProgramOperations(document.Current!, actions, flag);

            Assert.Multiple(() =>
            {
                Assert.That(onOutput.Select(o => o.Method), Does.Contain("_0x23"), "an output pin may be toggled");
                Assert.That(onFlag.Select(o => o.Method), Does.Not.Contain("_0x23"),
                    "a non-output variable is never offered the toggle");
                Assert.That(onFlag.Select(o => o.Method), Does.Contain("_0xa"),
                    "and it still gets the ordinary bool commands");
            });
        }

        /// <summary>
        /// Case eligibility, and that it is the SAME rule the command path enforces: the offer appears exactly when
        /// <c>AddCase</c> would be allowed, so a menu built from the offers cannot present a case the session refuses.
        /// </summary>
        [Test]
        public async Task CaseIsOfferedExactlyWhenTheCommandPathAllowsIt()
        {
            Project project = await Load("project2-CustomBlock.vis");
            IProjectDocument document = Session(project);
            ElementId enumerator = AddVariable(document, "Custom blok", "resource_enum", "Tilstand");
            ElementId flag = AddVariable(document, "Custom blok", "resource_flag", "Flag");
            ElementId actions = ContainerIn(document.Current!, "Custom blok", "actions");

            bool offeredForEnum = App.GetProgramOperations(document.Current!, actions, enumerator)
                .Any(o => o.Kind == ProgramOperationKind.Case);
            bool offeredForFlag = App.GetProgramOperations(document.Current!, actions, flag)
                .Any(o => o.Kind == ProgramOperationKind.Case);

            Assert.Multiple(() =>
            {
                Assert.That(offeredForEnum, Is.True, "an enumerator is an eligible switch");
                Assert.That(offeredForFlag, Is.False, "a flag is not");
                Assert.That(document.CanApply(App.Commands.AddCase(document.Current!, actions, enumerator)).Ok,
                    Is.EqualTo(offeredForEnum), "the offer and the command gate answer the same question");
                Assert.That(document.CanApply(App.Commands.AddCase(document.Current!, actions, flag)).Ok,
                    Is.EqualTo(offeredForFlag), "for both answers");
            });
        }

        /// <summary>
        /// The third container kind: a conditions group offers the PREDICATES of the armed pin's family. Asserted
        /// beside an enumerator, because the family split is what makes the query more than a constant list — a
        /// bool is tested by its own four operators and an enumerator by its own.
        /// </summary>
        [Test]
        public async Task ConditionOffersFollowTheArmedPinsFamilyToo()
        {
            Project project = await Load("project2-CustomBlock.vis");
            IProjectDocument document = Session(project);
            ElementId flag = AddVariable(document, "Custom blok", "resource_flag", "Flag");
            ElementId enumerator = AddVariable(document, "Custom blok", "resource_enum", "Tilstand");
            ElementId conditions = ContainerIn(document.Current!, "Custom blok", "conditions");

            var onFlag = App.GetProgramOperations(document.Current!, conditions, flag);
            var onEnum = App.GetProgramOperations(document.Current!, conditions, enumerator);

            Assert.Multiple(() =>
            {
                Assert.That(onFlag.Select(o => o.Kind), Has.All.EqualTo(ProgramOperationKind.Condition));
                Assert.That(onFlag, Is.Not.Empty, "a bool pin is testable");
                Assert.That(onEnum.Select(o => o.Method), Is.Not.EquivalentTo(onFlag.Select(o => o.Method)),
                    "an enumerator is tested by its own predicates, not the bool family's");
                Assert.That(onFlag.Any(o => o.NeedsOperand), Is.True,
                    "the two-operand predicates report that a second operand is still to be chosen");
            });
        }

        /// <summary>
        /// A two-operand operation carries its OFFERED operands: the block's other pins of the armed pin's family,
        /// with the armed pin itself excluded — a caller picks one, it does not go looking for candidates.
        /// </summary>
        [Test]
        public async Task TwoOperandOffersCarryTheEligibleOperands()
        {
            Project project = await Load("project2-CustomBlock.vis");
            IProjectDocument document = Session(project);
            ElementId armed = AddVariable(document, "Custom blok", "resource_flag", "Flag A");
            ElementId other = AddVariable(document, "Custom blok", "resource_flag", "Flag B");
            ElementId conditions = ContainerIn(document.Current!, "Custom blok", "conditions");

            ProgramOperationOffer twoOperand = App.GetProgramOperations(document.Current!, conditions, armed)
                .First(o => o.NeedsOperand);

            Assert.Multiple(() =>
            {
                Assert.That(twoOperand.Operands.Select(o => o.VariableId), Does.Contain(other),
                    "a sibling of the same family is an eligible operand");
                Assert.That(twoOperand.Operands.Select(o => o.VariableId), Does.Not.Contain(armed),
                    "the armed pin is never its own operand");
                Assert.That(twoOperand.Operands.Select(o => o.Name), Has.All.Not.Empty,
                    "each operand carries the name a caller would show");
                Assert.That(twoOperand.Operands.Select(o => o.Method), Has.All.EqualTo(twoOperand.Method),
                    "an ordinary two-operand row uses the offer's own method for every operand");
            });
        }

        /// <summary>
        /// An enumerator's operand must share its TYPE: two enum variables of different typedefs cannot be compared,
        /// so the offer excludes the mismatch rather than offering a row the author would have to discover is wrong.
        /// </summary>
        [Test]
        public async Task EnumOperandsMustShareTheArmedEnumeratorsType()
        {
            Project project = await Load("project2-CustomBlock.vis");
            IProjectDocument document = Session(project);
            ProjectElement section = Block(document.Current!, "Custom blok").Children
                .First(c => c.Tag is "variables" or "settings");
            document.Apply(App.Commands.AddEnumVariable(
                document.Current!, section.Id!.Value, "Tilstand A", "Driftstilstand", ["Auto", "Manuel"])!);
            document.Apply(App.Commands.AddEnumVariableOfType(
                document.Current!, section.Id!.Value, "Tilstand B", "Driftstilstand")!);
            ElementId armed = EnumNamed(document.Current!, "Tilstand A");
            ElementId sameType = EnumNamed(document.Current!, "Tilstand B");
            ElementId untyped = AddVariable(document, "Custom blok", "resource_enum", "Uden type");
            ElementId conditions = ContainerIn(document.Current!, "Custom blok", "conditions");

            var operands = App.GetProgramOperations(document.Current!, conditions, armed)
                .Where(o => o.NeedsOperand).SelectMany(o => o.Operands).Select(o => o.VariableId).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(operands, Does.Contain(sameType), "two enumerators of the same typedef are comparable");
                Assert.That(operands, Does.Not.Contain(untyped),
                    "an enumerator naming no type has no states in common with one that does");
            });
        }

        /// <summary>
        /// Arithmetic (US-032): a numeric target offers each operator that has an AUTHORABLE cell for some operand,
        /// and each operand carries the opcode of its own cell — the F-108 grid is the SDK's, so a caller never
        /// derives a token from an operator symbol and two tags.
        /// </summary>
        [Test]
        public async Task ArithmeticOffersCarryThePerOperandOpcode()
        {
            (IProjectDocument document, ElementId counter, ElementId actions) = await CounterWithNumericOperand();

            var arithmetic = App.GetProgramOperations(document.Current!, actions, counter)
                .Where(o => o.Kind == ProgramOperationKind.Arithmetic).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(arithmetic, Is.Not.Empty, "a counter is an arithmetic target");
                Assert.That(arithmetic.Where(o => o.NeedsOperand).SelectMany(o => o.Operands).Select(o => o.Method),
                    Has.All.Not.Empty, "every offered operand names the opcode of its own cell");
                Assert.That(arithmetic.Where(o => o.NeedsOperand), Has.All.Matches<ProgramOperationOffer>(
                        o => o.Operands.Length > 0),
                    "an operator with no authorable cell is not offered at all");
                Assert.That(arithmetic.Any(o => !o.NeedsOperand && o.OperatorSymbol is "+" or "-"), Is.True,
                    "a counter target additionally offers the one-operand step commands");
            });
        }

        /// <summary>A variable that is not a numeric register is offered no arithmetic at all.</summary>
        [Test]
        public async Task ANonNumericVariableIsOfferedNoArithmetic()
        {
            Project project = await Load("project2-CustomBlock.vis");
            IProjectDocument document = Session(project);
            ElementId flag = AddVariable(document, "Custom blok", "resource_flag", "Flag");
            ElementId actions = ContainerIn(document.Current!, "Custom blok", "actions");

            Assert.That(App.GetProgramOperations(document.Current!, actions, flag)
                    .Where(o => o.Kind == ProgramOperationKind.Arithmetic), Is.Empty);
        }

        /// <summary>
        /// An offer is a menu, not a promise: what it names must still be accepted by the command path. Every
        /// offered arithmetic operand is checked against the document's own gate, so the two cannot disagree.
        /// </summary>
        [Test]
        public async Task EveryOfferedArithmeticOperandIsAcceptedByTheCommandPath()
        {
            (IProjectDocument document, ElementId counter, ElementId actions) = await CounterWithNumericOperand();

            var offers = App.GetProgramOperations(document.Current!, actions, counter)
                .Where(o => o.Kind == ProgramOperationKind.Arithmetic && o.NeedsOperand).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(offers, Is.Not.Empty, "the check is worthless over an empty offer set");
                foreach (ProgramOperationOffer offer in offers)
                    foreach (ProgramOperandOffer operand in offer.Operands)
                        Assert.That(document.CanApply(App.Commands.AddArithmeticCommand(
                                document.Current!, actions, counter, operand.Method, operand.VariableId,
                                offer.NameTemplate, operand.Note)).Ok,
                            Is.True, $"{offer.Id} with operand {operand.Name} was offered, so it must be accepted");
            });
        }

        /// <summary>
        /// A case VALUE branch is a commands container too — it is where one case's commands land — so the query
        /// offers commands there. Pinned because the obvious three tags are not the whole set, and a query that
        /// knew only "actions" would silently empty every menu inside a case.
        /// </summary>
        [Test]
        public async Task ACaseValueBranchIsACommandsContainer()
        {
            Project project = await Load("project2-CustomBlock-case.vis");
            ProjectElement branch = project.Root.Descendants().First(e => e.Tag == "case_action");
            ProjectElement variable = project.Root.Descendants().First(e => e.Tag == "resource_output");

            var offers = App.GetProgramOperations(project, branch.Id!.Value, variable.Id!.Value);

            Assert.That(offers.Select(o => o.Kind), Has.Some.EqualTo(ProgramOperationKind.Command),
                "commands are authorable inside a case value branch");
        }

        /// <summary>An element that is not a program container offers nothing — the query refuses by returning no
        /// offers rather than by throwing, because a frontend asks it on every selection change.</summary>
        [Test]
        public async Task ANonProgramContainerOffersNothing()
        {
            Project project = await Load("project2-CustomBlock.vis");
            ElementId block = Block(project, "Custom blok").Id!.Value;
            ElementId variable = project.Root.Descendants().First(e => e.Tag == "resource_flag").Id!.Value;

            Assert.That(App.GetProgramOperations(project, block, variable), Is.Empty);
        }
    }
}
