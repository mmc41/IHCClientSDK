# ihcclient_stub

A controller stub. It answers the SOAP calls the `ihcclient` SDK sends, so tests and manual exploratory runs can exercise the whole SDK without an IHC controller. This is useful for E2E and other tests that would alter a controller's state to a high degree. Note that the stub does not replicate a real controller and cannot be relied on for exact responses; it gives plausible ones, enough to satisfy those tests. Only the controller is replaced: the SDK's wrappers, serializer, envelope guards, cookie session and telemetry all run as they do against a real one, so a stub-backed test exercises that code, and coverage counts the lines it reaches.

This page is the short version, for someone using or changing this project. Two documents carry the rest:

- [ADR-005](../docs/adr/ADR-005-controller-stub-seam.md) records the decisions behind the stub, their rationale and the rejected alternatives.
- [Controller stub architecture](docs/stubdesign.md), in this project's `docs/` folder, is the ADR's illustrated companion. It shows [who references whom](docs/stubdesign.md#who-references-whom), [how one SOAP call is routed](docs/stubdesign.md#one-soap-call-through-clientpost), [the two ways to start a stub](docs/stubdesign.md#two-ways-to-start-a-stub), [how the stub answers one exchange](docs/stubdesign.md#inside-ihcclient_stub-answering-one-exchange) and [which suite uses which path](docs/stubdesign.md#which-suite-uses-which-path). Its last table, [the safeguard layers](docs/stubdesign.md#safeguard-layers), defines the tags such as `L3` or `O6` that this page uses below.

## Scope

- **It answers every wire operation the SDK can post.** Operations seen in real controller traffic, the positive-flow operations that traffic lacks, and OpenAPI's operations over the resources, the login and the stored project, with its event wait, have typed answers, which read and write the state of one default controller: ready, with a ready SD card and a valid project the SDK built itself. Every other operation gets a value built from its response type, a shape default, until a test needs a typed value. A shape default proves the response maps, not that the operation succeeded, and it ignores the scenario: OpenAPI's time, uptime, firmware version, dataline inputs and ping still answer shape defaults, which do not follow the typed services they mirror.
- **Fidelity is best effort.** A pass against the stub never means an operation works on a controller. The typed answers are this repository's reading of the wire, kept consistent with the SDK, not with a controller.
- **Any non-empty user name with a non-blank password logs in**, except `ControllerStub.IllegalPassword`, which every login refuses.
- **It ships nowhere.** Test suites reference it and a person runs it. No application, utility or package may reference it (L4): an application reaches a stub only through the SDK's `--controller-stub` switch.
- **The seam is not here.** `IControllerStub`, `ControllerStubs`, routing and the latch that refuses real endpoints (L3) live in the SDK, under `ihcclient/src/stubbing/`. [One SOAP call through Client.Post](docs/stubdesign.md#one-soap-call-through-clientpost) draws that routing.

## Usage

### Manual run

```bash
dotnet run --project ihcclient_stub/ihcclient_stub.csproj
# --controller-stub=http://127.0.0.1:<port>/<token>/
```

The first line is the switch to start an application or utility with. After it, the stub prints one line per call, and a line of its own for an operation it does not model or a defect in itself. Stop it with Ctrl+C. [Running against a controller stub](../README.md#running-against-a-controller-stub) lists which executables take the switch and what changes under it, and [Two ways to start a stub](docs/stubdesign.md#two-ways-to-start-a-stub) shows the launched path: the switch replaces the endpoint and the credentials and keeps every other setting from the file (O5), and the app checks the host's token before it continues (O3).

### From a test

Most tests register the stub in their own process:

```csharp
using Ihc;
using Ihc.Stubbing;

var stub = new ControllerStub();
using var registration = ControllerStubs.Register(stub);   // a fresh endpoint that never resolves
var settings = new IhcSettings { Endpoint = registration.Endpoint, UserName = "stub-user", Password = "stub-only" };
using var authentication = new AuthenticationService(settings);
var controller = new ControllerService(authentication);

await authentication.Authenticate();
ProjectFile? project = await controller.GetProject();      // the default controller's project

var sent = stub.Journal.Calls<IControllerService>(nameof(IControllerService.GetProject));
stub.VerifyNoFaults();                                     // nothing unmodelled, no defect in the stub
```

- **Arrange state** through the stub's scenario properties (`stub.Controller`, `stub.Resources`, `stub.UserManager` and the others) before the call.
- **Fail an SDK method** with `stub.FailOn<TService>(sdkMethod)`, as a SOAP fault, which the SDK raises as `ErrorWithCodeException` with `CONTROLLER_SOAP_FAULT`, or, with `StubFault.ConnectionLost`, as a lost connection, which it raises as `HttpRequestException`, except for `IConfigurationService.SetNetworkSettings`, which raises it as an unconfirmed write, `ErrorWithCodeException` with `CONTROLLER_WRITE_UNCONFIRMED` and the transport's error inside. An injected failure never changes the stub's state. `stub.OverrideRaw(service, soapAction, answer)` answers one wire action by hand, ahead of everything else, for a malformed body or similar; its answer carries a final status, 200 to 599, and a body, and any other is refused as a stub defect. It takes the WIRE names a journal entry's `Service` and `SoapAction` carry, such as `SMSModemService` and `getIHCProject`, not the SDK's `SmsModemService` and `GetProject`, because an override must also reach a step no SDK method owns. [Answering one exchange](docs/stubdesign.md#inside-ihcclient_stub-answering-one-exchange) gives the full order: raw override, injected failure, typed answer, shape default.
- **Name only what can happen.** `FailOn`, `Journal.Calls` and `OverrideRaw` refuse with an `ArgumentException` a name no exchange could ever match: a service class instead of its interface, a method the interface does not declare, one that sends nothing or owns no wire operation, or a wire pair no operation has. The refusal says what to name instead. Left alone, such a name would fail nothing, or answer an empty journal that also passes an assertion that nothing was sent.
- **Read the journal in answer order.** `Journal.All` lists exchanges in the order the stub answered them, which is the order they arrived in except for a held long poll, journalled when it ends.
- **Drive a launched executable** with `ControllerStubLoopbackHost.StartAsync(stub, cancellationToken)`, and pass `--controller-stub=` followed by `host.BaseAddress` to it. The stub and its journal stay in the test. `await ControllerStubs.RegisterLoopbackAsync(host.BaseAddress, cancellationToken)` reaches the same host from the test's own process.

In `safe_stub_tests`, `StubbedSdk.Start()` builds every SDK service on a registered stub, and disposing it runs `VerifyNoFaults()`.

## Test integration

`safe_stub_tests` tests the stub itself and the SDK's stub seam. `safe_unit_tests`, `safe_lab_tests`, `safe_visual_stubbed_tests` and `safe_visual_e2e_tests` use the stub to run something else. `safe_architecture_tests` never runs it; it reads the stub's IL and every project file. [Which suite uses which path](docs/stubdesign.md#which-suite-uses-which-path) says what each covers, and [TESTSTRATEGY.md](../TESTSTRATEGY.md) decides which suite a new test belongs in.

A new stub-backed test must:

- **Be admitted by the reach guard.** A site that builds real SDK services from settings needs an entry, with the stub-endpoint reason, in `tests/shared/ControllerReachGuard.cs`. Without one, the guard stops the suite's run before any test executes, a filtered run included (L9).
- **Own its registration and dispose it**, after the services built on it. Let `Register` generate an endpoint per test. A `mock://` key is single-use per process, so keep one for a fixture that holds it for the whole run.
- **Call `VerifyNoFaults()` at teardown.** An unmodelled operation or a stub defect reaches the code under test as an HTTP 500 or a coded fault, which that code may swallow, so the stub's own lists are where it shows (L8).
- **Use the stub's public surface.** Never the generated `Ihc.Soap` types; only `safe_stub_tests` sees the stub's internals.
- **Launch OpenVisual only through `E2E.Launch`** in `safe_visual_e2e_tests`. It starts a fresh loopback host for every launch and checks stub mode before the first action (O6), and `LaunchGateTests` fails if any other site asks the driver to launch, calls the launcher, or starts a process.

`safe_unit_tests`, `safe_project_tests`, `safe_lab_tests`, `safe_visual_tests`, `safe_visual_stubbed_tests` and `safe_visual_e2e_tests` also link `tests/shared/ControllerStubLatchFixture.cs`. It holds a fail-every-call stub for the whole run, so the latch refuses any real endpoint in that suite (L11). It is declared outside any namespace, because only there does NUnit apply a setup fixture to the whole assembly. It needs only `ihcclient`, so linking it does not mean referencing this project. `safe_stub_tests` is left out because its seam tests must see the latch both off and on.

## Risks

Two risks come with the stub, and they are handled in opposite ways. An answer that differs from a controller's is expected, so a test must claim no more than the stub can show. A stub test that reaches a real controller is never acceptable, and the guardrails below stop it.

### The stub answers differently from a controller

This is expected. Fidelity is best effort by decision ([ADR-005](../docs/adr/ADR-005-controller-stub-seam.md#the-stubs-answers)): the typed answers are this repository's reading of the wire, kept consistent with the SDK and never compared with a controller. So the stub is never the truth about a controller, and no test or person may rely on it as one. It differs from a controller by design in at least these ways:

- **Credentials are not checked against any account.** The login rule under [Scope](#scope) is the stub's only check, so a stubbed login says nothing about a controller's credential check.
- **There is one default controller.** Unless a test arranges otherwise, it is ready, with a ready SD card and a valid project the SDK built itself. A real controller can be in any state, with any project.
- **A shape default is no controller's answer.** An operation without a typed answer gets a well-formed value built from its response type, which proves the response maps, not what a controller would send.
- **Not every typed default is a captured answer.** The SMS modem settings and the wireless settings are stand-ins, since no capture holds either answer; each scenario class says which of its defaults are captured.
- **An empty list is written as a controller writes it.** Every empty list in an answer, typed or shape-built, goes out as one nil `arrayItem`, the form a 3.3.44 controller gives every empty list it sends; only a list of integers, whose items cannot be nil, goes out as its answer holds it, the low-battery list's captured "none" being a lone `-1`. A test that reads an answer's raw body sees that form, not an empty element.
- **A segmented transfer is cut as captured, but from the stub's own gzip.** A stored project is served as a 3.3.44 controller serves it: slices of one gzip stream, `SegmentationSize` bytes of gzip data each (7,500 by default), only the first carrying the gzip header, every one named `unknown.bin`. The stream is .NET's gzip of the project, so its bytes and the segment count are not a controller's for the same project. An upload's slices, each sent with the upload's segment count, are joined and decoded when the last one arrives; that the controller unpacks an upload only then is inferred, not captured.
- **Timing is not a controller's.** A wait for a controller state change answers at once, because the stub's transitions are synchronous, so code that mishandles a slow controller still passes. An executable run under the switch marks its telemetry `ihc.controller.stub=true`, so stub latencies never mix into a controller's figures.

How a test stays honest about it:

- **Assert on what the test arranged.** Take the expected value from the scenario the test set up, or from the SDK's contract, never from whatever the default controller happens to answer. The test then states its assumption about the controller where a reader sees it.
- **Arrange a special case in the test that needs it.** A refusal, a lost connection or an unusual answer is a `FailOn` or an `OverrideRaw` there, not a change to the default controller that every other test shares.
- **A stub defect never passes for a controller's answer (L8).** The SDK raises it as `CONTROLLER_STUB_FAULTED`. The stub records it, as it records an operation it does not model, and `VerifyNoFaults()` at teardown fails a test whose code swallowed either.
- **Correct the stub when a controller disproves it.** Fix the typed answer, and any test that relied on it, in the same change. No gate compares the stub with a controller, so nothing else will catch the difference.
- **Take controller truth from a controller.** Only `safe_integration_tests` may reach one, through state-safe operations, and it never runs in CI, so a claim about how a controller behaves rests on a deliberate run of it.

### A stub test reaches a real controller

This must never happen. The guardrails are independent layers, arranged so that no single mistake in a stub test, or in an executable started with the switch, lets it happen. Where they fall short, this section says so: the per-response check refuses an answer, not the request that drew it (O4), and the limits at the end of the section stay open. The guardrails are listed by the mistake they catch; each tag is a row of [the safeguard layers](docs/stubdesign.md#safeguard-layers).

- **The test's settings name a real controller.** While any registration is live, the latch refuses every non-stub endpoint before a socket exists, with `CONTROLLER_SOCKET_REFUSED_WHILE_STUBBED` (L3). The suites that link the latch fixture hold a registration for their whole run, so even a test that registered nothing, or a service a product factory built, is refused. The architecture suite fails a test project that should link the fixture and does not, and fails the one suite exempt from it if that suite stages the developer's settings file (L11). The reach guard stops a suite's run before any test executes when the suite builds real services from settings at a site not admitted with a reason that is true of it (L9), and `safe_visual_stubbed_tests` fails its run when an `ihcsettings.json` sits beside it (L5).
- **The registration is missing or already disposed.** A stub endpoint goes only to its own registration, and without one it is refused with `CONTROLLER_STUB_NOT_REGISTERED`, never sent to a socket (L2). Its host is under `.controllerstub.invalid`, which never resolves and which a system proxy refuses, and `mock://` is no HTTP scheme at all, so even a call that escaped routing reaches no controller (L1).
- **Code sends around the routing.** Routing and the latch both sit in `Client.Post`. An architecture test fails the SDK if it creates or sends on a network client anywhere but its named transport sites, and fails an executable the switch is wired into, or the bootstrap code the executables share, that creates or uses one at all (L12). A test that builds a network transport of its own, such as a bare `HttpClient`, a socket handler, a generated WCF client, a socket or web client, a `WebRequest` or a subclass of one, stops its suite's run before any test executes (L9).
- **An executable starts without the switch, or ignores it.** The architecture suite fails unless every wired executable applies the switch from its `Main`, and unless every executable that references the SDK is either scanned or listed as not wired, with its reason (O7). Every desktop launch goes through `E2E.Launch`, which always passes the switch and kills the app before the first action unless the host saw its identity check and the title shows the stub marker, and nothing else in that suite may start a process (O6). A utility's process test runs it from a clean copy whose settings name only a `.invalid` host, so a utility that ignored the switch fails, and every desktop launch starts OpenVisual from such a copy, with no login, which the run checks before any test (O8). In a manual run, what tells a person is the stub marker in the title (L10), or the stub line a console utility prints on standard error before anything else.
- **The switch points at something that is not the stub.** It accepts only `http` to a loopback IP literal with an explicit port and a token, and anything else stops the executable, with no fallback to the settings file (O1). The client behind it uses no proxy, follows no redirect and connects only to loopback addresses (O2). The host must echo its token before the registration exists, so a tunnel or port-forward to a controller is refused before any SOAP request is sent to it (O3). Every response after must carry the token too, but that is checked on the response itself: a process that takes the port after the identity check still receives each request, and the SDK refuses its answer, failing the call with `CONTROLLER_STUB_IDENTITY_FAILED` (O4).
- **A switched process meets a real endpoint.** The switch replaces the settings file's endpoint and credentials with stub values (O5), and the SDK holds its registration until the process exits, so the latch refuses every real endpoint for the rest of the run, one typed into Lab's Setup dialog included (L3).
- **The stub is made to forward calls.** An IL scan fails the stub on any outbound transport of its own, so it cannot become a relay to a controller (L7).
- **A request slips past all of the above.** `safe_visual_stubbed_tests` checks from telemetry that every request its run sent went to a stub host or the loopback address, after a canary call proves that the capture sees requests at all (L6).

The journal is not a guardrail. It records only the calls that reached the stub, so it cannot see one that went to a controller: asserting on it checks an interaction, not safety, which is why the stub has no unsafe-call check ([ADR-005](../docs/adr/ADR-005-controller-stub-seam.md#safeguards)). Three limits remain:

- **`safe_stub_tests` holds no latch for its run**, because its seam tests must see the latch released. The reach guard still stops its run before any test executes, but it sees only what the suite constructs itself: a service a product factory builds is invisible to it, and nothing in that suite refuses the service's calls. So a stub test that needs an endpoint other than a stub's uses a host under `.invalid`, and the suite may not stage the developer's settings file (L11).
- **`safe_integration_tests`** links neither the reach guard nor the latch fixture, because it is the one suite meant to reach a controller. A stub test belongs in a controller-free suite.
- **The SDK examples** take no switch. They ignore `--controller-stub` and run against the controller their settings file names; [Running against a controller stub](../README.md#running-against-a-controller-stub) lists the executables that take it.

## Maintenance

| Path | Holds |
| --- | --- |
| `ControllerStub.cs` | The stub: answer precedence, failure injection, raw overrides and the teardown check |
| `StubJournal.cs` | The journal of every exchange |
| `ControllerStubLoopbackHost.cs` | The loopback host that serves a stub to another process |
| `Program.cs` | The manual-run entry point and its console report |
| `Scenario/` | The state tests arrange (`Stub*.cs`), the default controller and `SdkOperations`, the one operation table |
| `Wire/` | The SOAP codec, the dispatcher and the shape defaults; `Wire/Services/` holds the typed answers, one class per service |
| `docs/stubdesign.md` | The design page: the illustrated companion of ADR-005, the suite map and the safeguard tags |
| `docs/*.svg` | The design page's four diagrams: `stub-references`, `stub-routing`, `stub-activation` and `stub-answer-chain` |

Common changes:

- **A test needs a value the shape default cannot give.** Add the state to the matching `Scenario/Stub*.cs` class, write the answer in `Wire/Services/` under the generated operation's name and signature, and declare its kind in `SdkOperations`. `StubAnswerSelfTests` fails while the answers and the declarations disagree.
- **The SDK gains a public service method.** `SdkOperations` maps it to the wire action of the same name. A method that sends nothing, is gated, or sends other actions must be declared as such, and the table refuses to build, naming each case, until it is.
- **The SOAP layer is regenerated.** A new operation answers from its shape at once. `StubWireCompletenessTests` pins the number of generated operations, so review what changed before you update it.
- **The loopback protocol changes.** Its header names, identify path and answer prefix are spelled both in `ControllerStubLoopbackHost` and in the SDK's internal `ControllerStubLoopback`. Change both; the loopback tests fail on any drift.

What must stay true:

- **No outbound connection (L7).** The host only listens. `StubOutboundArchitectureTests` reads the stub's IL and fails on any client transport.
- **No reference from outside `tests/` (L4).** `StubReferenceArchitectureTests` enforces it.
- **No password recorded, and no payload, cookie or password printed (O5).** A login journals the user name alone, and a recorded payload has its string members cleared where the SDK marks them `[SensitiveData]` or names them for a password. Only the payload's own members are cleared, so a stored project is journalled whole, including any SMS modem PIN code in its XML, and that is accepted. The journal and the console report are read and pasted freely.
- **No file outside the repository.** The defaults are built in code, on a fixed clock, so they are the same in every process.

`ihcclient_stub.dll` is in the measured set in [.runsettings](../.runsettings), so a new answer's tests are judged by the uncovered lines of the files it touched, as product code is. After a change, run `safe_stub_tests` and `safe_architecture_tests`, plus the suite that uses the changed path. A change to the stub's contract also updates ADR-005 and the design page, in the same change.

Keeping the design page true:

- **State rules, never counts.** The page says which operations get typed answers, not how many, because typed answers are added whenever a test needs one.
- **Change a diagram and its text together.** Each SVG is hand-written and carries its description twice, in its `<title>` and its `aria-label`, and the page repeats it as the image's alt text. Keep all three equal, and keep the colour legend at the top of the page true for every diagram.
- **A new safeguard gets a tag.** Add its row to the design page's safeguard table, and cite the tag wherever this README or the design page describes the safeguard.
