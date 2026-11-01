# ihc_uiautomation_windows

An application-agnostic toolkit for driving a Windows desktop application through UI Automation. It is development and test tooling: no shipped application references it.

## Purpose

Driving a real desktop application from a test takes Windows mechanics that have nothing to do with the application: a connection to the UI Automation client, element searches, control patterns, synthesized input in the right DPI context, screenshots, launch-and-wait. This project holds those mechanics once, so a driver built on it holds only its own policy: its verbs, its result envelope, its automation ids. Today that driver is [`UiaDriver`](../../tests/safe_visual_e2e_tests/Drivers/UiaDriver.cs), the desktop mode of `safe_visual_e2e_tests`, which drives `ihc_openvisual.exe`.

It also replaces fixed sleeps with `UiaWait.Until`, a bounded poll whose result says, on a timeout, how often it looked, for how long and what it last saw.

## Scope

| What | Public types | File |
| --- | --- | --- |
| The connection to the UI Automation client | `UiaSession` | `UiaSession.cs` |
| One live element: properties, children, searches, control patterns | `UiaElement` | `UiaElement.cs` |
| A process's visible top-level windows, topmost first | `DesktopWindows` | `DesktopWindows.cs` |
| Start an application and wait until it is driveable; ask a window to close; stop every instance | `AppLauncher`, `LaunchedApp` | `AppLauncher.cs` |
| Mouse and keyboard input, and taking the foreground | `Mouse`, `Keyboard`, `Foreground` | `SynthesizedInput.cs` |
| Per-monitor DPI awareness for a block of code | `DpiScope` | `DpiScope.cs` |
| A screen region saved as a PNG | `ScreenCapture` | `ScreenCapture.cs` |
| Bounded waits that explain a timeout | `UiaWait`, `UiaWaitResult<T>` | `UiaWait.cs` |
| The types public signatures use in place of generated ones | `UiaScope`, `UiaControlType`, `UiaRange`, `UiaExpandState`, `UiaKey`, `UiaModifiers`, `UiaGesture` | `UiaVocabulary.cs`, `UiaKeys.cs` |

Besides these, `Win32Handles.cs` keeps the generated window-handle pointer code in one internal place, `AssemblyInfo.cs` declares the platform, and `NativeMethods.txt` lists what CsWin32 generates.

Out of scope:

- **Anything about a particular application.** Verbs, result envelopes, automation ids and gesture grammars such as `{ENTER}` or `^z` belong to that application's driver.
- **The `aui-openvisual` skill.** It is a separate PowerShell tool and does not use this project.

## Portability

- **It compiles everywhere and runs only on Windows.** The target is plain `net10.0`, and [`AssemblyInfo.cs`](AssemblyInfo.cs) declares `[assembly: SupportedOSPlatform("windows6.1")]`, the floor CsWin32 annotates its generated members with, so the platform analyzer holds every caller to Windows. CI builds the solution on Linux, macOS and Windows, which keeps the toolkit compiling on all three.
- **There is no `-windows` target framework**, because one would force every consumer to multi-target. The `_windows` in the project name carries the platform fact instead, and it is load-bearing: [invariant 12](../../ARCHITECTURE.md#architectural-invariants) lets only a project whose name contains `windows` reference a Windows package or call a Windows-only API, and [`WindowsQuarantineArchitectureTests`](../../tests/safe_architecture_tests/WindowsQuarantineArchitectureTests.cs) enforces it. Every other project reaches Windows by referencing this one.
- **It calls raw UIA3 COM and `user32` through `Microsoft.Windows.CsWin32`**, a source generator that emits what [`NativeMethods.txt`](NativeMethods.txt) lists at build time and needs no `-windows` target. It is a private asset, so nothing of it reaches a consumer. `System.Drawing.Common`, which `ScreenCapture` uses, is a real runtime dependency that consumers inherit.

## Limitations

- **It needs an interactive, unlocked desktop session.** Windows refuses to move the cursor while the session is locked or a screensaver runs, and UIPI blocks input into a more privileged window, such as an elevated process's.
- **Create the `UiaSession` on an MTA thread.** On an STA thread that pumps no messages, calls into the automation client deadlock instead of failing. NUnit's worker threads are MTA by default.
- **Searches start from a process and go down.** A query from the desktop root fails when any running application's provider faults, so there is none: start from `DesktopWindows.OfProcess` or `AppLauncher` and search downward. There is no parent or ancestor search either.
- **Elements are live, and they fail soft.** Every property read goes to the running application. A destroyed element reads as defaults, with `ProcessId` 0, and an operation the element does not support returns `false`, `null` or an empty list instead of throwing, so check every result.
- **Input goes wherever the focus is.** Call `Foreground.Acquire` first, and send nothing when it returns `false`.
- **Measure and act in one DPI context.** On a scaled display, a rectangle read on a DPI-unaware thread and a click sent with per-monitor awareness are in different coordinate spaces, and the click misses. The input and capture methods enter a `DpiScope` themselves, so read the rectangle inside one too; `UiaDriver` wraps each verb in a single scope.
- **The input vocabulary is small.** The mouse clicks, double-clicks, turns the wheel and moves, but does not right-click or drag. Keys are the `UiaKey` set. `Keyboard.SendText` types text as Unicode characters, so the keyboard layout does not matter. Only the control patterns `NativeMethods.txt` names are wrapped, and the Value pattern only for reading.
- **Waits poll.** Nothing subscribes to UI Automation events.
- **A capture copies the screen**, so a covered, minimized or off-screen window captures whatever is in its rectangle.
- **`AppLauncher.KillAll` stops every process with the given name**, including one a person is using.
- **Only `UiaWait` has unit tests.** Everything else is verified by desktop runs alone; see [Maintenance](#maintenance).

## Usage

Reference the project, and mark the calling type `[SupportedOSPlatform("windows6.1")]`, as `UiaDriver` and `UiaWaitTests` do, or guard each call with `OperatingSystem.IsWindowsVersionAtLeast(6, 1)`. Without either, CA1416 fails the build, because warnings are errors in this repository.

```xml
<ProjectReference Include="..\..\shared\ihc_uiautomation_windows\ihc_uiautomation_windows.csproj" />
```

```csharp
using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using Ihc.UiAutomation;

[SupportedOSPlatform("windows6.1")]
static void Drive(string executablePath)
{
    using UiaSession session = new();                      // on an MTA thread
    using DpiScope dpi = DpiScope.Enter();                 // measure and act in one DPI context

    // "Ready" is the caller's to define: a first window can be a splash screen or an empty shell.
    LaunchedApp app = AppLauncher.Start(session, executablePath, [],
        isReady: window => window.FindFirstById("MainShell") is not null,
        timeout: TimeSpan.FromSeconds(60));
    using Process process = app.Process;                   // the caller owns it
    UiaElement shell = app.MainWindow
        ?? throw new InvalidOperationException($"no window: {app.Wait}");

    // A refusal is a false, a null or an empty list, never an exception.
    if (shell.FindFirstById("RefreshButton")?.Invoke() != true)
        throw new InvalidOperationException("RefreshButton is missing or cannot be invoked");

    // Input goes wherever the focus is, so take the foreground first.
    if (!Foreground.Acquire(shell.NativeWindowHandle)
        || !Keyboard.Send(new UiaGesture(UiaKey.S, UiaModifiers.Control)))
        throw new InvalidOperationException("Ctrl+S was not delivered");

    UiaWaitResult<UiaElement> status = UiaWait.Until(
        probe: () => shell.FindFirstById("StatusText"),
        satisfied: text => text.Name.Length > 0,
        timeout: TimeSpan.FromSeconds(5),
        describe: text => text is null ? "no StatusText element" : $"status '{text.Name}'");
    if (!status.Satisfied)
        throw new TimeoutException(status.ToString());     // polls, elapsed time, what it last saw

    ScreenCapture.Save(shell.BoundingRectangle, "shot.png");
    _ = AppLauncher.RequestClose(shell.NativeWindowHandle); // the application may still prompt
}
```

The automation ids are placeholders. In `safe_visual_e2e_tests`, only `UiaDriver.Launch` may call `AppLauncher.Start`, which [`LaunchGateTests`](../../tests/safe_visual_e2e_tests/LaunchGateTests.cs) enforces, and a scenario launches OpenVisual through `E2E.Launch`.

## Maintenance

- **To reach a new Win32 function or UIA interface**, add its name to `NativeMethods.txt`; CsWin32 generates it, and the types it references, at the next build. Ask for the enums `UIA_PROPERTY_ID` and `UIA_PATTERN_ID`, never a single `UIA_*Id` constant, which fails with PInvoke004.
- **Generated types are internal**, so no public signature can name one. Mirror what a caller needs as a toolkit type, as `UiaScope`, `UiaControlType` and `UiaKey` do, and expose a control pattern as the operation a driver wants, failing soft as `UiaElement` does. Read a string property through `GetCurrentPropertyValue`, never through a string-valued `Current*` accessor, which leaks the string; the remarks on `UiaElement.ReadString` explain why.
- **Keep it application-agnostic, and keep its deliberate absences**: no gesture grammar and no search from the desktop root.
- **Keep the `_windows` name and the plain `net10.0` target**; together they are the platform contract under [Portability](#portability). A Windows-only dependency that a consumer needs belongs here, or in another project named for Windows, never in the consumer.
- **Package versions are pinned** in [`Directory.Packages.props`](../../Directory.Packages.props). CsWin32 is pinned at a version measured to compile clean at `net10.0` under this repository's analyzers, so build the whole solution after an upgrade.

Verify a change with:

| Command | Verifies |
| --- | --- |
| `dotnet build IHCClientSDK.sln` | That it compiles; CI repeats this on Linux and macOS |
| `dotnet test tests/safe_unit_tests/safe_unit_tests.csproj` | `UiaWait`, through `UiaWaitTests` |
| `dotnet test tests/safe_architecture_tests/safe_architecture_tests.csproj` | The Windows quarantine rule, which uses this project as its positive control |
| `dotnet test tests/safe_visual_e2e_tests/safe_visual_e2e_tests.csproj` | Everything that calls the live automation client, in the suite's desktop mode |

Run the desktop mode deliberately: it takes over the screen for minutes and force-kills any running OpenVisual. CI runs that suite only headless, with an in-process driver, so the toolkit's live code never runs there and a green CI run says nothing about it. Coverage does not judge a change here either: [`.runsettings`](../../.runsettings) leaves this assembly out of the measured set on purpose, because its lines would measure the test harness rather than the product.

When the scope, the platform contract or the test routing changes, update the toolkit's entries in [ARCHITECTURE.md](../../ARCHITECTURE.md#codemap) and [TESTSTRATEGY.md](../../TESTSTRATEGY.md#levels-and-suites), and the header comment in [`ihc_uiautomation_windows.csproj`](ihc_uiautomation_windows.csproj), in the same change.
