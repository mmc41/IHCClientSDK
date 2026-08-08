using System;

namespace ihc_openvisual.ViewModels;

/// <summary>Marks immutable value snapshots consumed by the command-availability registry.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
internal sealed class CommandContextValueAttribute : Attribute { }
