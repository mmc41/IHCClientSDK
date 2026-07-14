using System.Collections.Immutable;

namespace ihc_openvisual.Services;

/// <summary>One occupied module terminal (US-050): the decoded <c>line.terminal</c> address and the product
/// terminal that occupies it.</summary>
public sealed record ModuleAddressEntry(string Address, string Product, string Terminal);

/// <summary>The Wired module address map (US-050): the addressed input-module and output-module terminals,
/// read-only. Unaddressed terminals do not appear.</summary>
public sealed record ModuleAddressMap(
    ImmutableArray<ModuleAddressEntry> InputModules, ImmutableArray<ModuleAddressEntry> OutputModules);
