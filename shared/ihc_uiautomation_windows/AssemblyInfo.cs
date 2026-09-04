using System.Runtime.Versioning;

// Every type here calls the Windows UI-Automation client or user32. Declared once, at the assembly, so the
// compiler holds callers to it — which is what lets this stay a plain net10.0 project: the assembly COMPILES
// on any platform and can only RUN on Windows. 6.1 is what CsWin32 annotates the generated members with.
[assembly: SupportedOSPlatform("windows6.1")]
