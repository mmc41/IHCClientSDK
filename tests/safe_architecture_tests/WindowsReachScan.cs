using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ihc.Tests
{
    /// <summary>
    /// Finds every way an assembly's authored code reaches Windows: a call into an API that exists only there, a
    /// field that holds one of its types, a P/Invoke into one of its system libraries, or a declaration marking
    /// the assembly's own code as Windows-only.
    ///
    /// <para><b>What "Windows-only" means here.</b> The same thing it means to the compiler. An API is
    /// Windows-only when the <see cref="SupportedOSPlatformAttribute"/>s that govern it name Windows and nothing
    /// else — read from the member, then from the types enclosing it, then from its assembly, the nearest level
    /// that declares any deciding. The walk matters: <c>Thread.SetApartmentState</c> is marked on the member,
    /// <c>System.Drawing.Common</c> and <c>Microsoft.Win32.Registry</c> on nothing but the assembly. An API that
    /// carries only <see cref="UnsupportedOSPlatformAttribute"/>s is not Windows-only, whatever it is unsupported
    /// on; <c>File.SetUnixFileMode</c> is the opposite of one. Reading the attribute rather than a namespace list
    /// is what keeps <c>System.Drawing.Color</c>, which lives in the cross-platform primitives, off the list.</para>
    ///
    /// <para><b>Where the assembly-level mark is read from.</b> Not from the loaded shared-framework assembly.
    /// That is the platform's IMPLEMENTATION, and on Windows the SDK stamps every one built for a Windows target
    /// with <c>SupportedOSPlatform("Windows")</c> — <c>System.Console</c>, <c>System.Net.Http</c> and the
    /// drawing primitives included, which would make every call in the repository reach. The contract the
    /// compiler enforces is in the REFERENCE assemblies, which carry no such mark for those and do carry
    /// <c>windows</c> for the registry and the ACL libraries; so for a shared-framework assembly the verdict is
    /// read from the reference pack that built this suite (<see cref="ReferencePack"/>). Member- and type-level
    /// marks are authored and identical in both, and are read from the loaded assembly.</para>
    ///
    /// <para><b>Why a guard does not excuse a call.</b> The platform analyzer accepts a Windows-only call inside
    /// <c>if (OperatingSystem.IsWindows())</c>, and accepts it in a member that declares itself Windows-only. Both
    /// compile; both are still a neutral project reaching Windows on its own. The rule this scan serves says
    /// Windows is reached only through a project named for it, so the IL edge is what counts and the guard is
    /// invisible to it — and a declaration is reported in its own right, because it is the cheapest way to make
    /// such calls compile.</para>
    ///
    /// <para><b>Doors.</b> A call into a first-party project that IS named for Windows is how a neutral project is
    /// supposed to get Windows behaviour, so targets declared in those assemblies are never reach. Nor are targets
    /// in the scanned assembly itself: what it declares is reported once, as a declaration, not again at every
    /// call.</para>
    /// </summary>
    internal static class WindowsReachScan
    {
        internal enum ReachKind
        {
            /// <summary>A call, construction or method-group load whose target is Windows-only.</summary>
            Calls,

            /// <summary>A field whose type — or an array element, generic argument or wrapped value inside it — is
            /// Windows-only. A field is state, and state of a Windows type makes the holder Windows-bound even
            /// when every call that produced it went through a door.</summary>
            Holds,

            /// <summary>A P/Invoke into a Windows system library. The platform analyzer never sees these: a
            /// <c>DllImport</c> carries no platform attribute, so this is the one reach that would otherwise
            /// compile silently anywhere.</summary>
            PInvokes,

            /// <summary>Authored code marked Windows-only, on the assembly, a type or a member.</summary>
            Declares,
        }

        internal readonly record struct WindowsReach(ReachKind Kind, string Site, string Target)
        {
            public override string ToString() => $"{Kind}: {Site} -> {Target}";
        }

        /// <summary>One assembly, the namespace root its authored code lives under, and the assembly names of the
        /// projects sanctioned to reach Windows — the doors a call may go through.</summary>
        internal sealed record Scope(Assembly Assembly, string AuthoredRoot, IReadOnlySet<string> Doors);

        private const BindingFlags All =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Every reach in the scope, sorted so a failure reads the same on every run.</summary>
        /// <remarks>MEMOISED per scope: a full IL decode of every member body, and the fixture asks more than one
        /// question of the same immutable assembly.</remarks>
        internal static IReadOnlyList<WindowsReach> Sites(Scope scope) => Cache.GetOrAdd(scope, Scan);

        private static readonly ConcurrentDictionary<Scope, IReadOnlyList<WindowsReach>> Cache = new();

        private static IReadOnlyList<WindowsReach> Scan(Scope scope)
        {
            var found = new List<WindowsReach>();
            string own = scope.Assembly.GetName().Name!;

            if (AssemblyVerdict(scope.Assembly) == true)
            {
                found.Add(new(ReachKind.Declares, own, "[assembly: SupportedOSPlatform]"));
            }

            foreach (Type type in AuthoredMembers.Types(scope.Assembly, scope.AuthoredRoot))
            {
                string typeName = ArchRuleHelpers.OutermostTypeName(type.FullName!);
                if (Verdict(type.GetCustomAttributes<SupportedOSPlatformAttribute>(inherit: false)) == true)
                {
                    found.Add(new(ReachKind.Declares, typeName, "[SupportedOSPlatform] on the type"));
                }

                foreach (FieldInfo field in type.GetFields(All | BindingFlags.DeclaredOnly))
                {
                    foreach (Type held in ArchRuleHelpers.TypeAndArguments(field.FieldType))
                    {
                        if (IsForeignWindowsOnly(held, scope))
                        {
                            found.Add(new(ReachKind.Holds, $"{typeName}.{field.Name}", held.FullName!));
                        }
                    }
                }
            }

            foreach (MethodBase member in AuthoredMembers.Of(scope.Assembly, scope.AuthoredRoot))
            {
                string site = ContainmentSite.For(member).ToString();
                if (Verdict(member.GetCustomAttributes<SupportedOSPlatformAttribute>(inherit: false)) == true)
                {
                    found.Add(new(ReachKind.Declares, site, "[SupportedOSPlatform] on the member"));
                }

                if (member.Attributes.HasFlag(MethodAttributes.PinvokeImpl)
                    && member.GetCustomAttribute<DllImportAttribute>() is { Value: var library }
                    && IsWindowsLibrary(library))
                {
                    found.Add(new(ReachKind.PInvokes, site, library));
                }

                foreach (MethodBase called in IlBody.CalledMethods(member))
                {
                    if (IsForeignWindowsOnly(called, scope))
                    {
                        found.Add(new(ReachKind.Calls, site, $"{called.DeclaringType?.FullName}.{called.Name}"));
                    }
                }
            }

            return [.. found.Distinct().OrderBy(reach => reach.ToString(), StringComparer.Ordinal)];
        }

        /// <summary>True for a Windows-only target the scanned assembly neither declares itself nor reaches
        /// through a door.</summary>
        private static bool IsForeignWindowsOnly(MemberInfo target, Scope scope)
        {
            Assembly home = Home(target);
            if (home == scope.Assembly || scope.Doors.Contains(home.GetName().Name!))
            {
                return false;
            }
            return IsWindowsOnly(target);
        }

        /// <summary>The compiler's own reading of a member's platform support: the nearest level that declares
        /// any <see cref="SupportedOSPlatformAttribute"/> decides, and the assembly decides last.</summary>
        internal static bool IsWindowsOnly(MemberInfo member)
        {
            for (MemberInfo? level = member; level is not null; level = Enclosing(level))
            {
                if (Verdict(level.GetCustomAttributes<SupportedOSPlatformAttribute>(inherit: false)) is { } verdict)
                {
                    return verdict;
                }
            }
            return AssemblyVerdict(Home(member)) == true;
        }

        /// <summary>The assembly-level verdict, from the reference assembly for a shared-framework assembly and
        /// from the loaded one otherwise — a package ships the single asset it is judged by, and a first-party
        /// assembly's mark is authored in this repository.</summary>
        internal static bool? AssemblyVerdict(Assembly assembly) =>
            ReferencePack.IsSharedFramework(assembly)
                ? Verdict(ReferencePack.SupportedPlatforms(assembly.GetName().Name!))
                : Verdict(assembly.GetCustomAttributes<SupportedOSPlatformAttribute>()
                    .Select(attribute => attribute.PlatformName));

        private static MemberInfo? Enclosing(MemberInfo level) =>
            level is Type type ? type.DeclaringType : level.DeclaringType;

        private static Assembly Home(MemberInfo member) =>
            (member as Type ?? member.DeclaringType)?.Assembly ?? member.Module.Assembly;

        private static bool? Verdict(IEnumerable<SupportedOSPlatformAttribute> declared) =>
            Verdict(declared.Select(attribute => attribute.PlatformName));

        /// <summary><c>null</c> when nothing is declared at this level; otherwise whether every platform named is
        /// Windows. A version suffix ("windows6.1") still names Windows.</summary>
        private static bool? Verdict(IEnumerable<string> platforms)
        {
            List<string> named = [.. platforms];
            return named.Count == 0
                ? null
                : named.All(platform => platform.StartsWith("windows", StringComparison.OrdinalIgnoreCase));
        }

        // The libraries a P/Invoke reaches Windows through. A name list is unavoidable here — a DllImport says
        // which library, not which platform — so it is kept honest two ways: the ".dll" suffix is the Windows
        // naming convention that a cross-platform native library avoids, because the runtime's probing on other
        // platforms does not strip it; and the bare names are the system libraries a desktop toolkit or a shell
        // integration actually imports. A P/Invoke into anything else is outside this rule, not sanctioned by it.
        private static readonly HashSet<string> WindowsSystemLibraries = new(StringComparer.OrdinalIgnoreCase)
        {
            "kernel32", "user32", "gdi32", "ole32", "oleaut32", "shell32", "advapi32", "comctl32", "comdlg32",
            "dwmapi", "uxtheme", "shcore", "ntdll", "winmm", "uiautomationcore", "shlwapi", "psapi", "crypt32",
            "secur32", "ws2_32", "iphlpapi", "userenv", "wtsapi32", "setupapi", "hid", "imm32", "propsys",
            "combase", "windowscodecs", "bcrypt", "ncrypt", "wintrust", "d2d1", "d3d11", "dxgi", "dcomp",
            "msvcrt", "ucrtbase",
        };

        internal static bool IsWindowsLibrary(string library)
        {
            string name = library.Trim();
            if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return WindowsSystemLibraries.Contains(name)
                || name.StartsWith("api-ms-win-", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("ext-ms-win-", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The reference assemblies of the running framework — the contract the compiler judged this suite by — for
    /// the one question the loaded implementation answers wrongly: which assemblies declare a platform.
    ///
    /// <para>Found beside the runtime: <c>shared/Microsoft.NETCore.App/&lt;version&gt;</c> is where the loaded
    /// assemblies came from, and the SDK that carries it installs the matching
    /// <c>packs/Microsoft.NETCore.App.Ref/&lt;version&gt;/ref/&lt;tfm&gt;</c> beside it. The pack of the running
    /// version is preferred; failing that the newest of the same major, which states the same contract. Nothing
    /// at all is an error rather than a lenient verdict — a rule that cannot read the contract has no business
    /// reporting green.</para>
    ///
    /// <para>Read with the metadata reader rather than loaded, because a reference assembly refuses to load for
    /// execution, and because all that is wanted is the string each attribute was constructed with.</para>
    /// </summary>
    internal static class ReferencePack
    {
        private const string SupportedOSPlatformAttributeName = "System.Runtime.Versioning.SupportedOSPlatformAttribute";

        private static readonly string RuntimeDirectory =
            Path.TrimEndingDirectorySeparator(RuntimeEnvironment.GetRuntimeDirectory());

        private static readonly Lazy<string> Directory = new(Locate);

        private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> Cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>True when the assembly was loaded from the shared framework, whose implementation carries the
        /// build-time platform stamp a reference assembly does not.</summary>
        internal static bool IsSharedFramework(Assembly assembly) =>
            !string.IsNullOrEmpty(assembly.Location)
            && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(assembly.Location)!),
                RuntimeDirectory, StringComparison.OrdinalIgnoreCase);

        /// <summary>The platforms the reference assembly of that name declares at the assembly level. Empty for
        /// an assembly that declares none — and for one the pack has no reference assembly of, which is what an
        /// implementation-only assembly such as <c>System.Private.CoreLib</c> is: its public surface is the
        /// contract of the facades that expose it, and those are judged by their own name.</summary>
        internal static IReadOnlyList<string> SupportedPlatforms(string assemblyName) =>
            Cache.GetOrAdd(assemblyName, Read);

        private static IReadOnlyList<string> Read(string assemblyName)
        {
            string path = Path.Combine(Directory.Value, assemblyName + ".dll");
            if (!File.Exists(path))
            {
                return [];
            }

            using FileStream stream = File.OpenRead(path);
            using var image = new PEReader(stream);
            MetadataReader metadata = image.GetMetadataReader();
            var platforms = new List<string>();
            foreach (CustomAttributeHandle handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
            {
                CustomAttribute attribute = metadata.GetCustomAttribute(handle);
                if (AttributeTypeName(metadata, attribute) != SupportedOSPlatformAttributeName)
                {
                    continue;
                }
                BlobReader value = metadata.GetBlobReader(attribute.Value);
                if (value.ReadUInt16() == 1 && value.ReadSerializedString() is { } platform)
                {
                    platforms.Add(platform);
                }
            }
            return platforms;
        }

        private static string? AttributeTypeName(MetadataReader metadata, CustomAttribute attribute)
        {
            EntityHandle type = attribute.Constructor.Kind switch
            {
                HandleKind.MemberReference =>
                    metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
                HandleKind.MethodDefinition =>
                    metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
                _ => default,
            };
            return type.Kind switch
            {
                HandleKind.TypeReference => FullName(metadata, metadata.GetTypeReference((TypeReferenceHandle)type)),
                HandleKind.TypeDefinition => FullName(metadata, metadata.GetTypeDefinition((TypeDefinitionHandle)type)),
                _ => null,
            };
        }

        private static string FullName(MetadataReader metadata, TypeReference type) =>
            metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);

        private static string FullName(MetadataReader metadata, TypeDefinition type) =>
            metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);

        private static string Locate()
        {
            // shared/Microsoft.NETCore.App/<version> -> the dotnet root, three levels up.
            string runtimeVersion = Path.GetFileName(RuntimeDirectory);
            string root = Path.GetFullPath(Path.Combine(RuntimeDirectory, "..", "..", ".."));
            string packs = Path.Combine(root, "packs", "Microsoft.NETCore.App.Ref");
            string framework = $"net{Environment.Version.Major}.{Environment.Version.Minor}";

            IEnumerable<string> candidates = new[] { Path.Combine(packs, runtimeVersion) }
                .Concat(System.IO.Directory.Exists(packs)
                    ? System.IO.Directory.GetDirectories(packs)
                        .Where(pack => Path.GetFileName(pack).StartsWith(Environment.Version.Major + ".", StringComparison.Ordinal))
                        .OrderByDescending(pack =>
                            System.Version.TryParse(Path.GetFileName(pack), out System.Version? parsed) ? parsed : new System.Version(0, 0))
                    : []);

            return candidates
                .Select(pack => Path.Combine(pack, "ref", framework))
                .FirstOrDefault(System.IO.Directory.Exists)
                ?? throw new InvalidOperationException(
                    $"no reference pack for {framework} under {packs}; the rule cannot read the platform contract "
                    + "of the shared framework, and a verdict without it would be a guess");
        }
    }
}
