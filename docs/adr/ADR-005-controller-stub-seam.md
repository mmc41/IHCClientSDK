# ADR-005: A controller-stub seam ships in the SDK; the stub itself ships in no application

## Status

Decided — 2026-10-31. Amended 2026-11-01 (asynchronous loopback registration; safeguards L9, L11, L12 and O8
tightened; a friend-list grant for `safe_stub_tests`; a console utility checks its command line before reading its
settings; a near spelling of the switch is refused; an answer no final HTTP response carries is refused where a
stub builds it). Refines ADR-002, whose fakes sit on the low-level API services: this
record adds a seam below them, at the SDK's one transport, so the whole SDK runs against a stub. Applies ADR-004's
placement rule to the new guards. Leaves ADR-003 unchanged: the stub uses the generated layer internally, through
an opt-out taken in its own project file. Amends the testability-switch paragraph of
[ARCHITECTURE.md](../../ARCHITECTURE.md) (see "The amended testability rule" below). The
[stub design page](../../ihcclient_stub/docs/stubdesign.md) is this record's illustrated companion: it describes the
implementation, the suite map and the safeguard tags.

Revisit triggers: (a) `ihcclient` is published as a NuGet package, which turns the seam into a distributed
contract a package consumer can register against; (b) the SDK gains a second transport beside `Client.Post`,
since routing and the latch both sit there; (c) OpenVisual's controller work (epic E10) starts, which is the
first product use of the seam and inherits the two constraints recorded under Negative implications; (d) the
first request to let the switch accept anything but a loopback address.

## Decision at a glance

The SDK carries a small public seam, `Ihc.Stubbing`: one provider interface that answers one SOAP exchange, an
endpoint-keyed registry, a routing check in `Client.Post`, and an executable entry, `ControllerStubs.ApplySwitch`.
The stub's behaviour lives in a companion project, `ihcclient_stub`, which tests reference and a person runs as
an executable, and which no application or utility references. A shipped executable can only be redirected,
by the `--controller-stub` switch, towards a loopback stub in another process that proves its identity. The SDK
holds the switch's registration until the process exits, so a switched process refuses every real endpoint for
its whole life. Layered safeguards make sure that no single mistake lets a stubbed run reach a controller.

## Terms

- **Switch**: the `--controller-stub` command-line argument, which points an executable at a loopback host.
  `ControllerStubs.ApplySwitch` applies it.
- **Registration**: the live entry that routes one stub endpoint to one stub, returned by `Register` or
  `RegisterLoopbackAsync`; `ApplySwitch` keeps its own.
- **Latch**: while any registration is live, `Client.Post` refuses every real endpoint before a socket is opened
  (L3).
- **Loopback host**: a stub served over HTTP on a loopback address, by a test or by `ihcclient_stub` run by hand.
  It proves its identity with a token: it echoes the token to a **preflight** request before the registration
  exists (O3), and carries it in a header on every response (O4).
- **Indicator**: the stub marker in OpenVisual's and Lab's window titles (L10).
- **Journal**: the stub's record of every exchange it answered. It sees only calls that reached the stub.
- **Stub line**: `CONTROLLER STUB in use (no real controller): <endpoint>`, which a console utility prints first,
  on standard error, when it runs against a stub.

## Context

**Current state** (2026-10-31, before this decision):

- Every controller request the SDK sends passes through one method, `Client.Post`, over one process-wide
  `HttpClient`. The only substitution point is an explicit transport passed through internal constructors,
  which only the friend suites can reach.
- `mock://` is a reserved endpoint prefix. Service construction refuses it, and Lab maps it to a FakeItEasy
  fake (`IhcFakeSetup`) that ships inside Lab.
- OpenVisual makes no controller call. Its Send and Retrieve commands refuse, and its composition root is
  file-only.
- `ControllerReachGuard` keeps the controller-free suites from building real services from settings, except at
  admitted sites whose recorded reason is that nothing is called.
- The friend list in `ihcclient.csproj` is closed: [ARCHITECTURE.md](../../ARCHITECTURE.md) says it does not
  grow.

**Decision forces**: OpenVisual's controller work needs integration and end-to-end tests that drive a real
launched executable, and none of them may reach a controller. A launched executable is a separate process, so
no in-process substitution reaches it. Product code (each executable's `Main`) has to reach the seam, and the
friend list cannot carry it, so the seam is public. A test affordance in a shipped binary must not become a
way to change what the product does against a real controller.

**Reversibility**: one-way for the public surface once the SDK is published, two-way before that (the API
contract permits narrowing during the unpublished beta). `ihcclient_stub` and every guard are internal to the
repository and can change freely.

**Assumptions**:

| Assumption | Type | Confidence | Source | Validation trigger |
| --- | --- | --- | --- | --- |
| `Client.Post` stays the SDK's only send site for controller traffic | technical | high | the transport funnel today, pinned by an architecture test | a second transport site appearing in the SDK |
| A controller cannot echo a token it never saw | technical | high | the loopback preflight's design | a tunnel or proxy that rewrites responses |
| Names under `.invalid` never resolve | technical | high | RFC 2606 and RFC 6761 | a resolver or proxy that answers for the reserved domain |
| Shape-only answers are enough for the positive flows tests need | operational | medium | the owner's best-effort fidelity ruling | a test that needs controller fidelity the stub cannot give |

**Constraints**:

| Constraint | Category | Provenance |
| --- | --- | --- |
| The friend list does not grow | organizational | ARCHITECTURE.md; one test-suite exception since 2026-11-01, under What ships where |
| The SDK has no logging dependency; it reports through its `ActivitySource` | technical | ARCHITECTURE.md |
| Test logic must not ship in a production artefact | technical | Meszaros, "Test Logic in Production" |
| The stub refers to no file outside the repository | organizational | owner ruling |
| Windows is reached only from a project whose name contains `windows` | technical | ARCHITECTURE.md |

## Evaluation Criteria

Priority order (highest first). The first is decisive: an option that lets a stubbed run reach a controller is
not an option.

1. **Controller safety** — no stubbed run, in-process or launched, can reach a controller through any single
   mistake.
2. **Fidelity of what is tested** — the real SDK runs: wrappers, mapping, serializer, envelope guards, cookie
   session and telemetry.
3. **Shipped surface** — how much test code, or test-only public API, reaches a production artefact.
4. **Authoring cost** — how readable and maintainable a stub answer is for a test author.
5. **Reach** — whether one mechanism serves unit tests, the Lab suite and a launched desktop executable.

## Options

### 1. A per-exchange seam in the SDK, the stub in a companion project, executables redirected by a loopback switch (chosen)

`IControllerStub` answers one exchange in plain values: service, SOAP action, body and cookie in; status, body
and Set-Cookie out. No HTTP type crosses it. The SDK converts the HTTP request itself, under its own logging
handler. `ihcclient_stub` implements it over the generated service interfaces, and tests write its answers as
SDK model values.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Controller safety | 5/5 | Routing, a latch, a reserved domain and an identity preflight each stop a stubbed run independently |
| Fidelity of what is tested | 5/5 | Only the controller is replaced; every SDK layer above the transport runs |
| Shipped surface | 4/5 | Only the seam ships; the stub ships nowhere, at the cost of a public seam any package consumer can register against |
| Authoring cost | 4/5 | Tests write SDK model values; the wire tier is internal to the stub |
| Reach | 5/5 | In-process registration for tests, the loopback switch for any launched executable |
| | **Total: 23/25** | **Trade-offs**: a test feature in the public SDK, mitigated by redirect-only activation, the indicator and the guards |

### 2. The stub inside `ihcclient`

One assembly, no companion project, no public SPI beyond an on/off entry.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Controller safety | 4/5 | Routing can be as strict, but the stub's behaviour is reachable from every consumer |
| Fidelity of what is tested | 5/5 | Same transport-level replacement |
| Shipped surface | 1/5 | The stub's whole behaviour ships in the package: test logic in production |
| Authoring cost | 4/5 | Same authoring model |
| Reach | 5/5 | Same reach |
| | **Total: 19/25** | **Trade-offs**: simplest layout, paid for by shipping test code to every consumer |

### 3. A public hook that accepts an `HttpMessageHandler`

The seam is the handler pipeline itself.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Controller safety | 1/5 | Anything can be registered, a real socket handler included, so "a stub is configured" can lie |
| Fidelity of what is tested | 5/5 | The SDK still runs above the handler |
| Shipped surface | 3/5 | A small hook, but it puts an HTTP type into the public contract |
| Authoring cost | 2/5 | Test authors write HTTP and SOAP by hand |
| Reach | 3/5 | Serves in-process tests; a launched executable still needs a redirect |
| | **Total: 14/25** | **Trade-offs**: the smallest seam, at the price of the guarantee the seam exists to give |

### 4. Applications carry the stub, started by `mock://` in the settings file

Lab's shape before this decision, generalised: each executable references the stub and starts it in-process when
its settings name a `mock://` endpoint.

| Criteria | Score | Rationale |
| --- | --- | --- |
| Controller safety | 3/5 | A settings edit switches modes, and the stub is reachable from product code |
| Fidelity of what is tested | 5/5 | Same transport-level replacement |
| Shipped surface | 1/5 | Every application ships the stub and its server dependencies |
| Authoring cost | 4/5 | Same authoring model |
| Reach | 4/5 | One step for a manual run, but a test cannot hold the stub or its journal across the process boundary |
| | **Total: 17/25** | **Trade-offs**: one-step manual runs, paid for by test code in every shipped application |

## Decision

Adopt option 1.

### What ships where

The scope is the stub and its seam. OpenVisual's controller work (E10) is not part of it; the stub is what lets
that work be tested safely.

- The **seam** is a built-in test feature of the packable `ihcclient`: the registry, routing, the latch, the
  error codes, the provider interface, `Register`, `RegisterLoopbackAsync` and `ApplySwitch`. Any package consumer
  could register a stub.
- **`ihcclient_stub` ships in no application.** Tests reference it, and a person runs it as an executable for a
  manual run. A shipped executable can only be redirected to a stub that another process runs. A separate shared
  project for the switch was rejected: `ApplySwitch` in `ihcclient` is the whole redirect.
- So the only test affordance in a shipped executable is the redirect, and it can only point towards a loopback
  stub that echoes its token. The stub's behaviour stays out of every production artefact; only the enabling
  point ships, which is the distinction between a seam and built-in test code (Feathers, 2004; Meszaros, 2007).
- **Every application and utility that calls a controller supports the switch**, not only the desktop
  applications, so none needs a stub branch of its own. A console utility prints the stub line first. The SDK
  examples stay unwired, as minimal samples, and so does the settings encryptor, which makes no service call.
- Lab's FakeItEasy fake is removed with nothing in its place: one stub serves every suite, and keeping the fake
  beside it was rejected. OpenVisual ships nothing beyond the SDK it already uses.
- **One test suite joins the friend list.** `safe_stub_tests`, which holds the seam's tests that read the registry
  and the switch's internal core, became a friend assembly on 2026-11-01: the one exception to the rule that the
  list does not grow. A grant is assembly-wide, so every test in that suite can reach every SDK internal, not
  only the two its tests need. Product code still reaches the seam only through its public surface.

### The seam and its contract roles

The public surface is `IControllerStub`, `ControllerStubRequest`, `ControllerStubResponse`, `ControllerStubs`,
`ControllerStubRegistration`, the method `IhcSettings.IsStubConfigured()` and new `Errors` constants. The
stub-host suffix stays internal to the one predicate behind `IsStubConfigured()`, which routing, the latch and
both indicators share. The roles the [API contract](../../ihcclient/docs/api-contract.md) records for them:

- `IControllerStub` is a **provider contract (SPI)**: a caller supplies it to the SDK.
- `ControllerStubRequest` and `ControllerStubResponse` are **inputs and results**.
- `ControllerStubs` and `ControllerStubRegistration` are **service entry points**: they route a settings endpoint
  to a stub.
- The **failure contract** is `ErrorWithCodeException` with six new codes: `CONTROLLER_STUB_NOT_REGISTERED`,
  `CONTROLLER_SOCKET_REFUSED_WHILE_STUBBED`, `CONTROLLER_STUB_FAULTED`, `CONTROLLER_STUB_ENDPOINT_INVALID`,
  `CONTROLLER_STUB_IDENTITY_FAILED` and `CONTROLLER_STUB_ENDPOINT_IN_USE`. Each message starts with its code's
  name, so a refusal can be found by its code in telemetry. The API contract's
  [failure-contract section](../../ihcclient/docs/api-contract.md#4-failure-contracts) says when each is raised.
- No HTTP, socket, WCF or envelope type may appear in a public `Ihc.Stubbing` signature, so the rejected handler
  hook (option 3) cannot return as an overload.

**Stubs are selected by endpoint.** The registry is keyed by the settings endpoint, so settings stay plain
bindable data and the endpoint text itself shows that a stub is in use. Settings that carry the stub object were
rejected: they would change the settings type's equality, `with` and binding.

### Request and response are sealed classes, not records

ARCHITECTURE.md gives immutable public values records. `ControllerStubRequest` and `ControllerStubResponse` are
sealed classes with get-only properties instead. They are exchange messages that no code compares, copies or
deconstructs, and a positional record would add equality, `with`, deconstruction and `init` setters to the public
baseline for no caller. This departure applies to these two types only.

The response's constructor refuses a status outside 200 to 599 and a null body, which no final HTTP response
carries. A stub that builds one fails where it built it, so the SDK raises the defect code over both transports;
carried on, the answer would reach the caller as a controller's status, or as a transport or identity failure
that differs between them. *(Amended 2026-11-01: such an answer was passed on before.)*

### Routing, ownership and the latch

- `Client.Post` decides on the client's own URL, fixed at construction, never on the mutable settings endpoint.
  A stub endpoint goes only to the registry, and is refused when nothing is registered for it. An explicit test
  transport comes next. Then the latch refuses every endpoint while any registration is live. Only then is the
  shared socket client used.
- Whoever calls `Register` or `RegisterLoopbackAsync` owns the registration and disposes it. The registry is a set:
  `Dispose` removes that registration by identity and is idempotent, the registry holds every registration
  strongly, and a registration has no finalizer, so a leaked registration keeps the latch on loudly rather than
  releasing it silently. A `mock://` key is single-use per process, so two fixtures can never swap each other's
  stub.
- **The latch has two lifetimes.** A test's registration latches the process only while it is live, and disposing
  the last live one releases the latch, so one suite can test the latch both on and off. A latch that stays
  on once set was rejected, because it would make that suite order-dependent. The switch's registration is
  never released: `ApplySwitch` keeps every registration it makes until the process exits and never hands one to
  the executable, and an executable can reach no other registration (O7). So an application process is a stub
  process or a live one, never both; letting it leave stub mode, or locking Lab's endpoint field instead, was
  rejected. Because stub mode cannot end, the launch gate (O6) checks it only once, not before every
  action. Tests reach the switch's rules through an internal core that returns the registration, so they
  can dispose it.
- `ApplySwitch` runs once per start, after the executable's own settings loader and telemetry bootstrap, inside
  `Main`'s error handling, and before any SDK service is built. Any switch value but a valid loopback URL makes the
  executable refuse to start, with a non-zero exit code and no fallback to the file. So does an option that names
  the switch in any spelling but the exact one, such as another case, a colon for the `=`, one dash or a slash:
  ignored, it would leave the executable on its settings file's controller in a run its author believes is
  stubbed. The refusal repeats only the option's name, since the value carries the host's token. Accepting the near
  spellings was rejected, because an executable that accepts more spellings than it documents has no fixed
  switch. *(Amended 2026-11-01: a near spelling was ignored before.)*
- **The executable's own argument parser never sees the switch.** It reads the arguments `ApplySwitch` leaves, or
  leaves out each argument `SwitchRequested` recognises, which is the same list whenever the switch is accepted. A
  console utility does the latter, before its settings loader, so it answers a command line it cannot use with its
  usage and exit code whether or not a settings file exists. Parsing after `ApplySwitch` there was rejected, because
  it made a usage error depend on the settings file. *(Amended 2026-11-01: a console utility's check before its
  settings loader replaced parsing after `ApplySwitch`.)*
- **The switch replaces the endpoint and the credentials, and nothing else.** The credentials become fixed
  stub-only values, so the developer's own credentials reach no stub, journal or trace. Every other setting,
  telemetry included, comes from the file, so a stubbed run reads its settings as a normal run does. Blank
  credentials were rejected, because the stub refuses a blank password, and so was copying only named settings,
  which would drop a setting added later.
- **A stale `mock://` endpoint in a settings file is repaired by editing the file.** Lab builds every service at
  start, so the refusal is its startup error, and the `CONTROLLER_STUB_NOT_REGISTERED` message names the endpoint
  and both ways out: a controller's address in the file, or the switch. Keeping Lab's Setup dialog reachable after the refusal was rejected.
- **`ApplySwitch` blocks its caller**, unlike the rest of the stub API and the SDK's controller operations. With
  the switch it waits up to five seconds for the host's identity check, because no registration may exist, and no
  executable may start, before the host has proved itself. Without the switch it sends nothing. This departs from
  the rule that a library exposes no synchronous wrapper over asynchronous work (Toub, 2012), without that rule's
  hazards: the wait runs once, in `Main`, before any UI thread or synchronization context exists, and every await
  beneath it ignores the captured context, so it can neither deadlock nor starve the thread pool; its public
  documentation states the I/O it performs. Its callers need it synchronous: OpenVisual and Lab call it from a
  synchronous `[STAThread]` `Main` before Avalonia starts, and a console utility has nothing to do until it
  returns. An asynchronous `ApplySwitch`, or an `ApplySwitchAsync` twin, would only move the same wait into each
  `Main`.
- **The other two registration members do not block.** `Register` does no I/O. `RegisterLoopbackAsync` returns a
  task and takes a cancellation token, because its callers hold a host they started themselves and can await it;
  its failures follow the task-based asynchronous pattern, as its documentation states. `ApplySwitch` registers
  through it and waits on the task, so a refusal still surfaces as `ErrorWithCodeException` and the identity check
  still takes the asynchronous send that the SDK traces. *(Amended 2026-11-01: `RegisterLoopbackAsync` replaced a
  blocking `RegisterLoopback`.)*

### The stub's answers

- **Fidelity is best-effort.** The stub keeps minimal state for ordinary positive flows, and a test can override
  an answer for a negative case. A shape-faithful stub is enough to exercise the SDK; controller fidelity is not
  claimed. A contract fixture built from captured controller traffic was rejected, and the stub refers to no file
  outside the repository.
- **Answers are typed SDK model values**, over a wire tier internal to the stub, so they are easy to read and
  maintain and can carry state, like the fakes they replace. Raw XML answers were rejected as unreadable. Typed values
  cover the operations tests need rather than every operation, and any operation that sends a request can still
  be failed; the [design page](../../ihcclient_stub/docs/stubdesign.md) says which operations are typed and how the
  rest are answered.

### Stub markers

- **The indicator is a title suffix**, because a person and a launch gate both read the title. A banner, with or
  without the title, was rejected.
- **Lab's title keeps the stub marker for the whole of a stub process**, even after a person enters a real
  endpoint, so the title never contradicts the latch. A title that follows the current settings was rejected.
- **A stubbed run is marked `ihc.controller.stub=true` on its telemetry resource**, so stub latency never mixes
  into a real controller's figures. Leaving it unmarked, or suffixing the deployment environment name, was
  rejected.

### The amended testability rule

ARCHITECTURE.md's testability-switch paragraph, and the doc comment of `--test` in OpenVisual's `Program.cs`, held
that nothing behind a test flag may change what the application does. This record amends that rule in both:

> A testability input may change behaviour only by redirecting controller traffic to a stub. It may never relax
> validation, authentication or persistence against a real controller.

`--controller-stub` is a separate switch from `--test`, which stays observation-only. Both are parsed in `Main`.
The redirect qualifies because it can only point towards a loopback stub that proves its identity, never
towards a controller, and the indicator says so.

### Safeguards

Each layer is independent. Together they mean no single mistake reaches a controller. The
[design page](../../ihcclient_stub/docs/stubdesign.md) maps each layer to the suite that enforces it.

| Layer | Rule |
| --- | --- |
| L1 Reserved host | A generated stub endpoint lives under `.controllerstub.invalid`, which never resolves; a system proxy refuses it instead. `mock://` is not an HTTP scheme at all. |
| L2 Two-way refusal | In `Client.Post`, a stub endpoint goes only to the registry and is refused when unregistered; a non-stub endpoint never reaches a stub. Service construction refuses an unregistered `mock://`. |
| L3 Latch | While any registration is live, the socket path refuses every endpoint. It is the only preventive control against code that builds real settings by hand during a stub run. |
| L4 No stub in an application | No project outside `tests/` may reference `ihcclient_stub`, a project inside `tests/`, or the FakeItEasy package, so no application carries a stub, even transitively. Inside an executable, only the switch's entry points, `SwitchRequested` and `ApplySwitch`, and the predicate's named readers may reach the stub surface (O7). |
| L5 Fail-closed fixture | The OpenVisual stub suite fails, never ignores, when a settings file sits beside its assembly, and each test fails unless its settings are a stub's. |
| L6 Request destinations checked | In the OpenVisual stub suite, every request that the SDK's or the runtime's HTTP telemetry records must go to a stub host or the loopback address, and a canary call must be recorded, so a capture that sees nothing cannot pass. |
| L7 No outbound connection from the stub | `ihcclient_stub` has no client transport of its own: no `HttpClient` or handler, WCF client, or socket, WebSocket, QUIC, mail or network-information API. Its loopback host only listens, in framework code. |
| L8 Loud faults | An operation with no answer faults, a stub defect fails the call with `CONTROLLER_STUB_FAULTED`, and teardown asserts that the stub recorded neither. |
| L9 Reach guard | Every suite under `tests/` except the integration and architecture suites links the reach guard. A site in a suite's own code that builds real SDK services from settings must be admitted with a reason that is true of it, such as a registered stub endpoint or a latch probe on a reserved `.invalid` host. A network transport the suite builds itself -- an HTTP client or handler, a generated SOAP client, a socket or web client, or a `WebRequest` -- is never admitted, because its calls never enter `Client.Post`, and a subclass the suite declares of a real service or transport counts as its base. An unadmitted site stops the run before any test executes, a filtered run included: a check that ran as a test would report only after the offending call was sent, and a filtered run would skip it. |
| L10 Visible indicator | Both titles say stub while calls go to a stub, and stub suites assert it before their first action. |
| L11 Latch held for the run | Each controller-free suite that runs SDK code, except the stub suite, registers a stub that fails every call and holds it for the whole run, so the latch refuses every real endpoint there. The stub suite must see the latch both off and on, so it runs unlatched and may not stage the developer's settings file. |
| L12 No socket outside the SDK's named transport sites | In the SDK, only `Client`, the loopback client in `Ihc.Stubbing` and the OTLP probe `TelemetrySelfCheck` may construct a network client, only named methods may send on one, and only the generated layer may use WCF. An executable the switch is wired into, and the shared host bootstrap projects, may construct or use none at all; an executable that is not wired never runs switched or under a suite's latch, and is outside the rule. Routing, the latch and the switch all take effect in `Client.Post`, so a second send site, in the SDK or in an executable, would bypass them, and no runtime test would notice. |
| O1 Loopback-only switch | The switch accepts only `http` to an IP literal in `127.0.0.0/8` or `[::1]`, with an explicit port, a token path segment and no userinfo, written exactly `--controller-stub=` and its value. Anything else, `mock://` included, stops the executable, and so does a near spelling of the switch, which would otherwise be ignored. |
| O2 Locked-down loopback client | No proxy, no redirects, no cookie container, and a connect callback that refuses any non-loopback remote address. |
| O3 Identity preflight at registration | `RegisterLoopbackAsync` checks the host's token echo before its task completes, and `ApplySwitch` before it returns, so before any SOAP request and, in an executable, during startup. A tunnel or port-forward to a controller cannot echo a token it never saw. |
| O4 Per-response stub header | Every loopback response must carry the host's token in exactly one header, which also catches another process taking the port after the preflight. The request has then already been sent, and the refusal says so. |
| O5 File credentials never used | Under the switch, the file's user name and password are replaced by fixed stub-only values, and the stub never records a password. |
| O6 Launch gate | Every desktop launch starts a fresh loopback host and passes the switch. Once, between launch and the first action, the gate requires that the host answered a preflight and that the title shows the stub marker, and kills the process otherwise. Every launch goes through this one gated site: a driver launches only when that site asks with its launch flag, so a readiness check starts nothing, and the desktop driver refuses a launch without the switch. |
| O7 Stub surface confined | The stub surface is `Ihc.Stubbing` plus the `IsStubConfigured()` predicate. The sites outside `Ihc.Stubbing` that call into it, call the predicate or implement `IControllerStub` must equal a fixed list: the SDK's routing and construction checks; each executable's `Main` asking for and applying the switch, which Lab does through `Program.Compose`; the GET/STORE command line two console utilities share, asking of each argument whether it is the switch; the composition roots and titles that read the predicate; and the host bootstrap's stub-line print. The rule is an equality, so a dropped `ApplySwitch` fails as surely as an extra reader. A `mock://` or stub-host string literal may appear only in `Ihc.Stubbing`, and every executable that references `ihcclient`, itself or through a project it references, is either checked or listed as not wired, with its reason. |
| O8 Executables run from a clean copy | A test that starts a utility or OpenVisual starts it from a copy of its build output with a settings file of its own, naming an endpoint under a reserved `.invalid` host, so a run the switch failed to redirect fails in transport instead of reaching a controller. For a utility, only the switch can then produce the stub line. OpenVisual's desktop copy keeps the developer's telemetry and logging sections but no login, and its desktop run fails before any test unless the settings beside the executable name a `.invalid` endpoint and no login. |

*(Amended 2026-11-01: L9 now stops the run before any test executes; L11 keeps the developer's settings file away
from the stub suite; L12 also covers the executables; O8 also covers OpenVisual's desktop launches.)*

The SDK's `AllowDangerousInternTestCalls` gate is unchanged.

**No unsafe-call check.** The stub has no unsafe-call flag and no check over its journal. Routing decides
controller reach for every operation alike, and a journal check sees only calls that already reached the stub, so
it cannot see one that went to a controller. A write-verb rule, a fail-safe read-verb rule and a hardware-only
list were rejected on the same ground.

### Why L4's member rule and L7 are IL tests, not bans

ADR-004 moves a rule to a compile-time ban only when a namespace or type entry states it exactly and admits no
waiver it would need. Neither rule qualifies:

- **L4's member rule** allows only the switch's entry points and the predicate inside an executable, each only at
  named sites. A ban cannot allow part of a namespace it bans, or a member at one site but not another, and
  banning the other members one by one would be a hand-kept roster of an evolving API, which ADR-004 rules out.
- **L7** refuses any type deriving from `ClientBase<T>`, `HttpMessageInvoker` or `HttpMessageHandler`, whoever
  declares it, which a type ban cannot say, and it admits by name the listener-side members the stub needs, which
  a ban could express only as a local suppression. L12's executable half reuses L7's scan for the first reason,
  and admits nothing.

Both stay IL tests.

Confidence: high on the shape, which follows from the decisions above and the transport funnel. Top unresolved
uncertainty: whether the stub's typed answers stay close enough to a controller's behaviour for the positive
flows E10 will test; shape-only fidelity is accepted, and a test that needs more raises it on demand.

## Implications

### Positive

- The whole SDK runs in tests without a controller: only the transport answer is replaced.
- A launched desktop executable can be stub-driven, so end-to-end tests become possible without a controller.
- Lab no longer ships a test double, and FakeItEasy leaves the production dependency graph.
- Routing, the latch and both indicators share one predicate, so a title that says stub means no call reaches a controller.

### Negative

- A test feature ships in the public SDK: any package consumer can register a stub or pass the switch. It can
  only redirect towards a loopback stub that proves its identity, and the indicator says so.
- The registry is static, which is ambient context; the endpoint key travels explicitly in the settings value as
  the mitigation.
- A manual run takes an extra step: start the stub utility first, then the executable with the printed switch.
- A pass against the stub never means an operation works on a controller.
- The stub's typed values are its owners' reading of the wire; round-trip tests keep them consistent with the
  SDK, not with a controller.
- OpenVisual's controller work inherits two constraints. It may make no controller call before the main window
  shows: the launch gate runs after the window appears, so if the switch were dropped, a startup call would
  reach the settings file's endpoint before the gate could catch it. And its stubbed suite must fail on a refused call: L6 counts only HTTP
  client spans, which a refused call never opens, so a test that composes OpenVisual on a real endpoint would
  otherwise pass while testing no stub.

### Neutral

- The friend list grows by one test suite, `safe_stub_tests`, and by no product code (see What ships where).
- The generated SOAP layer stays as ADR-003 decided; the stub's use of it is one more visible opt-out, taken in
  its own project file.
- Telemetry for a run without the switch is unchanged.

## Confirmation

- `safe_architecture_tests` enforces L4, L7, L12, O7 and the rule that every guarded suite links the reach guard
  (L9), each with a seeded violation or an equality that proves it can fail, and the rule that keeps HTTP and WCF
  types out of the public stub signatures.
- `safe_stub_tests` tests routing, refusals, the latch, the switch's rules and stub fidelity through the real SDK,
  and `safe_unit_tests` runs each console utility from a clean copy (O8).
- The controller-free suites hold the latch for their whole run (L11), and the OpenVisual stub suite checks every
  request's destination (L6).
- The desktop end-to-end suite launches from a clean copy it checks before any test runs (O8), and gates every
  launch (O6).
- The public surface is recorded in the PublicAPI baseline and the approved SDK contract roster.

## Consultation

Ruled by the repository owner and sole maintainer, who took every ruling recorded above: on 2026-10-31, on a
design verified against the code and repeated independent reviews of it, and on 2026-11-01 for the asynchronous
loopback registration and the `safe_stub_tests` friend grant. No external stakeholders were consulted — the
project is single-maintainer.
