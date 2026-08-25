<#
.SYNOPSIS
  aui — semantic UI-Automation driver for the IHC OpenVisual desktop app (Windows only).

.DESCRIPTION
  Drives a running (or freshly launched) IHC OpenVisual instance through Windows UI
  Automation, exposing a stable `domain.verb` command vocabulary (e.g. `tree.select`,
  `project.save`, `product.insert`). Every command prints ONE JSON result envelope to
  stdout and maps its outcome Code to a process exit code, so multi-step runs are
  scriptable and diffable.

  The command surface is declared in commands.json (the vocabulary / self-description),
  and each command is executed by a generic *mechanism*. Adding a new command usually
  means adding one row to commands.json that reuses an existing mechanism — no code.

.USAGE
  pwsh aui.ps1 <domain> <verb> [positional] [--flag value] [--switch]
  pwsh aui.ps1 catalog commands            # list the whole vocabulary + status
  pwsh aui.ps1 doctor                      # readiness preflight
  pwsh aui.ps1 session status
  pwsh aui.ps1 tree select "Localities/Kitchen" --tree TV1
  pwsh aui.ps1 project save

  Windows-only: on macOS/Linux it prints a Code="PlatformUnsupported" error and exits 2.
#>

[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $CmdArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ─────────────────────────────────────────────────────────────────────────────
# Paths / constants
# ─────────────────────────────────────────────────────────────────────────────
$script:SkillRoot   = Split-Path -Parent $PSScriptRoot        # .../aui-openvisual
$script:RegistryPath = Join-Path $PSScriptRoot 'commands.json'
$script:AppName     = 'IHC OpenVisual'
$script:ProcName    = 'ihc_openvisual'                         # ihc_openvisual.exe
$script:WindowSuffix = 'IHC OpenVisual'                        # main window title suffix
# The resolved main window, set once in Main. Held so "is this element the main window?" can be asked
# by IDENTITY rather than by title -- the app has a dialog TITLED "About IHC OpenVisual", so a title
# test answers "that is the main window" about a modal. Initialized here because Set-StrictMode makes
# reading an unassigned variable an error.
$script:MainWindow  = $null

# ---------------------------------------------------------------------------
# Coordinate-space contract
# ---------------------------------------------------------------------------
# Every coordinate this driver emits comes from a UIA BoundingRectangle, and UIA rects in this host
# are PHYSICAL pixels. That was MEASURED, not assumed, on a 175% display: the window's UIA extent
# (1750x1190) matches its true physical extent (DWM extended frame bounds, 1754x1244) to within
# 0.3%, where the virtualized alternative would have been ~1002x711; and a point derived from a UIA
# rect, set with SetCursorPos from this DPI-unaware host, reads back unchanged from a
# per-monitor-aware thread. That second reading is also WHY the driver's gestures land correctly:
# the rects it reads and the cursor it sets speak the same space.
#
# The name is spelled HERE AND ONLY HERE. A tag mistyped at an emission site is unfalsifiable in
# JSON -- a reader has no way to tell "physicl" from a space it has not heard of.
$script:NativeCoordSpace = 'physical'

# ─────────────────────────────────────────────────────────────────────────────
# Result envelope + output (mirrors the reference tool's CommandResult contract)
# ─────────────────────────────────────────────────────────────────────────────
# Code → exit-code tiers: 0 ok · 1 ok-but-unverified/inapplicable · 2 usage/policy ·
# 3 target resolution · 4 runtime/interaction failure.
#
# EVERY Code this driver can emit MUST have a row here. An unmapped Code falls through to the `else`
# below and exits 1 -- the "app not running / not implemented" tier -- so a hard failure reports as an
# inapplicable no-op. That shipped: DialogBlocked (a file picker already open, or a modal still up
# after a project open/save) exited 1 and read to a scripted caller as "nothing to do here".
#
# The rows marked RESERVED are the shared vocabulary this driver keeps in step with the vendor-side
# `ihcvisual` driver so one harness reader can parse both. They are not emitted here today; they are
# mapped anyway so that wiring one later cannot silently land in the wrong tier.
$script:ExitTier = @{
    'Ok' = 0
    'OkUnverified' = 1                                                            # RESERVED
    'AppNotRunning' = 1; 'NotImplemented' = 1
    'PlatformUnsupported' = 2; 'NotAllowed' = 2; 'ConfirmationRequired' = 2
    'Unverified' = 2; 'PreconditionMissing' = 2; 'InvalidInput' = 2
    'BadScope' = 2                                                                # RESERVED
    'TargetNotFound' = 3; 'TargetExists' = 3; 'ControlNotFound' = 3
    'TargetAmbiguous' = 3; 'DiscoveryFailed' = 3                                  # RESERVED
    'DialogNotFound' = 4; 'DialogTimeout' = 4; 'DialogError' = 4; 'DialogBlocked' = 4
    'NoEffect' = 4; 'MutationFailed' = 4; 'CaptureFailed' = 4
    'CaptureOccluded' = 4
    'PostFailed' = 4                                                              # RESERVED
}

function New-Result {
    param(
        [bool] $Ok, [string] $Code, [string] $Message,
        [bool] $Verified = $false,
        [string[]] $Warnings = @(),
        $Context = $null, $Screenshot = $null, $Data = $null
    )
    [ordered]@{
        ok = $Ok; code = $Code; message = $Message; verified = $Verified
        warnings = @($Warnings); context = $Context
        screenshot = $Screenshot; data = $Data
    }
}

function Write-Result {
    param($Result)
    # Compress keeps runs diffable line-by-line. Write-Output (not Console.Out) so the JSON flows on the
    # success stream — captured by callers AND printed to stdout — while `exit` still sets the exit code.
    #
    # DEPTH IS A CORRECTNESS SETTING, NOT A TUNING KNOB. Past -Depth, ConvertTo-Json does not fail or
    # truncate the envelope: it calls .ToString() on the node, so the JSON stays well-formed and `ok:true`
    # while the payload silently becomes the literal "System.Collections.Specialized.OrderedDictionary".
    # (The only hint is a WARNING on stderr, which every scripted caller here discards.)
    #
    # The budget is not the walk depth: the envelope costs ~3 (result > data > titles[]) and every nested
    # level costs TWO (the node object + its children[] array). So a `menu dump-bar --depth 6` needs
    # 3 + 6*2 = 15 — and the old value of 12 blew out at exactly the product leaves, which is the one
    # thing that walk exists to reach (measured verify2 2026-07-16: LK FUGA's children came back as 18
    # copies of that type name). 64 covers any walk this driver can produce, and costs nothing when the
    # data is shallower.
    $json = $Result | ConvertTo-Json -Depth 64 -Compress
    Write-Output $json
    $code = [string]$Result.code
    $tier = if ($script:ExitTier.ContainsKey($code)) { $script:ExitTier[$code] } else { 1 }
    exit $tier
}

# ─────────────────────────────────────────────────────────────────────────────
# Platform guard — Windows only, by design (UI Automation is a Windows API)
# ─────────────────────────────────────────────────────────────────────────────
function Assert-Windows {
    $isWin = $false
    if (Test-Path variable:IsWindows) { $isWin = $IsWindows } else { $isWin = $true } # PS5.1 ⇒ Windows
    if (-not $isWin) {
        Write-Result (New-Result -Ok $false -Code 'PlatformUnsupported' `
            -Message "aui-openvisual drives the app through Windows UI Automation and runs on Windows only. Current OS is not Windows.")
    }
}

# ---------------------------------------------------------------------------
# Coordinate conversion (pure) and the declared-point serializer
# ---------------------------------------------------------------------------
# Conversion is a PURE function of (point, monitorLogicalOrigin, monitorPhysicalOrigin, scale):
#
#   physical = physicalOrigin + Round((logical  - logicalOrigin ) * scale)
#   logical  = logicalOrigin  + Round((physical - physicalOrigin) / scale)
#
# Both axes convert independently. Round is half-AWAY-FROM-ZERO and is applied to the OFFSET from
# the origin, never to the absolute coordinate, so behaviour is symmetric about the monitor origin
# and unaffected by a negative or non-zero one -- a monitor placed left of the primary has negative
# coordinates, and rounding the absolute value there would bend the result toward the desktop origin.
#
# The conversion is LOSSY physical -> logical at non-integer scales. Round-tripping is not expected
# to be exact and nothing asserts that it is; a driver that forced exactness would be lying about one
# of the two numbers it publishes.
function New-MonitorGeometry {
    param(
        [int] $LogicalX, [int] $LogicalY,
        [int] $PhysicalX, [int] $PhysicalY,
        [double] $Scale
    )
    return [ordered]@{
        logicalOrigin  = [ordered]@{ x = $LogicalX;  y = $LogicalY }
        physicalOrigin = [ordered]@{ x = $PhysicalX; y = $PhysicalY }
        scale          = [double]$Scale
    }
}

function Get-RoundedOffset {
    param([double] $Offset)
    return [int][math]::Round($Offset, 0, [System.MidpointRounding]::AwayFromZero)
}

function ConvertTo-LogicalPoint {
    param([int] $X, [int] $Y, $Geometry)
    return [ordered]@{
        x = [int]$Geometry.logicalOrigin.x + (Get-RoundedOffset (($X - $Geometry.physicalOrigin.x) / $Geometry.scale))
        y = [int]$Geometry.logicalOrigin.y + (Get-RoundedOffset (($Y - $Geometry.physicalOrigin.y) / $Geometry.scale))
    }
}

function ConvertTo-PhysicalPoint {
    param([int] $X, [int] $Y, $Geometry)
    return [ordered]@{
        x = [int]$Geometry.physicalOrigin.x + (Get-RoundedOffset (($X - $Geometry.logicalOrigin.x) * $Geometry.scale))
        y = [int]$Geometry.physicalOrigin.y + (Get-RoundedOffset (($Y - $Geometry.logicalOrigin.y) * $Geometry.scale))
    }
}

# The emitted shape. The native x/y stay exactly what the driver read; the sibling is a PLAIN point
# with no nested space tag, because stating the same fact twice gives it two places to disagree.
# When the monitor geometry could not be probed the sibling is OMITTED and `space` is still declared:
# a sibling computed from an assumed scale of 1.0 would be the confident wrong answer this whole
# contract exists to remove, while dropping `space` too would hide which space x/y are already in.
function New-DeclaredPoint {
    param([int] $X, [int] $Y, $Geometry)
    $declared = [ordered]@{ x = [int]$X; y = [int]$Y; space = $script:NativeCoordSpace }
    if ($Geometry) { $declared.logical = (ConvertTo-LogicalPoint -X $X -Y $Y -Geometry $Geometry) }
    return $declared
}

# The same contract for a rectangle. The sibling is derived from BOTH CORNERS with the extent
# RE-DERIVED from them, never by scaling width/height on their own: an isolated extent is rounded a
# second time, independently of where the rectangle sits, so it drifts by a pixel on exactly the
# rectangles whose corners do not happen to align. At scale 1.75 a 3 px extent at x=101 is 1 px
# logical (58 -> 59), while scaling the extent alone claims 2 -- and a driver that gets it right for
# most rectangles and wrong for some is harder to distrust than one that is always wrong.
function New-DeclaredRect {
    param([int] $X, [int] $Y, [int] $Width, [int] $Height, $Geometry)
    $declared = [ordered]@{
        x = [int]$X; y = [int]$Y; width = [int]$Width; height = [int]$Height
        space = $script:NativeCoordSpace
    }
    if ($Geometry) {
        $topLeft = ConvertTo-LogicalPoint -X $X -Y $Y -Geometry $Geometry
        $bottomRight = ConvertTo-LogicalPoint -X ($X + $Width) -Y ($Y + $Height) -Geometry $Geometry
        $declared.logical = [ordered]@{
            x = $topLeft.x; y = $topLeft.y
            width  = $bottomRight.x - $topLeft.x
            height = $bottomRight.y - $topLeft.y
        }
    }
    return $declared
}

# A screenshot's dimensions belong to the same contract: an image and a point in ONE envelope used to
# be in two spaces with nothing marking either. CopyFromScreen counts real screen pixels, so the
# dimensions are native.
#
# There is no logical sibling here, and that is not an omission: width/height are the PNG's actual
# pixel count, so a converted pair would describe a file that does not exist at that size. The only
# question a caller has is which space these pixels are counted in, and `space` answers it.
function New-ScreenshotMetadata {
    param([string] $Path, [int] $Width, [int] $Height, [string] $Scope)
    return [ordered]@{
        path = $Path; width = [int]$Width; height = [int]$Height
        space = $script:NativeCoordSpace
        scope = $Scope; mimeType = 'image/png'
    }
}

# Reads the monitor containing a point in BOTH spaces rather than deriving one from the other.
# Returns $null when any part of the probe fails, which is what makes the omission in
# New-DeclaredPoint reachable instead of theoretical.
function Get-MonitorGeometry {
    param([int] $X, [int] $Y)
    $raw = [Aui.Win32]::MonitorGeometry($X, $Y)
    if (-not $raw) { return $null }
    return (New-MonitorGeometry -LogicalX $raw['logicalX'] -LogicalY $raw['logicalY'] `
                                -PhysicalX $raw['physicalX'] -PhysicalY $raw['physicalY'] `
                                -Scale ([double]$raw['dpi'] / 96.0))
}

# What `doctor` publishes: the monitor the app is on, in both spaces, with the scaling in play. This
# is what makes a coordinate answer CHECKABLE -- a caller can apply the formula by hand to these
# numbers and see the siblings fall out. It is also what makes the whole bug class discoverable: an
# author on a 100% display reads `scale: 1` here and knows why their machine never reproduces it.
#
# Both rectangles are rebuilt in a fixed key order rather than passed through, so the published shape
# cannot depend on how the caller happened to build its input.
function New-DisplayBlock {
    param([string] $Monitor, [int] $Dpi, $LogicalRect, $PhysicalRect)
    return [ordered]@{
        monitor  = $Monitor
        dpi      = [int]$Dpi
        logical  = [ordered]@{ x = [int]$LogicalRect.x;  y = [int]$LogicalRect.y
                               width = [int]$LogicalRect.width;  height = [int]$LogicalRect.height }
        physical = [ordered]@{ x = [int]$PhysicalRect.x; y = [int]$PhysicalRect.y
                               width = [int]$PhysicalRect.width; height = [int]$PhysicalRect.height }
        scale    = [double]$Dpi / 96.0
    }
}

function Get-DisplayBlock {
    param([int] $X, [int] $Y)
    $raw = [Aui.Win32]::MonitorGeometry($X, $Y)
    if (-not $raw) { return $null }
    return (New-DisplayBlock -Monitor $raw['device'] -Dpi $raw['dpi'] `
        -LogicalRect  ([ordered]@{ x = $raw['logicalX'];  y = $raw['logicalY']
                                   width = $raw['logicalWidth'];  height = $raw['logicalHeight'] }) `
        -PhysicalRect ([ordered]@{ x = $raw['physicalX']; y = $raw['physicalY']
                                   width = $raw['physicalWidth']; height = $raw['physicalHeight'] }))
}

# ─────────────────────────────────────────────────────────────────────────────
# UIA bootstrap
# ─────────────────────────────────────────────────────────────────────────────
function Initialize-Uia {
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    if (-not ([System.Management.Automation.PSTypeName]'Aui.Win32').Type) {
        Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Threading;
namespace Aui {
  [StructLayout(LayoutKind.Sequential)] public struct AuiPoint { public int X; public int Y; }
  [StructLayout(LayoutKind.Sequential)] public struct AuiRect { public int Left; public int Top; public int Right; public int Bottom; }
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct AuiMonInfo {
    public int cbSize; public AuiRect rcMonitor; public AuiRect rcWork; public int dwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string szDevice; }

  public static class Win32 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    public const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004, RIGHTDOWN = 0x0008, RIGHTUP = 0x0010;
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(AuiPoint pt);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr h, uint m, IntPtr w, IntPtr l);
    const uint BM_CLICK = 0x00F5, WM_CLOSE = 0x0010, GA_ROOT = 2, GW_OWNER = 4;

    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(AuiPoint pt, int flags);
    [DllImport("user32.dll", EntryPoint="GetMonitorInfoW", CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr mon, ref AuiMonInfo mi);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr mon, int type, out uint dpiX, out uint dpiY);
    const int MONITOR_DEFAULTTONEAREST = 2;
    static readonly IntPtr PER_MONITOR_AWARE_V2 = new IntPtr(-4);
    static readonly IntPtr DPI_UNAWARE = new IntPtr(-1);

    // The monitor containing a point, READ in BOTH spaces plus its identity and effective DPI.
    // Windows answers GetMonitorInfo with the VIRTUALIZED rect to a DPI-unaware caller and with the
    // DEVICE-PIXEL rect to a per-monitor-aware one: same monitor, same call, two frames -- exactly the
    // pair of origins the conversion needs. Neither rectangle is computed from the other, because
    // virtualization rounds per monitor (2194 * 1.75 is 3839.5, not 3840) and a secondary display's
    // virtualized origin is not its physical origin scaled either.
    //
    // BOTH reads assert their OWN context rather than inheriting the ambient one, and this is not
    // defensive tidiness -- it was measured here: loading UIAutomationClient flips this process from
    // PROCESS_DPI_UNAWARE to SYSTEM_AWARE, after which the "unaware" read silently returns the
    // PHYSICAL rect. The driver loads UIA before every command, so trusting the ambient context would
    // have made the logical rect a copy of the physical one -- a wrong answer indistinguishable from
    // a right one on a single monitor at the origin, and wrong for every secondary display.
    //
    // Returns null when ANY step fails -- including on a Windows too old for the thread-context APIs.
    // A null here is what makes "omit the sibling" reachable rather than theoretical: the alternative,
    // assuming a scale, publishes a fabricated coordinate that reads exactly like a measured one.
    public static System.Collections.Hashtable MonitorGeometry(int x, int y) {
      try {
        AuiPoint p; p.X = x; p.Y = y;
        IntPtr mon = MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST);
        if (mon == IntPtr.Zero) return null;

        AuiMonInfo logical = NewMonInfo();
        bool logicalOk;
        IntPtr entry = SetThreadDpiAwarenessContext(DPI_UNAWARE);
        try { logicalOk = GetMonitorInfo(mon, ref logical); }
        finally { SetThreadDpiAwarenessContext(entry); }
        if (!logicalOk) return null;

        AuiMonInfo physical = NewMonInfo();
        bool physicalOk;
        uint dpiX = 0, dpiY = 0;
        int hr;
        IntPtr previous = SetThreadDpiAwarenessContext(PER_MONITOR_AWARE_V2);
        try {
          physicalOk = GetMonitorInfo(mon, ref physical);
          hr = GetDpiForMonitor(mon, 0, out dpiX, out dpiY);   // 0 = MDT_EFFECTIVE_DPI
        } finally { SetThreadDpiAwarenessContext(previous); }
        if (!physicalOk || hr != 0 || dpiX == 0) return null;

        var geometry = new System.Collections.Hashtable();
        geometry["device"] = physical.szDevice;
        geometry["dpi"] = (int)dpiX;
        geometry["logicalX"] = logical.rcMonitor.Left;
        geometry["logicalY"] = logical.rcMonitor.Top;
        geometry["logicalWidth"] = logical.rcMonitor.Right - logical.rcMonitor.Left;
        geometry["logicalHeight"] = logical.rcMonitor.Bottom - logical.rcMonitor.Top;
        geometry["physicalX"] = physical.rcMonitor.Left;
        geometry["physicalY"] = physical.rcMonitor.Top;
        geometry["physicalWidth"] = physical.rcMonitor.Right - physical.rcMonitor.Left;
        geometry["physicalHeight"] = physical.rcMonitor.Bottom - physical.rcMonitor.Top;
        return geometry;
      } catch { return null; }
    }

    static AuiMonInfo NewMonInfo() {
      AuiMonInfo mi = new AuiMonInfo();
      mi.cbSize = Marshal.SizeOf(typeof(AuiMonInfo));
      mi.szDevice = "";
      return mi;
    }

    static string TextOf(IntPtr h) {
      var sb = new System.Text.StringBuilder(512);
      GetWindowTextW(h, sb, 512);
      return sb.ToString();
    }

    // The caller supplies a live menu item's midpoint as positive popup identity. Native guards then
    // refuse the main HWND and any foreign, hidden, titled, non-Avalonia, or non-owned target.
    public static bool CloseOwnedPopupAtPoint(int pid, IntPtr main, int x, int y) {
      if (pid <= 0 || main == IntPtr.Zero || !IsWindow(main)) return false;
      AuiPoint point; point.X = x; point.Y = y;
      IntPtr underPoint = WindowFromPoint(point);
      IntPtr target = underPoint == IntPtr.Zero ? IntPtr.Zero : GetAncestor(underPoint, GA_ROOT);
      if (target == IntPtr.Zero || target == main || !IsWindow(target) || !IsWindowVisible(target)) return false;
      uint ownerPid;
      GetWindowThreadProcessId(target, out ownerPid);
      if (ownerPid != (uint)pid || GetWindow(target, GW_OWNER) != main) return false;
      var className = new System.Text.StringBuilder(128);
      GetClassNameW(target, className, className.Capacity);
      if (!className.ToString().StartsWith("Avalonia-", StringComparison.Ordinal)) return false;
      if (TextOf(target).Length != 0) return false;
      return PostMessageW(target, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    // A dialog OWNED by another dialog (the shell's replace prompt over a Save picker) is not a
    // UI-Automation child of the desktop, so the UIA walk cannot see it at all; EnumWindows can.
    //
    // Found by CLASS AND IDENTITY, not by caption. The caption is localized -- this used to match the
    // literal "Confirm Save As", so on a Danish Windows (which this project targets; dialog.cancel
    // already falls back to Annuller/Nej) the prompt was never found, nobody answered it, and
    // `project save-as --overwrite` sat there until DialogTimeout with the prompt still up. The
    // language-independent facts are: it is a #32770, it belongs to this process, it is visible, and
    // it is NOT the picker that raised it.
    public static IntPtr FindDialogExcept(int pid, IntPtr except) {
      IntPtr hit = IntPtr.Zero;
      EnumWindows((h, l) => {
        uint p; GetWindowThreadProcessId(h, out p);
        if (p != (uint)pid || !IsWindowVisible(h) || h == except) return true;
        var cn = new System.Text.StringBuilder(64);
        GetClassNameW(h, cn, 64);
        if (cn.ToString() != "#32770") return true;
        hit = h; return false;
      }, IntPtr.Zero);
      return hit;
    }

    // Clicks by handle rather than by keystroke: the default button of the overwrite prompt is "No",
    // so pressing Enter would silently DECLINE the replace and look like a rejected path.
    public static string ClickDialogButton(IntPtr dlg, string caption) {
      IntPtr btn = IntPtr.Zero;
      var seen = new System.Collections.Generic.List<string>();
      EnumChildWindows(dlg, (h, l) => {
        var cn = new System.Text.StringBuilder(64);
        GetClassNameW(h, cn, 64);
        if (cn.ToString() != "Button") return true;
        string t = TextOf(h).Replace("&", "");
        seen.Add(t);
        if (string.Equals(t, caption, StringComparison.OrdinalIgnoreCase)) { btn = h; return false; }
        return true;
      }, IntPtr.Zero);
      if (btn == IntPtr.Zero) return "buttons: " + string.Join(", ", seen);
      SendMessageW(btn, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
      return null;
    }

    const byte VK_MENU = 0x12;
    const uint KEYUP = 0x0002;

    // A drag is press, TRAVEL, release. The travel is what makes it a drag: a toolkit only begins one after
    // the pointer moves past its drag threshold while the button is held, and it drops where the pointer last
    // moved to -- so a single jump between down and up is delivered as a plain click on the source row.
    // The first nudge crosses the threshold; the interpolated steps give the drop target time to register
    // the pointer entering it.
    public static void Drag(int x1, int y1, int x2, int y2) {
      SetCursorPos(x1, y1);
      Thread.Sleep(60);
      mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
      Thread.Sleep(80);
      SetCursorPos(x1 + 6, y1 + 6);       // exceed the drag threshold so the gesture starts
      Thread.Sleep(60);
      const int steps = 12;
      for (int i = 1; i <= steps; i++) {
        SetCursorPos(x1 + (x2 - x1) * i / steps, y1 + (y2 - y1) * i / steps);
        Thread.Sleep(30);
      }
      SetCursorPos(x2, y2);
      Thread.Sleep(120);
      mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
      Thread.Sleep(80);
    }

    // A press needs a measurable down->up duration; a zero-length press is dropped by
    // Avalonia's menu hit-testing often enough to look like a flaky no-op.
    public static void Click(int x, int y) {
      SetCursorPos(x, y);
      Thread.Sleep(40);
      mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
      Thread.Sleep(40);
      mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }

    // A REAL right-click, which Shift+F10 is not: the keyboard gesture acts on whatever already has
    // focus and so can never move the caret, which is precisely the question C2 asks of a right-click.
    public static void RightClick(int x, int y) {
      SetCursorPos(x, y);
      Thread.Sleep(40);
      mouse_event(RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
      Thread.Sleep(40);
      mouse_event(RIGHTUP, 0, 0, 0, UIntPtr.Zero);
    }

    // Activate and REPORT WHETHER IT WORKED. SetForegroundWindow returns true while silently
    // refusing to change the foreground when the caller lacks foreground rights (the usual case
    // for an automation host launched in the background) -- so its return value is worthless and
    // GetForegroundWindow must be read back. The ALT tap is the documented way to acquire those
    // rights: it is delivered to the CURRENT foreground window (not ours), which grants this
    // thread the right to activate. Callers MUST refuse to send input when this returns false --
    // otherwise keystrokes land in whatever app the user actually has in front.
    public static bool Activate(IntPtr h) {
      if (h == IntPtr.Zero) return false;
      ShowWindow(h, 9);   // SW_RESTORE
      SetForegroundWindow(h);
      Thread.Sleep(80);
      if (GetForegroundWindow() == h) return true;
      keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
      keybd_event(VK_MENU, 0, KEYUP, UIntPtr.Zero);
      ShowWindow(h, 9);
      SetForegroundWindow(h);
      Thread.Sleep(150);
      return GetForegroundWindow() == h;
    }
  }
}
"@
    }
    $script:AE   = [System.Windows.Automation.AutomationElement]
    $script:Walk = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $script:Desc = [System.Windows.Automation.TreeScope]::Descendants
    $script:ChildScope = [System.Windows.Automation.TreeScope]::Children
}

# ─────────────────────────────────────────────────────────────────────────────
# UIA helpers
# ─────────────────────────────────────────────────────────────────────────────
function New-PropCondition {
    param($Property, $Value)
    New-Object System.Windows.Automation.PropertyCondition($Property, $Value)
}

function Find-ByAutomationId {
    param($Root, [string] $Id, $Scope = $null)
    if ($null -eq $Scope) { $Scope = $script:Desc }
    $cond = New-PropCondition ([System.Windows.Automation.AutomationElement]::AutomationIdProperty) $Id
    $Root.FindFirst($Scope, $cond)
}

function Find-ByName {
    param($Root, [string] $Name, $ControlType = $null, $Scope = $null)
    if ($null -eq $Scope) { $Scope = $script:Desc }
    $nameCond = New-PropCondition ([System.Windows.Automation.AutomationElement]::NameProperty) $Name
    if ($null -ne $ControlType) {
        $ctCond = New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) $ControlType
        $cond = New-Object System.Windows.Automation.AndCondition($nameCond, $ctCond)
    } else { $cond = $nameCond }
    $Root.FindFirst($Scope, $cond)
}

function Get-Pattern {
    param($Element, $Pattern)
    try { return $Element.GetCurrentPattern($Pattern) } catch { return $null }
}

function Set-Foreground {
    param($Window)
    # Returns $true only when $Window verifiably owns the foreground. Never assume success:
    # synthesized keys go to the FOREGROUND window, so a silent failure here would type into
    # whatever the user has in front (an editor, a shell) instead of the app.
    try {
        $h = [IntPtr]$Window.Current.NativeWindowHandle
        if ($h -eq [IntPtr]::Zero) { return $false }
        $ok = [Aui.Win32]::Activate($h)
        if ($ok) { Start-Sleep -Milliseconds 120 }
        return $ok
    } catch { return $false }
}

function Assert-Foreground {
    param($Window)
    # Guard for every input-synthesizing mechanism: returns $null when the app has the
    # foreground, otherwise the failure result the mechanism must return unchanged.
    if (Set-Foreground $Window) { return $null }
    return (New-Result -Ok $false -Code 'PreconditionMissing' `
        -Message 'Could not bring IHC OpenVisual to the foreground, so input would be delivered to another application. Refusing to send it. Close any window holding a modal/foreground lock and retry.' `
        -Context (Get-Context $Window))
}

# ─────────────────────────────────────────────────────────────────────────────
# App attach / launch / context
# ─────────────────────────────────────────────────────────────────────────────
function Get-AppProcess {
    Get-Process -Name $script:ProcName -ErrorAction SilentlyContinue | Select-Object -First 1
}

# Why a --launch did not produce a window. Reported by doctor, because "IHC OpenVisual is not running
# (launch it or pass --launch)" is a useless -- and misleading -- thing to say to somebody who DID pass
# --launch: the two real causes (no built exe, or a start-up that never showed a window) need different
# fixes and neither is "pass --launch".
$script:LaunchProblem = $null

function Start-App {
    param([int] $TimeoutSec = 40, [string] $ProjectPath)
    $exe = Find-AppExe
    if (-not $exe) {
        $script:LaunchProblem = 'Could not find ihc_openvisual.exe: nothing under applications/ihc_openvisual/bin and nothing on PATH. Build it first: dotnet build applications/ihc_openvisual/ihc_openvisual.csproj'
        return $null
    }
    # By default no launch arguments: the app comes up on the standard empty project with no start-up prompt.
    #
    # With --path, the file is passed as the app's OWN start-up argument -- the "Open with..." route it already
    # supports. That is the only way to open a project without the file dialog, and the file dialog needs the
    # foreground, which an automation host frequently cannot take. Before this, `--launch --path` silently
    # ignored the path and left the caller driving the untitled empty project while believing it had the file
    # open: a self-consistent, completely wrong answer. Note the app opens it AFTER the window is shown (so an
    # open-failure dialog has an owner), so a caller must still wait for the title rather than for readiness.
    if ($ProjectPath) {
        Start-Process -FilePath $exe -ArgumentList @($ProjectPath) | Out-Null
    } else {
        Start-Process -FilePath $exe | Out-Null
    }
    $win = Wait-MainWindow -TimeoutSec $TimeoutSec
    if (-not $win) {
        $script:LaunchProblem = "Started '$exe' but no main window appeared within $TimeoutSec s."
    }
    return $win
}

function Find-AppExe {
    # Prefer a built exe under the repo; fall back to PATH. The skill lives inside the repo,
    # so walk up to the repo root and probe the standard build output.
    $repo = $script:SkillRoot
    for ($i = 0; $i -lt 8 -and $repo; $i++) { $repo = Split-Path -Parent $repo; if (Test-Path (Join-Path $repo 'IHCClientSDK.sln')) { break } }
    if ($repo) {
        $candidates = Get-ChildItem -Path (Join-Path $repo 'applications/ihc_openvisual/bin') -Recurse -Filter 'ihc_openvisual.exe' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending
        if ($candidates) { return $candidates[0].FullName }
    }
    $cmd = Get-Command 'ihc_openvisual.exe' -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

# The app's MAIN window -- never one of its dialogs.
#
# This used to be FindFirst(Children, ProcessId=pid): no ControlType filter, no title test, first hit
# wins. The desktop's children include EVERY top-level window the process owns, and OpenVisual shows
# its dialogs as separate top-level Windows (Views\ResultDialog.cs -> ShowDialog(owner)), so with any
# dialog up the resolved "main window" could be that dialog. Everything downstream then reads off the
# wrong element: Find-ByAutomationId 'ToolbarSave'/'InstallationTree' miss, context.toolbarVisible goes
# false, and doctor reports deepUiaUsable:false and blames an INTEGRITY MISMATCH -- a confident wrong
# diagnosis of "you left a dialog open".
#
# Identify it by what only the main window has, in order:
#   1. the InstallationTree descendant -- definitive whenever deep UIA works at all;
#   2. else the title suffix, which still resolves under UIPI blindness (a top-level window's NAME is
#      readable cross-integrity while its descendants are not -- the state doctor exists to report);
#   3. else any Window of the process, so a mid-startup titleless frame cannot stall the wait.
# Tier 2 can still pick "About IHC OpenVisual" if the app is BOTH blind and showing About; nothing is
# drivable in that state anyway, and doctor says so.
function Wait-MainWindow {
    param([int] $TimeoutSec = 40, [int] $ProcId = 0)
    if ($ProcId -eq 0) {
        $p = Get-AppProcess
        if ($p) { $ProcId = $p.Id }
    }
    $windowCond = New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Window)
    $cond = if ($ProcId -ne 0) {
        New-Object System.Windows.Automation.AndCondition($windowCond,
            (New-PropCondition ([System.Windows.Automation.AutomationElement]::ProcessIdProperty) $ProcId))
    } else { $windowCond }
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $wins = @($script:AE::RootElement.FindAll($script:ChildScope, $cond))
        $byTitle = $null
        foreach ($w in $wins) {
            if (Find-ByAutomationId $w 'InstallationTree') { return $w }
            if ((-not $byTitle) -and $w.Current.Name -like "*$($script:WindowSuffix)") { $byTitle = $w }
        }
        if ($byTitle) { return $byTitle }
        if ($ProcId -ne 0 -and $wins.Count -gt 0) { return $wins[0] }
        Start-Sleep -Milliseconds 400
    }
    return $null
}

function Resolve-MainWindow {
    param([switch] $Launch, [string] $ProjectPath)
    $p = Get-AppProcess
    if (-not $p) {
        if ($Launch) { return (Start-App -ProjectPath $ProjectPath) }
        return $null
    }
    return (Wait-MainWindow -TimeoutSec 15 -ProcId $p.Id)
}

# The status bar is visible iff a Text element sits in the window's bottom strip.
#
# This used to probe Find-ByName 'Danish project locale' -- the locale flag's AutomationProperties.Name
# -- and reported FALSE while the bar was plainly visible and working ("Copied Lampeudtag" in the very
# same screenshot). The flag is a bare <Border>, and Avalonia gives a Border no automation peer, so that
# name NEVER surfaces: the probe asked for something that cannot exist. Verified by dumping the live UIA
# tree (2026-07-16): the whole status-bar band contains exactly ONE element, the StatusText <TextBlock>,
# and it carries no AutomationId.
#
# So key on that Text's POSITION, which is what actually distinguishes it. The window's control-view
# children are flat (Avalonia surfaces no peer for the Borders/DockPanels), and only three Text elements
# exist at that level: the two pane headers ('Installation'/'Functions', y at the TOP) and the status
# text (y at the BOTTOM). Tree rows are TreeItems inside the Tree subtrees, not loose Text. The status
# bar's <Border> is IsVisible-bound, so hiding it removes the Text from the tree entirely -- which is
# exactly the signal we want, and it is verified by effect (toggle off -> false, on -> true).
#
# Deliberately NOT matched by name: StatusText is the last-action hint and changes constantly
# ("Ready" / "Copied Lampeudtag" / "Jumped to ..."), so any name match would be a new false negative.
# Returns the status Text element, or $null when the bar is hidden.
#
# POSITION ALONE IS NOT ENOUGH, and the reason is the tree. The two panes reach the window's bottom
# edge, tree rows expose their labels as Text descendants (Get-TreeItemOffsetClickPoint depends on
# exactly that), and this search is over DESCENDANTS -- the "only three Text elements exist" reading is
# true of the window's control-view CHILDREN, not of its subtree. So with the status bar HIDDEN the
# bottom band is pure tree, and the first row in it satisfied the band test: statusBarVisible came back
# TRUE with the bar off, and statusText returned a NODE LABEL. That poisons the toggle's own effect
# check and the statusText oracle that node.cut/copy/paste and link.* are documented to verify through
# -- a wrong reading, not a missing one. Hence: search the window's own children first (where the
# status text really is the only bottom-band Text, and which is far cheaper on a large project), and
# reject any candidate that lives inside a tree either way.
function Test-InsideTree {
    param($El)
    $cur = $El
    for ($i = 0; $i -lt 16 -and $cur; $i++) {
        try { $cur = $script:Walk.GetParent($cur) } catch { return $false }
        if (-not $cur) { return $false }
        $ct = $cur.Current.ControlType.ProgrammaticName
        if ($ct -eq 'ControlType.Tree' -or $ct -eq 'ControlType.TreeItem') { return $true }
        if ($ct -eq 'ControlType.Window') { return $false }
    }
    return $false
}

function Get-StatusBarElement {
    param($Window)
    $wr = $Window.Current.BoundingRectangle
    if ($wr.IsEmpty) { return $null }
    $bandTop = $wr.Y + $wr.Height - 60   # the bar measures ~24px high and sits at the very bottom
    $textCond = New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Text)
    $texts = @($Window.FindAll($script:ChildScope, $textCond))
    if ($texts.Count -eq 0) { $texts = @($Window.FindAll($script:Desc, $textCond)) }
    foreach ($t in $texts) {
        $r = $t.Current.BoundingRectangle
        # A collapsed element reports an empty/infinite rect; never treat that as "on screen".
        if ($r.IsEmpty -or [double]::IsInfinity($r.Y) -or [double]::IsInfinity($r.Height)) { continue }
        if ($r.Y -lt $bandTop -or $r.Y -ge ($wr.Y + $wr.Height)) { continue }
        if (Test-InsideTree $t) { continue }   # a tree row's label sitting in the band, not the status bar
        return $t
    }
    return $null
}

function Get-Context {
    param($Window)
    if ($null -eq $Window) {
        # SAME KEY SET, SAME ORDER as the live snapshot below. This branch used to omit focusedPane and
        # selections entirely, so a caller that reads context.selections got a missing key exactly when
        # the app was not running -- the case it is most likely to be inspecting.
        return New-GoneContext
    }
    # A context snapshot is a READBACK, never the action -- so a window that has gone away while we were
    # reading it is an ANSWER ("the app is not running any more"), not a failure of the command that was
    # just performed. Without this, `menu invoke --id app.exit` closed the app exactly as asked and then
    # died inspecting the corpse: every property read below throws ElementNotAvailable / IsEmpty on a
    # destroyed element, the exception escaped to the top-level handler, and the run reported exit 1 /
    # MutationFailed for a command that had SUCCEEDED. A caller (or CI) reading that verdict retries a
    # non-idempotent action or fails a green run.
    try {
        return Get-ContextCore $Window
    } catch {
        return New-GoneContext
    }
}

# The snapshot of "no app to look at". One definition, so the live and the gone shapes cannot drift apart.
function New-GoneContext {
    [ordered]@{ appRunning = $false; windowTitle = $null
        toolbarVisible = $null; statusBarVisible = $null; statusText = $null
        openModal = $null; focusedPane = $null; selection = $null; selections = @() }
}

function Get-ContextCore {
    param($Window)
    # Toolbar/status-bar presence: detect by a child that only exists while the bar is shown.
    # The toolbar is easy — the Save button carries an AutomationId and vanishes with the bar.
    $toolbar = Find-ByAutomationId $Window 'ToolbarSave'
    $statusEl = Get-StatusBarElement $Window
    $modal = Get-OpenModal $Window
    # @() at the call site, never ,@($out) in the function: a 1-element return unrolls to the bare
    # [ordered] dict, whose .Count is its KEY count (2), which would silently corrupt $sels[0].
    $sels = @(Get-SelectedNodeNames $Window)
    [ordered]@{
        appRunning = $true
        windowTitle = $Window.Current.Name
        toolbarVisible = [bool]$toolbar
        statusBarVisible = [bool]$statusEl
        # The status bar is this app's last-action hint, and it NAMES the node it acted on ("Cut X",
        # "Copied X", "Jumped to X"). That makes it the only readback that can prove a selection-relative
        # gesture hit the node it was ADDRESSED with rather than the one that was selected — the §2 bug.
        # Several commands (node.cut/copy/paste, link.*) have no other observable effect at all: Cut only
        # stages a move, so the tree is deliberately unchanged and a tree diff proves nothing.
        statusText = $(if ($statusEl) { $statusEl.Current.Name } else { $null })
        openModal = $modal
        # Which pane holds keyboard FOCUS -- the only way to observe F6, whose whole effect is to move
        # it. Deliberately NOT derived from `selection` below: focus and selection are different things
        # and F6 moves only the first (see Get-FocusedPane).
        focusedPane = Get-FocusedPane $Window
        # selection = the first pane that has one (kept for compatibility); selections = EVERY pane.
        # Reporting only the first hides the other pane's caret entirely, and cross-pane effects are
        # real: the vendor's F4 jumps the OPPOSITE pane's caret and leaves the typed-into pane's
        # alone, so a single-pane readback records "nothing happened" for a jump that did happen.
        selection = $(if ($sels.Count -gt 0) { $sels[0] } else { $null })
        selections = $sels
    }
}

function Get-OpenModal {
    param($Window)
    # Projection of the open modal (if any) for the context snapshot.
    #
    # `id` carries the dialog's AutomationId, which since 2026-08-08 every OpenVisual window declares
    # (AboutWindow, PropertiesWindow, ReportPickerWindow, … and ConfirmDialog for the code-built message
    # boxes). It is the LOCALE-INDEPENDENT handle on "which dialog is up": `title` is Danish and several
    # dialogs retitle themselves from project data at runtime (the properties dialog takes the node's
    # name), so a run that branches on the title is branching on user content. Empty for a window that
    # declares none, so an empty id is "unlabelled", never "no dialog" -- that is what $null is for.
    $w = Get-OpenModalWindow
    if ($w) { return [ordered]@{ title = $w.Current.Name; id = [string]$w.Current.AutomationId } }
    return $null
}

# Which pane holds the KEYBOARD FOCUS: 'TV1' | 'TV2' | 'Other' | 'None' | 'Unknown'.
#
# NOT the same thing as `selection`, and the difference is the whole point. This driver's
# selectedBefore/selectedAfter readback is SELECTION -- and `context.selection` is only "the first pane
# that has a selected item", so it is not even a per-pane indicator. F6's entire effect is to move
# FOCUS; it leaves both selections exactly where they were, so a selection readback records "nothing
# happened" for the one gesture this field exists to measure. Select() sets selection; Shift+F10 follows
# keyboard focus; the two are not the same thing (see Set-TreeSelection's note).
#
# 'Unknown' = UIA could not answer (the field admitting ignorance); 'None' = nothing holds focus.
# Both are distinct from 'Other' (focus is in the app, but in neither tree -- a menu, a dialog, a
# toolbar), and none of the three may be reported as TV1: a constant dressed as a reading is exactly
# the trap the vendor driver's hardcoded selection.tree turned out to be.
function Get-FocusedPane {
    param($Window)
    $focused = $null
    try { $focused = $script:AE::FocusedElement } catch { return 'Unknown' }
    if (-not $focused) { return 'None' }

    foreach ($pair in @(@('InstallationTree', 'TV1'), @('FunctionsTree', 'TV2'))) {
        $tree = Find-ByAutomationId $Window $pair[0]
        if (-not $tree) { continue }
        # Walk UP from the focused element: focus lands on the TreeViewItem (or deeper), not the
        # TreeView itself, so an identity test against the pane alone would answer 'Other' every time.
        $cur = $focused
        while ($cur) {
            if ([System.Windows.Automation.Automation]::Compare($cur, $tree)) { return $pair[1] }
            try { $cur = $script:Walk.GetParent($cur) } catch { break }
        }
    }
    return 'Other'
}

function Get-SelectedNodeNames {
    param($Window)
    # ASK THE PROVIDER FOR THE SELECTED ROW, do not enumerate and filter here. This used to FindAll every
    # TreeItem in both panes and then read IsSelected off each one in-process -- two full-subtree walks
    # per call, on a control the driver's own --depth note measures at ~20 000 UIA round trips for the
    # 2.9 MB project. Get-Context calls this on EVERY command (node.rightClick's settle loop calls
    # Get-Context up to eight times), so that cost was paid several times per invocation to learn two
    # labels. FindFirst with IsSelected in the condition answers the same question in one query that
    # stops at the hit.
    $out = @()
    foreach ($id in @('InstallationTree', 'FunctionsTree')) {
        $tree = Find-ByAutomationId $Window $id
        if (-not $tree) { continue }
        $cond = New-Object System.Windows.Automation.AndCondition(
            (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::TreeItem)),
            (New-PropCondition ([System.Windows.Automation.SelectionItemPattern]::IsSelectedProperty) $true))
        $hit = $tree.FindFirst($script:Desc, $cond)
        if ($hit) { $out += [ordered]@{ tree = $id; name = $hit.Current.Name } }
    }
    return $out
}

# ─────────────────────────────────────────────────────────────────────────────
# Mechanisms
# ─────────────────────────────────────────────────────────────────────────────
function Invoke-Mechanism-Passive {
    param($Spec, $Opts, $Window)
    $ctx = Get-Context $Window
    if ($Spec.id -eq 'doctor') {
        $issues = @()
        if (-not $Window) {
            if ($script:LaunchProblem) { $issues += $script:LaunchProblem }
            else { $issues += 'IHC OpenVisual is not running (launch it or pass --launch).' }
        }

        # READINESS IS GATED ON A REAL DESCENDANT READ, not on the root window resolving.
        #
        # This used to be `$ready = [bool]$Window` -- which is answerable from a CROSS-INTEGRITY
        # enumeration, because a top-level window is visible to any process while its DESCENDANTS are
        # not. So a non-elevated session driving an elevated OpenVisual got Ready:true with
        # toolbarVisible/statusBarVisible/statusText/selections ALL blank against a screenshot showing
        # every one of them -- reproduced verbatim in this repo on 2026-07-16. A doctor that cannot
        # detect UIPI blindness is worse than no doctor: it invites a whole session of fabricated cells,
        # every one of them a confident reading of an empty tree.
        #
        # The probe is the InstallationTree, which is a descendant, always present once a window exists,
        # and needs no project loaded to resolve. If it cannot be found, deep UIA is blind -- almost
        # always an integrity mismatch (the app elevated, this session not, or vice versa).
        $deepOk = $false
        if ($Window) {
            $tree = Find-ByAutomationId $Window 'InstallationTree'
            $deepOk = [bool]$tree
            if (-not $deepOk) {
                $issues += 'Deep UI Automation is BLIND: the main window resolves but its descendants do not (InstallationTree not found). This is almost always an integrity mismatch -- run this session at the SAME elevation as IHC OpenVisual. Every deep field (selection, status text, tree contents) would read as empty/false, which is indistinguishable from the app genuinely being in that state: do not record anything from this session until it is fixed.'
            }
        }
        $ready = [bool]$Window -and $deepOk
        $code = if (-not $Window) { 'AppNotRunning' } elseif (-not $deepOk) { 'PreconditionMissing' } else { 'Ok' }
        $msg  = if (-not $Window) { 'App not running.' } elseif (-not $deepOk) { 'NOT ready: deep UI Automation is blind (see issues).' } else { 'Ready.' }
        # ADDITIVE: `display` describes the monitor the app is on and never participates in `ready`.
        # A machine whose geometry cannot be probed is still perfectly usable -- it just cannot publish
        # the conversion, so the block is null there for the same reason a sibling is omitted.
        # Probed from the WINDOW's own top-left, so a session with OpenVisual dragged to a second
        # display describes THAT display rather than the primary one.
        $display = $null
        if ($Window) {
            $winRect = $Window.Current.BoundingRectangle
            if (-not ([double]::IsInfinity($winRect.X) -or [double]::IsNaN($winRect.X))) {
                $display = Get-DisplayBlock -X ([int]$winRect.X) -Y ([int]$winRect.Y)
            }
        }
        $data = [ordered]@{ ready = $ready; osWindows = $true; appRunning = [bool]$Window
            windowUsable = [bool]$Window; deepUiaUsable = $deepOk; issues = $issues
            display = $display }
        return (New-Result -Ok $ready -Code $code -Message $msg -Verified $true -Context $ctx -Data $data)
    }
    return (New-Result -Ok ([bool]$Window) -Code ($(if ($Window) {'Ok'} else {'AppNotRunning'})) `
        -Message ($(if ($Window) {'Context captured.'} else {'App not running.'})) -Verified $true -Context $ctx)
}

function Invoke-Mechanism-Static {
    param($Spec, $Opts, $Window)
    # catalog.commands — self-describe the whole vocabulary.
    $rows = foreach ($c in $script:Registry.commands) {
        [ordered]@{ id = $c.id; status = $c.status; mechanism = $c.mechanism
            route = (Get-Route $c.mechanism)
            mutating = $c.mutating; description = $c.description }
    }
    return (New-Result -Ok $true -Code 'Ok' -Message "$($rows.Count) commands." -Verified $true `
        -Context (Get-Context $Window) -Data @($rows))
}

function Invoke-Mechanism-Invoke {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $el = Find-ByAutomationId $Window $Spec.automationId
    if (-not $el) { return (New-Result -Ok $false -Code 'ControlNotFound' -Message "No element with AutomationId '$($Spec.automationId)'." -Context (Get-Context $Window)) }
    if (-not $el.Current.IsEnabled) {
        return (New-Result -Ok $false -Code 'PreconditionMissing' -Message "Control '$($Spec.automationId)' is disabled." -Context (Get-Context $Window))
    }
    $inv = Get-Pattern $el ([System.Windows.Automation.InvokePattern]::Pattern)
    if (-not $inv) { return (New-Result -Ok $false -Code 'NotAllowed' -Message "Control '$($Spec.automationId)' has no Invoke pattern." -Context (Get-Context $Window)) }
    $inv.Invoke()
    Start-Sleep -Milliseconds 250
    return (New-Result -Ok $true -Code 'Ok' -Message "Invoked '$($Spec.automationId)'." -Verified $false -Context (Get-Context $Window))
}

# StrictMode-safe read of an OPTIONAL registry flag.
#
# `Set-StrictMode -Version Latest` (top of this file) makes `$Spec.missingProp` THROW rather than yield
# $null, so a bare `$Spec.selectionRelative` takes down every row that simply omits the flag. That
# shipped and was live-caught here: edit.undo / edit.redo / view.configuration -- the three key commands
# that are deliberately GLOBAL and therefore carry no selectionRelative -- each returned
# MutationFailed "The property 'selectionRelative' cannot be found on this object" on every call, i.e.
# undo was completely dead while still registered "confirmed". Use this for anything optional; the
# menu-bar dump already uses the same PSObject.Properties guard inline.
function Test-SpecFlag {
    param($Spec, [string] $Name)
    $prop = $Spec.PSObject.Properties[$Name]
    return ($null -ne $prop -and $prop.Value -eq $true)
}

function Invoke-Mechanism-Key {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    # Checked here as well as in keySend, so the gate belongs to the GESTURE rather than to whichever
    # mechanism happens to send it: no row carries {DELETE} today, and a future one added with the
    # customary `gates: []` must not reopen the door key.send just had closed.
    if ((Test-DestructiveGesture $Spec.gesture) -and -not $Opts.ContainsKey('confirm-destructive')) {
        return (New-Result -Ok $false -Code 'ConfirmationRequired' `
            -Message "$($Spec.id) sends '$($Spec.gesture)', which the app routes to an irreversible removal; re-run with --confirm-destructive." `
            -Context (Get-Context $Window))
    }
    # A gesture lands on whatever the app currently has selected, so a selection-relative command
    # MUST own its selection. This used to ignore --path entirely: `node.cut --path X` cut whatever
    # happened to be selected and reported ok — a silent wrong-target MUTATION, the same class as the
    # ihcvisual MCP's SimplePost focus bug. Honour --path here, and refuse rather than act hopefully.
    $path = Get-PathOpt $Opts
    $target = $null
    if ($path) {
        $sel = Select-TreePath $Window (Resolve-TreeId $Opts) $path
        if (-not $sel.ok) { return (New-Result -Ok $false -Code $sel.code -Message $sel.message -Context (Get-Context $Window)) }
        $target = $path
    }
    elseif (Test-SpecFlag $Spec 'selectionRelative') {
        # Deliberately do NOT default to a pane: forcing TV1 would make a TV2 command silently take
        # the wrong node — the regression the MCP nearly shipped when it added focus-first. Require an
        # existing selection and act on that, or fail.
        $ctx = Get-Context $Window
        if (-not $ctx.selection) {
            return (New-Result -Ok $false -Code 'PreconditionMissing' `
                -Message "$($Spec.id) is selection-relative but nothing is selected: pass --path (and --tree), or select a node first." `
                -Context $ctx)
        }
        $target = "$($ctx.selection.tree)/$($ctx.selection.name)"
    }
    $guard = Assert-Foreground $Window
    if ($guard) { return $guard }
    [System.Windows.Forms.SendKeys]::SendWait($Spec.gesture)
    Start-Sleep -Milliseconds 300
    $msg = "Sent gesture '$($Spec.gesture)'"
    if ($target) { $msg += " to '$target'" }
    return (New-Result -Ok $true -Code 'Ok' -Message "$msg." -Verified $false `
        -Context (Get-Context $Window) -Data ([ordered]@{ gesture = "$($Spec.gesture)"; target = $target }))
}

# TRUE children of a tree row, via the control-view TreeWalker rather than FindAll(Children).
#
# FindAll with TreeScope::Children does NOT return a TreeViewItem's own rows here: under an EXPANDED row
# it returned the wrong set entirely — "Stue/0" resolved to Stue's SECOND product and "Stue/1" to a
# product belonging to ENTRÉ, i.e. the enumeration both skipped a real child and ran past the end of the
# subtree. Label lookups failed the same way, which is what made a whole node kind (pins) look
# unaddressable. The walker follows the real parent/child links.
#
# ONE implementation for EVERY tree walk, which is the point of it being a function: tree.dump kept its own
# FindAll(Children) recursion long after this was diagnosed and written down ten lines from it, so the
# driver's structural oracle — the thing whole comparison runs are diffed from — was built by the very
# enumeration its own path resolver had rejected as wrong, and reported verified:true over it.
function Get-TreeItemChildren {
    param($El)
    $kids = @()
    $w = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $n = $w.GetFirstChild($El)
    while ($n) {
        if ($n.Current.ControlType -eq [System.Windows.Automation.ControlType]::TreeItem) { $kids += $n }
        $n = $w.GetNextSibling($n)
    }
    return @($kids)
}

# UIA does not realize child rows for collapsed TreeItems, so expand before taking child-count snapshots.
function Get-ExpandedTreeItemChildNames {
    param($El)
    $ecp = Get-Pattern $El ([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    if ($ecp -and $ecp.Current.ExpandCollapseState -eq [System.Windows.Automation.ExpandCollapseState]::Collapsed) {
        try { $ecp.Expand(); Start-Sleep -Milliseconds 150 } catch { }
    }
    return @(Get-TreeItemChildren $El | ForEach-Object { [string]$_.Current.Name })
}

# Split a label path into its segments. A literal '/' inside a node label is written '\/'.
#
# Without an escape a whole node KIND is unaddressable by label: the app labels every link row with the
# opposite end's full path joined by " / " (TreeLabelFormatter.LinkOppositePath), so those rows split into
# fragments that match no sibling and could only be reached by index — the fragile form the docs tell you
# to avoid. Same grammar as the menu walker's --menu-path (Resolve-MenuSegments), which has taken '\/'
# since the product catalog grew a "Lux / Temperatur sensor" leaf; the two disagreeing was an oversight.
# Backward-compatible: a path without '\/' splits exactly as before.
function Split-TreePath {
    param([string] $Path)
    if (-not $Path) { return @() }
    return @($Path -split '(?<!\\)/' | Where-Object { $_ -ne '' } | ForEach-Object { $_ -replace '\\/', '/' })
}

# Decide WHICH child a path segment names, given the sibling labels in order. Pure, and separate from the
# UIA walk, because every interesting case here is a decision rather than a lookup: an index segment, an
# exact label, a TRIMMED label — and a label that matches MORE THAN ONE sibling.
#
# That last one is why this returns a code instead of an element. Duplicate sibling labels are ordinary in
# this data (two products of the same type under one locality; two link rows onto the same target), and
# first-match-wins made a MUTATING command act on the wrong row while reporting success — the one failure
# mode a driver must not have, since the envelope looks identical either way. TargetAmbiguous was already
# in the shared code vocabulary, reserved and mapped to tier 3, waiting for exactly this.
#
# Every branch returns the SAME keys (ok/code/index/message). Under Set-StrictMode a caller reading
# `.code` off a success-shaped hashtable that omits it throws — a uniform shape is what makes the result
# safe to inspect without first knowing which branch produced it.
function Resolve-ChildIndex {
    param([string[]] $Names, [string] $Segment)
    $count = @($Names).Count
    if ($Segment -match '^\d+$') {
        $idx = [int]$Segment
        if ($idx -lt $count) { return @{ ok = $true; code = 'Ok'; index = $idx; message = "Index $idx of $count." } }
        return @{ ok = $false; code = 'TargetNotFound'; index = -1
                  message = "Index segment '$Segment' is out of range: there are $count child rows (0..$($count - 1))." }
    }
    # Exact first, then TRIMMED. Real node labels in this data end in a space ("LK FUGA Tryk 2 tast (Ved
    # dør) ") and a path cannot carry that reliably: as the LAST segment the shell/tokenizer drops it,
    # mid-path it survives, so the SAME row was addressable at the end of a path and unreachable in the
    # middle of one. That asymmetry made a whole node kind look unaddressable for several passes.
    # Case-insensitive, as the -eq it replaces was.
    $hits = @()
    for ($i = 0; $i -lt $count; $i++) { if ($Names[$i] -eq $Segment) { $hits += $i } }
    if (@($hits).Count -eq 0) {
        $wanted = $Segment.Trim()
        for ($i = 0; $i -lt $count; $i++) { if ("$($Names[$i])".Trim() -eq $wanted) { $hits += $i } }
    }
    if (@($hits).Count -eq 0) {
        return @{ ok = $false; code = 'TargetNotFound'; index = -1; message = "Path segment '$Segment' not found." }
    }
    if (@($hits).Count -gt 1) {
        return @{ ok = $false; code = 'TargetAmbiguous'; index = -1
                  message = ("Path segment '$Segment' matches $(@($hits).Count) sibling rows (indices $($hits -join ', ')); " +
                             "refusing to guess. Address the one you mean by index, e.g. '$($hits[0])'.") }
    }
    return @{ ok = $true; code = 'Ok'; index = $hits[0]; message = "Matched sibling $($hits[0])." }
}

# Resolve a label/index path to its TreeItem, expanding ancestors as needed. Does NOT select it.
#
# Split out of Select-TreePath because "find this row" and "select this row" are different questions,
# and node.rightClick has to ask the first WITHOUT the second: C2 asks whether a right-click MOVES the
# selection, so a driver that selects the target first has answered its own question and can only ever
# report "yes".
function Resolve-TreePath {
    param($Window, [string] $TreeId, [string] $Path)
    $tree = Find-ByAutomationId $Window $TreeId
    if (-not $tree) { return @{ ok = $false; code = 'ControlNotFound'; message = "Tree '$TreeId' not found." } }
    $segments = Split-TreePath $Path
    $current = $tree
    foreach ($seg in $segments) {
        # Expand current so children realize (root Tree has no ExpandCollapse; TreeItems do).
        $ecp = Get-Pattern $current ([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
        if ($ecp -and $ecp.Current.ExpandCollapseState -eq [System.Windows.Automation.ExpandCollapseState]::Collapsed) {
            try { $ecp.Expand(); Start-Sleep -Milliseconds 150 } catch { }
        }
        $kids = Get-TreeItemChildren $current
        $pick = Resolve-ChildIndex @($kids | ForEach-Object { [string]$_.Current.Name }) $seg
        if (-not $pick.ok) { return @{ ok = $false; code = $pick.code; message = "$($pick.message) (in '$TreeId', resolving '$Path')" } }
        $current = $kids[$pick.index]
    }
    return @{ ok = $true; code = 'Ok'; message = "Resolved '$Path' in $TreeId."; element = $current }
}

# Bring a row into the viewport without selecting it. An off-screen row has no usable on-screen point,
# so every coordinate gesture needs this; only SOME of them want the selection that used to come with it.
# Any element offering ScrollItemPattern, not only a tree item -- the Problemer table's rows go through it too.
function Show-ScrollableItem {
    param($El)
    $scroll = Get-Pattern $El ([System.Windows.Automation.ScrollItemPattern]::Pattern)
    if ($scroll) { try { $scroll.ScrollIntoView(); Start-Sleep -Milliseconds 150 } catch { } }
}

function Select-TreePath {
    param($Window, [string] $TreeId, [string] $Path)
    $r = Resolve-TreePath $Window $TreeId $Path
    if (-not $r.ok) { return $r }
    $current = $r.element
    $sip = Get-Pattern $current ([System.Windows.Automation.SelectionItemPattern]::Pattern)
    if (-not $sip) { return @{ ok = $false; code = 'NotAllowed'; message = "Target has no SelectionItem pattern." } }
    $sip.Select(); Start-Sleep -Milliseconds 150
    # Selecting does NOT scroll the row into view, and an off-screen row has no usable on-screen
    # point -- coordinate gestures (node.doubleClick) then hit whatever occupies that pixel, or
    # nothing. Bring it into view here, at the single call site every selection-relative command
    # already funnels through, so deep rows (e.g. a TV2 function block far down the pane) are
    # clickable at all.
    Show-ScrollableItem $current
    # verify caret landed
    if (-not $sip.Current.IsSelected) { return @{ ok = $false; code = 'TargetNotFound'; message = "Selection did not land on '$Path'." } }
    # Echo the RESOLVED LABEL, not the path we were handed. A command that repeats its own input can only
    # ever confirm what you asked for: to learn WHICH row it hit you had to read the selection back, and
    # that read lags by one selection — so every question about the driver's tree identity became a
    # settle-and-poll that still might report the previous answer. Several passes of a parity scenario
    # were lost to exactly that ambiguity. The resolved name is free here and makes the call
    # self-verifying.
    $resolved = $current.Current.Name
    return @{ ok = $true; code = 'Ok'; resolvedLabel = $resolved
              message = "Selected '$resolved' (path '$Path') in $TreeId."; element = $current }
}

function Invoke-Mechanism-TreeSelect {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $treeId = Resolve-TreeId $Opts
    $path = Get-PathOpt $Opts
    if (-not $path) { return (New-Result -Ok $false -Code 'InvalidInput' -Message 'tree.select requires a path argument.') }
    $r = Select-TreePath $Window $treeId $path
    return (New-Result -Ok $r.ok -Code $r.code -Message $r.message -Verified $r.ok -Context (Get-Context $Window))
}

function Invoke-Mechanism-ExpandCollapse {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $treeId = Resolve-TreeId $Opts
    $path = Get-PathOpt $Opts
    if (-not $path) { return (New-Result -Ok $false -Code 'InvalidInput' -Message "$($Spec.id) requires a --path.") }
    $sel = Select-TreePath $Window $treeId $path
    if (-not $sel.ok) { return (New-Result -Ok $false -Code $sel.code -Message $sel.message -Context (Get-Context $Window)) }
    $ecp = Get-Pattern $sel.element ([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    if (-not $ecp) { return (New-Result -Ok $false -Code 'NotAllowed' -Message 'Node does not support ExpandCollapse.' -Context (Get-Context $Window)) }
    if ($Spec.action -eq 'collapse') { $ecp.Collapse() } else { $ecp.Expand() }
    Start-Sleep -Milliseconds 200
    $state = "$($ecp.Current.ExpandCollapseState)"
    return (New-Result -Ok $true -Code 'Ok' -Message "$($Spec.action) '$path' -> $state." -Verified $true `
        -Context (Get-Context $Window) -Data ([ordered]@{ state = $state }))
}

# Hit-test guard. A point that resolves to a different row must FAIL rather than click: a wrong
# target that reports success is worse than a no-op, because it silently fabricates a result.
function Test-PointHitsTreeItem {
    param($Point, $El)
    try { $hit = $script:AE::FromPoint($Point) } catch { return $false }
    $cur = $hit
    while ($cur) {
        if ($cur.Current.ControlType.ProgrammaticName -eq 'ControlType.TreeItem') {
            return [System.Windows.Automation.Automation]::Compare($cur, $El)
        }
        $cur = $script:Walk.GetParent($cur)
    }
    return $false
}

# Find a point that PROVABLY hits this row, or nothing. Two live-verified traps make the obvious
# answers wrong:
#   1. An Avalonia TreeViewItem's BoundingRectangle encloses its ENTIRE expanded subtree, not just
#      its own row ('Entré/Gang' measured H=336 across five child rows), so GetClickablePoint() and
#      the rect centre land on a DESCENDANT -- a silent wrong-target click that reads as "the
#      gesture did nothing to this node" when the node was never clicked. The row's own header is
#      the strip from the top of its rect down to its first realized child.
#   2. A row at the edge of the viewport can be half-hidden behind the tree's own scrollbar (the
#      TV2 block 'Lamper v. hoveddør' measured Y=1283..1339 while the horizontal scrollbar began at
#      ~1319, so the header's centre hit-tested to the scrollbar's 'Page right' button).
# So: walk candidate points across the header strip and return the first that hit-tests back to the
# target. Left-edge candidates are deliberately avoided -- that is where the expander caret lives,
# and clicking it toggles expansion instead of activating the row.
# The row's OWN header strip height: from the top of its rect down to its first realized child row.
# Factored out because trap #1 above applies to every click on this control, not just the centred one --
# a second copy would drift out of it one fix at a time. Returns 0 when there is no usable strip.
function Get-TreeItemHeaderHeight {
    param($El, $Rect)
    $headerBottom = $Rect.Y + $Rect.Height
    $c = $script:Walk.GetFirstChild($El)
    while ($c) {
        if ($c.Current.ControlType.ProgrammaticName -eq 'ControlType.TreeItem') {
            $cr = $c.Current.BoundingRectangle
            if ($cr.Height -ge 1 -and $cr.Y -gt $Rect.Y -and $cr.Y -lt $headerBottom) { $headerBottom = $cr.Y }
        }
        $c = $script:Walk.GetNextSibling($c)
    }
    $h = $headerBottom - $Rect.Y
    return $(if ($h -lt 2) { 0 } else { $h })
}

function Get-TreeItemClickPoint {
    param($El)
    $r = $El.Current.BoundingRectangle
    if ($r.Width -lt 1 -or $r.Height -lt 1) { return $null }
    $h = Get-TreeItemHeaderHeight $El $r
    if ($h -lt 2) { return $null }
    # Keep X inside the owning tree's viewport: the row's layout rect can be wider than the pane
    # (horizontally scrollable content), so its own centre may fall outside the visible control.
    $cx = $r.X + $r.Width / 2
    $tree = $script:Walk.GetParent($El)
    while ($tree -and $tree.Current.ControlType.ProgrammaticName -ne 'ControlType.Tree') { $tree = $script:Walk.GetParent($tree) }
    if ($tree) {
        $tr = $tree.Current.BoundingRectangle
        $lo = $tr.X + 24; $hi = $tr.X + $tr.Width - 28
        if ($hi -gt $lo) { $cx = [Math]::Min([Math]::Max($cx, $lo), $hi) }
    }
    foreach ($fy in @(0.5, 0.25, 0.15, 0.75)) {
        $p = New-Object System.Windows.Point([double]$cx, [double]($r.Y + $h * $fy))
        if (Test-PointHitsTreeItem $p $El) { return $p }
    }
    return $null
}

# S5 / census C16: a point XOffset px RIGHT OF THE ROW'S OWN LABEL, still on the row -- the OpenVisual
# counterpart of the vendor's label-rect + offset. C16 asks what a double-click out in that empty space
# does: activate the row, toggle it, or nothing.
#
# The label rect is not handed to us the way Win32's TVM_GETITEMRECT(wParam=TRUE) hands it to the vendor
# driver, so it is read from the row's own Text descendant -- restricted to the HEADER strip, because a
# TreeViewItem's subtree contains every descendant row's TextBlock too and the widest of those belongs to
# somebody else.
#
# The hit-test-back guard is NOT optional here (see Test-PointHitsTreeItem and trap #1 above): without it
# an offset that overshoots silently clicks a DIFFERENT row -- or the pane background -- and reports
# success, which is precisely the fabricated hit area this measurement exists to avoid. Returns $null and
# the caller refuses.
function Get-TreeItemOffsetClickPoint {
    param($El, [int] $XOffset)
    $r = $El.Current.BoundingRectangle
    if ($r.Width -lt 1 -or $r.Height -lt 1) { return $null }
    $h = Get-TreeItemHeaderHeight $El $r
    if ($h -lt 2) { return $null }
    $headerBottom = $r.Y + $h

    # The row's own label: Text descendants that start inside the header strip.
    $labelRight = $null
    foreach ($t in $El.FindAll($script:Desc,
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Text)))) {
        $tr = $t.Current.BoundingRectangle
        if ([double]::IsInfinity($tr.X) -or $tr.Width -lt 1) { continue }
        if ($tr.Y -lt $r.Y -or $tr.Y -ge $headerBottom) { continue }   # a descendant row's label, not ours
        $right = $tr.X + $tr.Width
        if ($null -eq $labelRight -or $right -gt $labelRight) { $labelRight = $right }
    }
    if ($null -eq $labelRight) { return $null }

    $cx = $labelRight + $XOffset
    # Stay inside the owning tree's viewport, minus the scrollbar gutter (trap #2).
    $tree = $script:Walk.GetParent($El)
    while ($tree -and $tree.Current.ControlType.ProgrammaticName -ne 'ControlType.Tree') { $tree = $script:Walk.GetParent($tree) }
    if ($tree) {
        $tr2 = $tree.Current.BoundingRectangle
        if ($cx -ge ($tr2.X + $tr2.Width - 28)) { return $null }   # past the viewport: refuse, never clamp
    }
    foreach ($fy in @(0.5, 0.25, 0.75)) {
        $p = New-Object System.Windows.Point([double]$cx, [double]($r.Y + $h * $fy))
        if (Test-PointHitsTreeItem $p $El) { return $p }
    }
    return $null
}

function Invoke-Mechanism-DoubleClick {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $treeId = Resolve-TreeId $Opts
    $path = Get-PathOpt $Opts
    if (-not $path) { return (New-Result -Ok $false -Code 'InvalidInput' -Message 'node.doubleClick requires a --path.') }
    $sel = Select-TreePath $Window $treeId $path
    if (-not $sel.ok) { return (New-Result -Ok $false -Code $sel.code -Message $sel.message -Context (Get-Context $Window)) }
    $guard = Assert-Foreground $Window
    if ($guard) { return $guard }

    # --x-offset (census C16): click in the row's empty space RIGHT of its label instead of on the label.
    # NamedOnly (via Get-OptInt): the positional here is the PATH, and reading it as an offset used to
    # throw a cast error that surfaced as MutationFailed on the documented `node double-click <path>` form.
    $xoOpt = Get-OptInt $Opts @('x-offset') 0
    if (-not $xoOpt.ok) { return (New-Result -Ok $false -Code 'InvalidInput' -Message $xoOpt.message -Context (Get-Context $Window)) }
    $xo = $xoOpt.value
    $pt = if ($xo -gt 0) { Get-TreeItemOffsetClickPoint $sel.element $xo } else { Get-TreeItemClickPoint $sel.element }
    if (-not $pt) {
        $why = if ($xo -gt 0) {
            "No point $xo px right of '$path'`s label hit-tests back to it -- the offset leaves the row, the viewport, or lands on another row. Refusing rather than clamping: a clamped click reports success from a hit area nobody asked for."
        } else {
            "No point on '$path' hit-tests back to it (row occluded, clipped by a scrollbar, or scrolled out of view); refusing to click another row."
        }
        return (New-Result -Ok $false -Code 'TargetNotFound' -Message $why -Context (Get-Context $Window))
    }
    # Two clicks within the double-click time = a double-click / node activation.
    [Aui.Win32]::Click([int]$pt.X, [int]$pt.Y)
    Start-Sleep -Milliseconds 60
    [Aui.Win32]::Click([int]$pt.X, [int]$pt.Y)
    Start-Sleep -Milliseconds 250
    return (New-Result -Ok $true -Code 'Ok' -Message "Double-clicked '$path'$(if ($xo -gt 0) { " at +$xo px right of its label" })." -Verified $false `
        -Context (Get-Context $Window) `
        -Data ([ordered]@{ point = (New-DeclaredPoint -X ([int]$pt.X) -Y ([int]$pt.Y) `
                                      -Geometry (Get-MonitorGeometry -X ([int]$pt.X) -Y ([int]$pt.Y)))
                           targetVerified = $true
                           xOffset = $xo; hitArea = $(if ($xo -gt 0) { 'rightOfLabel' } else { 'labelCentre' }) }))
}

# ── Right-click semantics (C2 / dimension D3) ────────────────────────────────────────────────
# The census could not ask this: the only right-click in the driver was Shift+F10 inside the
# context-menu route, which (a) is a KEYBOARD gesture that acts on the focused row and so cannot move
# the caret by construction, and (b) always dumped the menu, so the selection question was never
# isolated. C2 asks: does right-clicking node B, while node A is selected, MOVE the selection to B?
# That underpins every context-menu cell -- if right-click does not select, the menu acts on a
# different node than the user is pointing at.
#
# The cell is designed so a no-op is DISTINGUISHABLE (the trap that makes vendor node.click 'partial'):
# the target is resolved but deliberately NOT selected, so selectedBefore != the target. If
# selectedAfter is the target, right-click selects; if it is unchanged, it does not. Either way the
# answer is readable from the envelope alone.
# Needed because the 3WAY scenarios have to prove that using a supplement (edit.moveUp/moveDown,
# link start-from-here/to-here) produces the same result as the drag it replaces. Comparing the supplement
# against the vendor's drag alone would leave the OpenVisual drag path itself unmeasured.
#
# Synthesized as press → threshold nudge → interpolated moves → release, rather than one jump to the target:
# a drag only STARTS after the pointer travels past the toolkit's drag threshold while held, and a drop only
# registers where the pointer last moved, so a single SetCursorPos between down and up produces a click, not
# a drag. Same-pane drags verify row-order changes. Cross-pane program drops arm a chooser and name both
# endpoints; link drops instead increase both endpoint child counts and report only generic "Linket."
function Invoke-Mechanism-NodeDrag {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    # Endpoint-specific selectors make the gesture itself addressable across panes. Legacy --tree remains the
    # fallback for both endpoints, so existing same-pane reorder/link calls keep their original grammar.
    $fromTreeId = Resolve-TreeId $Opts @('from-tree')
    $toTreeId = Resolve-TreeId $Opts @('to-tree')
    # Two DISTINCT positionals: `node drag A B`. Reading both from index 0 (the old shared fallback)
    # made --to equal --from, i.e. a drag of a row onto ITSELF reported as a real drag.
    $from = Get-OptValue $Opts @('from', 'path') -PositionalIndex 0
    $to = Get-OptValue $Opts @('to', 'target') -PositionalIndex 1
    if (-not $from -or $from -is [bool] -or -not $to -or $to -is [bool]) {
        return (New-Result -Ok $false -Code 'InvalidInput' -Message "$($Spec.id) requires --from <path> and --to <path>." -Context (Get-Context $Window))
    }

    $src = Resolve-TreePath $Window $fromTreeId $from
    if (-not $src.ok) { return (New-Result -Ok $false -Code $src.code -Message $src.message -Context (Get-Context $Window)) }
    Show-ScrollableItem $src.element
    $dst = Resolve-TreePath $Window $toTreeId $to
    if (-not $dst.ok) { return (New-Result -Ok $false -Code $dst.code -Message $dst.message -Context (Get-Context $Window)) }
    Show-ScrollableItem $dst.element

    $crossPane = $fromTreeId -ne $toTreeId
    $sourceChildrenBefore = @()
    $targetChildrenBefore = @()
    if ($crossPane) {
        $sourceChildrenBefore = @(Get-ExpandedTreeItemChildNames $src.element)
        $targetChildrenBefore = @(Get-ExpandedTreeItemChildNames $dst.element)
    }

    $guard = Assert-Foreground $Window
    if ($guard) { return $guard }

    # Re-resolve both points AFTER any scrolling Show-ScrollableItem did, or the coordinates are stale.
    $a = Get-TreeItemClickPoint (Resolve-TreePath $Window $fromTreeId $from).element
    $b = Get-TreeItemClickPoint (Resolve-TreePath $Window $toTreeId $to).element
    if (-not $a -or -not $b) {
        return (New-Result -Ok $false -Code 'TargetNotFound' `
            -Message 'One of the two rows does not hit-test back to itself (occluded or scrolled out of view); refusing to drag blind.' `
            -Context (Get-Context $Window))
    }

    $sourceOrderBefore = (Get-PaneRowOrder $Window $fromTreeId) -join '|'
    $targetOrderBefore = if ($crossPane) { (Get-PaneRowOrder $Window $toTreeId) -join '|' } else { $sourceOrderBefore }
    $before = Get-Context $Window
    $sourceName = [string]$src.element.Current.Name
    $targetName = [string]$dst.element.Current.Name
    [Aui.Win32]::Drag([int]$a.X, [int]$a.Y, [int]$b.X, [int]$b.Y)
    Start-Sleep -Milliseconds 350

    # Realized-row churn from scrolling is not a cross-pane effect. Accept cross-pane success only when
    # changed status names both endpoints or both endpoint child counts increase.
    $after = Get-Context $Window
    $sourceOrderAfter = (Get-PaneRowOrder $Window $fromTreeId) -join '|'
    $targetOrderAfter = if ($crossPane) { (Get-PaneRowOrder $Window $toTreeId) -join '|' } else { $sourceOrderAfter }
    $structureChanged = $sourceOrderBefore -ne $sourceOrderAfter -or $targetOrderBefore -ne $targetOrderAfter
    # Target selection can predate the drag, so keep this diagnostic out of the success oracle.
    $targetSelected = $false
    $statusChanged = $before.statusText -ne $after.statusText
    $statusNamesEndpoints = $statusChanged -and $after.statusText -and
        $after.statusText.IndexOf($sourceName, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -and
        $after.statusText.IndexOf($targetName, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    $sourceChildrenAfter = @()
    $targetChildrenAfter = @()
    if ($crossPane) {
        $sourceAfterResolution = Resolve-TreePath $Window $fromTreeId $from
        $targetAfterResolution = Resolve-TreePath $Window $toTreeId $to
        if ($sourceAfterResolution.ok) { $sourceChildrenAfter = @(Get-ExpandedTreeItemChildNames $sourceAfterResolution.element) }
        if ($targetAfterResolution.ok) { $targetChildrenAfter = @(Get-ExpandedTreeItemChildNames $targetAfterResolution.element) }
    }
    $endpointChildCountsIncreased = $sourceChildrenAfter.Count -gt $sourceChildrenBefore.Count -and
        $targetChildrenAfter.Count -gt $targetChildrenBefore.Count
    $effectObserved = if ($crossPane) { $statusNamesEndpoints -or $endpointChildCountsIncreased } else { $structureChanged }
    for ($i = 0; $i -lt 6; $i++) {
        $targetSelected = @($after.selections | Where-Object {
            $_ -and $_.tree -eq $toTreeId -and $_.name -eq $targetName }).Count -gt 0
        if ($effectObserved) { break }
        Start-Sleep -Milliseconds 250
        $after = Get-Context $Window
        $sourceOrderAfter = (Get-PaneRowOrder $Window $fromTreeId) -join '|'
        $targetOrderAfter = if ($crossPane) { (Get-PaneRowOrder $Window $toTreeId) -join '|' } else { $sourceOrderAfter }
        $structureChanged = $sourceOrderBefore -ne $sourceOrderAfter -or $targetOrderBefore -ne $targetOrderAfter
        $statusChanged = $before.statusText -ne $after.statusText
        $statusNamesEndpoints = $statusChanged -and $after.statusText -and
            $after.statusText.IndexOf($sourceName, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -and
            $after.statusText.IndexOf($targetName, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
        if ($crossPane) {
            $sourceAfterResolution = Resolve-TreePath $Window $fromTreeId $from
            $targetAfterResolution = Resolve-TreePath $Window $toTreeId $to
            if ($sourceAfterResolution.ok) { $sourceChildrenAfter = @(Get-ExpandedTreeItemChildNames $sourceAfterResolution.element) }
            if ($targetAfterResolution.ok) { $targetChildrenAfter = @(Get-ExpandedTreeItemChildNames $targetAfterResolution.element) }
        }
        $endpointChildCountsIncreased = $sourceChildrenAfter.Count -gt $sourceChildrenBefore.Count -and
            $targetChildrenAfter.Count -gt $targetChildrenBefore.Count
        $effectObserved = if ($crossPane) { $statusNamesEndpoints -or $endpointChildCountsIncreased } else { $structureChanged }
    }

    $moved = (-not $crossPane) -and $structureChanged
    $data = [ordered]@{
        from = [string]$from; to = [string]$to
        fromTree = $fromTreeId; toTree = $toTreeId; crossPane = $crossPane
        moved = $moved; effectObserved = $effectObserved
        targetSelected = $targetSelected; statusChanged = $statusChanged
        statusNamesEndpoints = $statusNamesEndpoints; endpointChildCountsIncreased = $endpointChildCountsIncreased
        sourceChildCountBefore = $sourceChildrenBefore.Count; sourceChildCountAfter = $sourceChildrenAfter.Count
        targetChildCountBefore = $targetChildrenBefore.Count; targetChildCountAfter = $targetChildrenAfter.Count
    }
    $route = if ($crossPane) { "$fromTreeId -> $toTreeId" } else { $fromTreeId }
    return (New-Result -Ok $effectObserved -Code $(if ($effectObserved) { 'Ok' } else { 'NoEffect' }) `
        -Message $(if ($effectObserved) { "Dragged '$from' onto '$to' ($route)." } else { "Dragged '$from' onto '$to' ($route), but no target-side effect was observed." }) `
        -Verified $effectObserved -Context $after -Data $data)
}

# The pane's visible rows in order — the cheapest signal that a drag actually moved something.
function Get-PaneRowOrder {
    param($Window, [string] $TreeId)
    $tree = Find-ByAutomationId $Window $TreeId
    if (-not $tree) { return @() }
    $names = @()
    foreach ($i in $tree.FindAll($script:Desc,
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::TreeItem)))) {
        $names += $i.Current.Name
    }
    return $names
}

function Invoke-Mechanism-RightClick {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $treeId = Resolve-TreeId $Opts
    $path = Get-PathOpt $Opts
    if (-not $path) { return (New-Result -Ok $false -Code 'InvalidInput' -Message "$($Spec.id) requires a --path (the node to right-click)." -Context (Get-Context $Window)) }

    # Resolve WITHOUT selecting -- the whole point of the measurement.
    $r = Resolve-TreePath $Window $treeId $path
    if (-not $r.ok) { return (New-Result -Ok $false -Code $r.code -Message $r.message -Context (Get-Context $Window)) }
    Show-ScrollableItem $r.element

    $guard = Assert-Foreground $Window
    if ($guard) { return $guard }

    # Give the PANE real keyboard focus before the gesture, by left-clicking the row that is ALREADY
    # selected. This is the vendor driver's S1' step, and OpenVisual turns out to need it too: measured on
    # a freshly launched app, the first right-click reported selectionMoved:false / no flyout, while the
    # identical call after some real input worked every time. A UIA `tree select` sets the selection
    # without giving the control focus, so the pane can be "selected but not focused" and swallow the
    # gesture. Clicking the ALREADY-SELECTED row cannot corrupt the measurement — it is not the target,
    # and the selection it sets is the one already there.
    $treeEl = Find-ByAutomationId $Window $treeId
    if ($treeEl) {
        $selCond = New-PropCondition ([System.Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty) $true
        foreach ($cand in $treeEl.FindAll($script:Desc, $selCond)) {
            $sp = $cand.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
            if (-not $sp.Current.IsSelected) { continue }
            $fp = Get-TreeItemClickPoint $cand
            if ($fp) { [Aui.Win32]::Click([int]$fp.X, [int]$fp.Y); Start-Sleep -Milliseconds 200 }
            break
        }
    }

    $before = Get-Context $Window
    $pt = Get-TreeItemClickPoint $r.element
    if (-not $pt) {
        return (New-Result -Ok $false -Code 'TargetNotFound' `
            -Message "No point on '$path' hit-tests back to it (row occluded, clipped by a scrollbar, or scrolled out of view); refusing to click another row." `
            -Context (Get-Context $Window))
    }
    [Aui.Win32]::RightClick([int]$pt.X, [int]$pt.Y)
    Start-Sleep -Milliseconds 400

    # Whatever flyout the click raised is itself data -- report it, then close it, so the command
    # leaves no menu up to block the next one.
    $proc = Get-AppProcess
    $items = @(Get-MenuPopupItems $proc.Id)
    $rows = foreach ($i in $items) { [ordered]@{ label = $i.Current.Name; enabled = [bool]$i.Current.IsEnabled } }
    $flyoutOpened = @($rows).Count -gt 0
    Close-AllMenus $Window

    # Read the selection of the TARGET pane, and POLL for it: `$ctx.selection` is "the first pane that
    # has one", so with both panes selected it can answer about the other tree entirely, and a single
    # read 400 ms after the click can land before Avalonia has published the new selection to UIA.
    # Both failure modes look identical — selectionMoved:false — and both are wrong answers rather than
    # missing ones: a screenshot taken straight after showed the target row plainly selected while the
    # envelope said the caret had not moved.
    $selOf = {
        param($ctx)
        $m = @($ctx.selections) | Where-Object { $_ -and $_.tree -eq $treeId } | Select-Object -First 1
        if ($m) { "$($m.tree)/$($m.name)" } else { '' }
    }
    $beforeSel = & $selOf $before
    $after = Get-Context $Window
    $afterSel = & $selOf $after
    for ($i = 0; $i -lt 6 -and $afterSel -eq $beforeSel; $i++) {
        Start-Sleep -Milliseconds 250
        $after = Get-Context $Window
        $afterSel = & $selOf $after
    }
    $sameSel = ($beforeSel -eq $afterSel)
    $data = [ordered]@{
        target = "$treeId/$path"
        # The C2 answer, straight off the envelope.
        selectedBefore = (@($before.selections) | Where-Object { $_ -and $_.tree -eq $treeId } | Select-Object -First 1)
        selectedAfter  = (@($after.selections)  | Where-Object { $_ -and $_.tree -eq $treeId } | Select-Object -First 1)
        selectionMoved = (-not $sameSel)
        flyoutOpened   = $flyoutOpened
        itemCount      = @($rows).Count
        items          = @($rows)
        point          = (New-DeclaredPoint -X ([int]$pt.X) -Y ([int]$pt.Y) `
                            -Geometry (Get-MonitorGeometry -X ([int]$pt.X) -Y ([int]$pt.Y)))
        targetVerified = $true
    }
    return (New-Result -Ok $true -Code 'Ok' `
        -Message "Right-clicked '$path'. Selection moved: $(-not $sameSel). Flyout opened: $flyoutOpened ($(@($rows).Count) items)." `
        -Verified $true -Context $after -Data $data)
}

function Invoke-Mechanism-ReadProperty {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $treeId = Resolve-TreeId $Opts
    $path = Get-PathOpt $Opts
    if (-not $path) { return (New-Result -Ok $false -Code 'InvalidInput' -Message "$($Spec.id) requires a --path.") }
    # RESOLVE, never Select: reading a property is a question, and a question that moves the caret
    # changes the state every later measurement is taken against (see Invoke-Mechanism-TreeDump).
    # A UIA property read does not need the row selected, focused, or even scrolled into view.
    $sel = Resolve-TreePath $Window $treeId $path
    if (-not $sel.ok) { return (New-Result -Ok $false -Code $sel.code -Message $sel.message -Context (Get-Context $Window)) }
    $prop = switch ($Spec.property) {
        'helpText' { [System.Windows.Automation.AutomationElement]::HelpTextProperty }
        'name'     { [System.Windows.Automation.AutomationElement]::NameProperty }
        default    { [System.Windows.Automation.AutomationElement]::HelpTextProperty }
    }
    $val = "$($sel.element.GetCurrentPropertyValue($prop))"
    return (New-Result -Ok $true -Code 'Ok' -Message "$($Spec.property) of '$path'." -Verified $true `
        -Context (Get-Context $Window) -Data ([ordered]@{ property = $Spec.property; value = $val }))
}

# ── Context flyout: ONE opener, shared by the dump and the act routes ────────────────────────
#
# Both routes were wrong, in two different ways, and both are fixed here so the two can never again
# disagree about whether the flyout opened (the dump read "0 items" for the same flyout the act route
# reported ok:true on).
#
# (1) SCOPE -- THIS is the defect that reported success while nothing happened, and it is proven.
#     The act route searched `$script:AE::RootElement` -- the DESKTOP -- for a MenuItem by name.
#     NodeContextMenu is a MenuFlyout declared once as an x:Key RESOURCE and shared by BOTH trees
#     (MainWindow.axaml:55, attached at :336/:379), and its items carry no AutomationId, so a desktop-
#     wide name search can match a realized-but-not-open item and Invoke() it to no effect -- ok:true
#     while the node is untouched, which is exactly what cost S03/S04 real Phase-2 turns. Only items
#     realized in the APP's process, with the menu actually up, are admissible. Everything below is
#     scoped to Get-MenuPopupItems, and a zero-item flyout is now a loud failure rather than an empty
#     inventory dressed up as a verified reading.
#
# (2) ORDER -- foreground FIRST, then select+focus, then send. ⚠ MEASURED, AND IT IS *NOT* WHAT FIXED
#     THE FLAKE. The ihcvisual MCP had a structurally identical defect (menu.dumpContext, F-022) that
#     looked intermittent and was deterministic -- cold 0/10, warm 10/10 -- because winning the
#     foreground is an ACTIVATION, which MFC answers by restoring focus to its own last-active view,
#     discarding the focus just set. This route had the same SHAPE (Select-TreePath -> Assert-Foreground
#     -> SendKeys), so the same fix was applied here. An A/B control then measured it (2026-07-16,
#     non-elevated app, File>New template, locality 0/0, cold foreground forced per run):
#
#         legacy order (select->activate->send): 3/3 Ok cold
#         fixed  order (activate->select->send): 3/3 Ok cold
#         fixed order, 6 cold + 6 warm:          12/12 Ok, every one on attempts=1
#
#     So Avalonia does NOT reproduce MFC's focus-restore-on-activation, the ordering was never this
#     route's bug, and the flake did not reproduce here AT ALL (18/18). The order is kept because it is
#     strictly more correct and costs nothing -- but do NOT record it as the fix, and do not treat these
#     numbers as proof the Phase-2 flake is gone: that was seen on a 2.9 MB real project from an
#     ELEVATED session, and neither condition is reproduced above. If it returns, the untested variables
#     are project size (tree realization cost) and elevation -- not the ordering, which is now ruled out.
#
# Returns @{ ok; code; message; items = @(); attempts }. The caller closes the menu.
function Open-ContextFlyout {
    param($Window, [string] $TreeId, [string] $Path)

    # 1. Foreground first (see (2) above: defensive, not the fix -- measured as making no difference here).
    $guard = Assert-Foreground $Window
    if ($guard) { return @{ ok = $false; code = 'PreconditionMissing'; message = $guard.message; items = @(); attempts = 0 } }

    # 2. THEN select + focus, so no activation can follow to undo it.
    $target = $null
    if ($Path) {
        $sel = Select-TreePath $Window $TreeId $Path
        if (-not $sel.ok) { return @{ ok = $false; code = $sel.code; message = $sel.message; items = @(); attempts = 0 } }
        $target = "$TreeId/$Path"
        # Select() sets SELECTION; Shift+F10 follows KEYBOARD FOCUS, and the two are not the same
        # thing. Setting focus explicitly is the analogue of the MCP's FocusCaretDeep.
        try { $sel.element.SetFocus() ; Start-Sleep -Milliseconds 80 } catch { }
    }

    $proc = Get-AppProcess
    if (-not $proc) { return @{ ok = $false; code = 'AppNotRunning'; message = 'App not running.'; items = @(); attempts = 0 } }

    # 3. Send, then POLL for realized items rather than sleeping once and looking: the flyout is raised
    #    asynchronously, so a fixed wait can miss a slow open and report a false "did not open".
    $items = @()
    $attempts = 0
    while ($attempts -lt 2 -and @($items).Count -eq 0) {
        $attempts++
        [System.Windows.Forms.SendKeys]::SendWait('+{F10}')   # Shift+F10 = context menu
        $deadline = (Get-Date).AddMilliseconds(2500)
        while ((Get-Date) -lt $deadline) {
            $items = @(Get-MenuPopupItems $proc.Id)
            if (@($items).Count -gt 0) { break }
            Start-Sleep -Milliseconds 50
        }
        if (@($items).Count -eq 0 -and $attempts -lt 2) {
            # Re-assert the whole gesture from the top, ordering included.
            $null = Assert-Foreground $Window
            if ($Path) {
                $re = Select-TreePath $Window $TreeId $Path
                if ($re.ok) { try { $re.element.SetFocus() ; Start-Sleep -Milliseconds 80 } catch { } }
            }
        }
    }

    # 4. Fail LOUDLY when it did not open. Every node in these two trees carries the SAME flyout (one
    #    shared resource), so "zero realized items" cannot mean "this node type has no context menu" --
    #    it means the gesture did not land. That makes this an unambiguous driver failure here, unlike
    #    the vendor's equivalent, where a TargetNotFound is only EVIDENCE of absence.
    if (@($items).Count -eq 0) {
        return @{ ok = $false; code = 'TargetNotFound'; attempts = $attempts; items = @()
            message = "The context flyout did not open for '$target' after $attempts attempts. Both trees share one MenuFlyout resource, so this is the gesture failing, NOT a node without a menu -- do not record it as one." }
    }
    return @{ ok = $true; code = 'Ok'; message = "Flyout open on '$target': $(@($items).Count) items."
        items = @($items); attempts = $attempts; target = $target }
}

function Invoke-Mechanism-ContextMenu {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $treeId = Resolve-TreeId $Opts
    $path = Get-PathOpt $Opts

    $flyout = Open-ContextFlyout $Window $treeId $path
    if (-not $flyout.ok) {
        return (New-Result -Ok $false -Code $flyout.code -Message $flyout.message -Context (Get-Context $Window))
    }

    # The registry row usually PINS the item (fb.unlock is always 'Unlock'). A row that does not pin
    # one takes --item instead, so the authoring commands a census must exercise do not each need
    # their own row. A '/' walks ONE submenu level ('Insert variable/Input'), because several
    # OpenVisual flyouts nest their palette rather than listing it flat.
    # StrictMode: a row that pins no item has no 'item' property at all, so probe before reading it.
    $wanted = $null
    if ($Spec.PSObject.Properties.Name -contains 'item') { $wanted = $Spec.item }
    $itemOpt = Get-OptValue $Opts @('item')
    if ($itemOpt -and $itemOpt -isnot [bool]) { $wanted = [string]$itemOpt }
    if (-not $wanted) {
        Close-AllMenus $Window
        return (New-Result -Ok $false -Code 'InvalidInput' `
            -Message "This command pins no context item, so --item <label> is required (use 'Parent/Leaf' for a submenu)." `
            -Context (Get-Context $Window))
    }
    $segments = @([string]$wanted -split '(?<!\\)/' | ForEach-Object { $_ -replace '\\/', '/' })

    # Match ONLY among the items this flyout actually realized. The items are Header text with the
    # access-key underscore stripped by the framework ('_Unlock' surfaces as 'Unlock').
    $level = @($flyout.items)
    $item = $null
    $proc = Get-AppProcess
    for ($s = 0; $s -lt $segments.Count; $s++) {
        $seg = $segments[$s]
        $hit = $null
        # AutomationId first, then label -- the same rule the menu-bar walk uses. The flyout's ids are
        # the app's CommandRegistry row ids under a "ctx." prefix (ctx.edit.cut, ctx.node.properties),
        # so `--item ctx.edit.delete` survives a rewording that `--item Slet` does not.
        foreach ($i in @($level)) {
            if (Test-MenuSegmentMatch $i $seg) { $hit = $i; break }
        }
        if (-not $hit) {
            $avail = (@($level | ForEach-Object {
                $n = $_.Current.Name; $a = $_.Current.AutomationId
                if ($a) { "$n [$a]" } else { $n } }) -join ', ')
            Close-AllMenus $Window
            # The flyout DID open and this item is not in it -- a real, recordable fact about the app,
            # and a different outcome from "the gesture failed" above. Conflating the two is what made a
            # driver failure look like a census cell.
            return (New-Result -Ok $false -Code 'TargetNotFound' `
                -Message "The flyout opened but has no item '$seg' (it is not applicable to this selection). Realized: [$avail]" `
                -Verified $true -Context (Get-Context $Window))
        }
        if (-not $hit.Current.IsEnabled) {
            Close-AllMenus $Window
            return (New-Result -Ok $false -Code 'PreconditionMissing' `
                -Message "Context item '$seg' is present but DISABLED, so invoking it cannot do anything. That is an observation -- record it rather than retrying." `
                -Verified $true -Context (Get-Context $Window))
        }
        if ($s -lt $segments.Count - 1) {
            # Not the leaf: open this item's submenu by HOVER (never a click, which would invoke it).
            $before = Get-PopupKeySet $proc.Id
            $null = Invoke-ElementHover $hit
            Start-Sleep -Milliseconds 450
            $new = @()
            foreach ($i in @(Get-MenuPopupItems $proc.Id)) {
                $k = Get-ElementKey $i
                if ($k -and -not $before.ContainsKey($k)) { $new += $i }
            }
            if (@($new).Count -eq 0) {
                Close-AllMenus $Window
                return (New-Result -Ok $false -Code 'TargetNotFound' `
                    -Message "Context item '$seg' realized no submenu, so '$($segments[$s+1])' cannot be reached under it." `
                    -Verified $true -Context (Get-Context $Window))
            }
            $level = $new
        } else {
            $item = $hit
        }
    }

    # This row reaches any flyout item, so it must honour the gates the pinned rows carry by name --
    # node.delete is confirmDestructive whether it is reached as `node delete` or as
    # `menu invoke-context --item ctx.edit.delete`. See Get-MenuTargetGate.
    $gated = Test-MenuTargetGate $item $Opts $Window
    if ($gated) { return $gated }

    $inv = Get-Pattern $item ([System.Windows.Automation.InvokePattern]::Pattern)
    if ($inv) { $inv.Invoke() } else { $null = Invoke-ElementClick $item }

    Start-Sleep -Milliseconds 300

    # An invoke that landed dismisses the menu. Items still realized means the click did not take --
    # report that rather than claiming success.
    #
    # That premise holds only for a TOP-LEVEL item. Invoking a SUBMENU leaf leaves the parent popup
    # realized, so the same check called a mutation that had plainly succeeded ("Condition added." in
    # the status bar, the row present in the tree) a MutationFailed -- a driver false NEGATIVE, the
    # same class of bug as an unverified success, just inverted. For a nested item the menu state is
    # simply not an oracle: close the menus and say so, leaving the effect to the caller (which is
    # what MR05 asks for anyway).
    $nested = $segments.Count -gt 1
    $stillOpen = @(Get-MenuPopupItems (Get-AppProcess).Id).Count -gt 0
    if ($stillOpen -and $nested) {
        Close-AllMenus $Window
        $warn = @("Invoked the submenu leaf '$($segments[-1])'; its parent flyout was still realized, which for a NESTED item says nothing either way -- this command reports delivery only. Read the effect back.")
        if ($flyout.attempts -gt 1) { $warn += "The flyout only opened on attempt $($flyout.attempts)." }
        return (New-Result -Ok $true -Code 'Ok' -Message "Invoked context item '$wanted'." -Verified $false `
            -Warnings $warn -Context (Get-Context $Window) `
            -Data ([ordered]@{ item = $wanted; attempts = $flyout.attempts; nested = $true }))
    }
    if ($stillOpen) {
        Close-AllMenus $Window
        return (New-Result -Ok $false -Code 'MutationFailed' `
            -Message "Invoked '$wanted' but the flyout stayed open, so the item did not activate. Nothing was verified -- do not record an effect from this run." `
            -Verified $true -Context (Get-Context $Window))
    }
    $warn = @()
    if ($flyout.attempts -gt 1) {
        $warn += "The flyout only opened on attempt $($flyout.attempts). The first gesture was lost; a RECURRING retry here means the foreground/focus order regressed -- investigate rather than accepting it."
    }
    # Verified:false stands: the menu closing proves the item ACTIVATED, not that the command had its
    # intended effect. statusText (which names the node acted on) is the oracle for that.
    return (New-Result -Ok $true -Code 'Ok' -Message "Invoked context item '$wanted'." -Verified $false `
        -Warnings $warn -Context (Get-Context $Window) `
        -Data ([ordered]@{ item = $wanted; attempts = $flyout.attempts }))
}

# ── Menu walking ─────────────────────────────────────────────────────────────
# AccessibleMenu exposes pattern-first bar traversal; click and hover remain fallbacks for older
# controls and popup census walks whose submenu realization is observable only through hover.

function Get-MenuPopupItems {
    param([int] $AppPid)
    # Realized popup items are the process's MenuItems/Separators minus live menu-bar RuntimeIds;
    # AutomationId cannot distinguish roots because named popup items carry one too.
    #
    # This walk RACES THE THING IT IS LOOKING FOR: the flyout is still realizing while UIA walks, and a
    # Descendants walk whose tree mutates mid-walk throws E_FAIL from the COM provider. It surfaced as an
    # unhandled MutationFailed on ~1-in-20 COLD runs against the 2.9 MB real project and never on the
    # File>New template (which realizes too fast to race) -- which is exactly why enabler2 could not
    # reproduce it at 18/18. Measured verify2 2026-07-16: cold 19/20 vs warm 20/20.
    #
    # E_FAIL here means "the tree moved under the walk", NOT "there are no items", so retrying the READ
    # is the correct handling and reporting a failure is a FALSE NEGATIVE. This is a retry of a read that
    # threw -- not the disproved "retry the gesture" hypothesis: the gesture fired and the flyout opened.
    # Separators count as popup content: the vendor's dump reports them as empty-label rows, and a menu
    # inventory that silently drops them cannot compare grouping (uxparity S-27).
    $kindCond = New-Object System.Windows.Automation.OrCondition(
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::MenuItem)),
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Separator)))
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ProcessIdProperty) $AppPid),
        $kindCond)
    $all = $null
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        try { $all = $script:AE::RootElement.FindAll($script:Desc, $cond); break }
        catch {
            # Only a transient provider fault is retryable; anything else is a real error, so rethrow it
            # rather than silently turning it into "no menu items".
            if ($attempt -eq 4 -or "$($_.Exception.Message)" -notmatch 'E_FAIL|COM component') { throw }
            Start-Sleep -Milliseconds 120
        }
    }
    # Exclude the menu-bar ROOTS by identity, not by "carries an AutomationId".
    #
    # That heuristic held only while every named menu item happened to be a bar root. Avalonia falls back
    # to a control's x:Name for its AutomationId, so ANY item the XAML names looks like a root to it and is
    # dropped from the walk -- silently, with no error and no gap in the output. Live-caught 2026-08-01
    # (uxparity S-06): File > "Recent projects" is `Name="RecentProjectsMenu"` in MainWindow.axaml, so
    # `menu dump-bar` reported a six-item File menu that visibly has seven, and the recent-projects list
    # looked ABSENT from the application rather than absent from the dump. A menu inventory that omits
    # items without saying so is worse than one that fails.
    $rootKeys = Get-MenuBarRootKeySet
    $out = @()
    foreach ($i in $all) {
        $k = Get-ElementKey $i
        if ($k -and $rootKeys.ContainsKey($k)) { continue }                     # a bar root, not a popup item
        if ($rootKeys.Count -eq 0 -and $i.Current.AutomationId -ne '') { continue }   # no bar resolved: fall back
        $out += $i
    }
    # Return unrolled and let every call site re-wrap with @(). Returning ,@($out) instead would
    # suppress unrolling, and @() around that yields a 1-element array holding the array -- which
    # then silently member-enumerates ($i.Current.Name becomes an array) instead of erroring.
    return $out
}

# RuntimeIds of the menu-bar's own top-level items. Recomputed per call: cheap (one FindFirst plus one
# child FindAll) and never stale, which matters because UIA regenerates RuntimeIds when a menu reopens.
function Get-MenuBarRootKeySet {
    $set = @{}
    # The already-resolved main window, not a fresh Resolve-MainWindow: re-resolving cost a process
    # lookup plus a UIA search per call, and could answer with a DIALOG (see Wait-MainWindow) -- which
    # has no menu bar, so the root set came back empty and Get-MenuPopupItems silently fell through to
    # its documented-as-wrong "anything with an AutomationId is a bar root" heuristic.
    $win = if ($script:MainWindow) { $script:MainWindow } else { Resolve-MainWindow }
    if (-not $win) { return $set }
    $bar = $win.FindFirst($script:Desc,
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Menu)))
    if (-not $bar) { return $set }
    foreach ($t in @($bar.FindAll($script:ChildScope,
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::MenuItem))))) {
        $k = Get-ElementKey $t
        if ($k) { $set[$k] = $true }
    }

    # ...and the TOOLBAR's own separators.
    #
    # Get-MenuPopupItems searches the whole PROCESS, so any Separator anywhere in the app matches it. That
    # was harmless while the toolbar drew its rules as Rectangles -- and stopped being harmless the moment
    # they became real Separators (alignment F-45). Live-caught the same day (F-46): every menu dump grew a
    # phantom trailing separator, including the Vis menu, which draws no rules at all. A false GROUPING row
    # is the worst artefact this walker could produce, because menu grouping is exactly what these dumps are
    # compared on -- it would have read as an OpenVisual defect in the very comparison it corrupted.
    #
    # Scoped to the toolbar rather than to "everything the main window hosts": Avalonia's popups ARE
    # reachable from the main window element (they are overlay-hosted, not detached), so the broader rule
    # excluded the flyout content too and every menu dumped empty -- measured, not assumed.
    $bar2 = Find-ByAutomationId $win 'Toolbar'
    if ($bar2) {
        foreach ($e in @($bar2.FindAll($script:Desc,
            (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Separator))))) {
            $k = Get-ElementKey $e
            if ($k) { $set[$k] = $true }
        }
    }
    return $set
}

function Invoke-ElementClick {
    param($El)
    $r = $El.Current.BoundingRectangle
    if ($r.Width -lt 1 -or $r.Height -lt 1) { return $false }
    [Aui.Win32]::Click([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
    return $true
}

function Test-LivePopupItem {
    param($Item)
    try {
        $r = $Item.Current.BoundingRectangle
        if ($r.Width -lt 1 -or $r.Height -lt 1 -or [double]::IsInfinity($r.X) -or [double]::IsInfinity($r.Y)) { return $false }
        $hit = $script:AE::FromPoint((New-Object System.Windows.Point(
            [double]($r.X + $r.Width / 2), [double]($r.Y + $r.Height / 2))))
        for ($i = 0; $i -lt 12 -and $hit; $i++) {
            if ([System.Windows.Automation.Automation]::Compare($hit, $Item)) { return $true }
            $hit = $script:Walk.GetParent($hit)
        }
    } catch { }
    return $false
}

function Close-AllMenus {
    param($Window = $null)
    $proc = Get-AppProcess
    if ($proc -and @(Get-MenuPopupItems $proc.Id).Count -gt 0 -and $Window) {
        # Escape is program.leaveMode in programming mode; stale popup readings previously sent an
        # extra Esc and made id lookup report the post-transition item disabled.
        try {
            $bar = $Window.FindFirst($script:Desc,
                (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Menu)))
            if ($bar) {
                foreach ($top in @($bar.FindAll($script:ChildScope,
                    (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::MenuItem))))) {
                    $ec = Get-Pattern $top ([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
                    if ($ec) {
                        try { $ec.Collapse() } catch { }
                    }
                }
                Start-Sleep -Milliseconds 120
            }
        } catch { }

        # Context flyouts have no bar root. The UIA re-hit-test supplies positive popup identity; the
        # native helper narrows its top-level HWND by main-owner, process, visibility, class, and title.
        $remaining = @(Get-MenuPopupItems $proc.Id)
        $live = $null
        foreach ($candidate in $remaining) {
            if (Test-LivePopupItem $candidate) { $live = $candidate; break }
        }
        if ($live -and (Test-LivePopupItem $live)) {
            try {
                $r = $live.Current.BoundingRectangle
                $main = [IntPtr]$Window.Current.NativeWindowHandle
                $closed = [Aui.Win32]::CloseOwnedPopupAtPoint($proc.Id, $main,
                    [int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
                if ($closed) { Start-Sleep -Milliseconds 150 }
            } catch { }
        }
    }
    # Park the pointer off the menu bar. This matters: menus open on hover once the bar is in menu
    # mode, so a cursor left resting on a root (by a previous hover walk) leaves that root already
    # open -- and the next "click the root to open it" would TOGGLE IT SHUT instead. Parking makes
    # every walk start from the same cold state.
    if ($Window) {
        try {
            $r = $Window.Current.BoundingRectangle
            if ($r.Width -ge 1 -and $r.Height -ge 1) {
                [Aui.Win32]::SetCursorPos([int]($r.X + 8), [int]($r.Y + $r.Height - 8)) | Out-Null
                Start-Sleep -Milliseconds 100
            }
        } catch { }
    }
}

function Resolve-MenuSegments {
    param($Spec, $Opts)
    # Base path from the registry row, optionally extended by --menu-path so one row can address a
    # dynamic catalog subtree (e.g. base "Insert/Products" + "Wired products/FUGA/Lampeudtag").
    $base = @()
    if ($Spec.PSObject.Properties.Name -contains 'menuPath' -and $Spec.menuPath) {
        $base = @($Spec.menuPath -split '/' | Where-Object { $_ -ne '' })
    }
    $extra = @()
    $mp = Get-OptValue $Opts @('menu-path', 'menuPath')
    # Split on unescaped '/'; a literal '/' inside a menu-item name is written '\/' (e.g. a product
    # named "Lux / Temperatur sensor med logning"). Backward-compatible: paths without '\/' are unchanged.
    if ($mp -and $mp -isnot [bool]) { $extra = @([string]$mp -split '(?<!\\)/' | Where-Object { $_ -ne '' } | ForEach-Object { $_ -replace '\\/', '/' }) }
    return @($base + $extra)
}

# Open a menu item's submenu. Prefers the ExpandCollapse PATTERN and falls back to a real click.
#
# The pattern route arrived on 2026-08-08, when OpenVisual gained AccessibleMenu/AccessibleMenuItem:
# Avalonia's stock MenuItemAutomationPeer implements IToggleProvider and nothing else, so until then a
# menu could only be opened by clicking it. The pattern is strictly better where it exists -- it needs
# no foreground, no on-screen rectangle and no coordinates, so it cannot land on the wrong element and
# cannot be stolen by another window mid-walk. The click fallback stays because it is what makes this
# function honest about a menu that does NOT carry the pattern (an older build, a stock MenuItem someone
# reintroduces), rather than failing in a way that reads as "the item is missing".
function Open-MenuItemElement {
    param($Item, [ref] $Route)
    $ec = Get-Pattern $Item ([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    if ($ec) {
        try { $ec.Expand(); $Route.Value = 'pattern'; return $true } catch { }
    }
    if (Invoke-ElementClick $Item) { $Route.Value = 'click'; return $true }
    return $false
}

# Activate a LEAF menu item: Invoke pattern first, click second. Same reasoning as Open-MenuItemElement.
function Invoke-MenuItemElement {
    param($Item, [ref] $Route)
    $inv = Get-Pattern $Item ([System.Windows.Automation.InvokePattern]::Pattern)
    if ($inv) {
        try { $inv.Invoke(); $Route.Value = 'pattern'; return $true } catch { }
    }
    if (Invoke-ElementClick $Item) { $Route.Value = 'click'; return $true }
    return $false
}

# The realized items one level below an OPEN menu item. Two sources, in order:
#   1. the item's own UIA descendants -- correct and cheap once the submenu is open;
#   2. the process's popup windows (Get-MenuPopupItems) -- the original route, kept because a popup that
#      is not parented under its owning item in the UIA tree would otherwise read as an empty submenu.
function Get-MenuChildItems {
    param($Parent, $ProcId)
    $kids = @()
    try {
        $kids = @($Parent.FindAll($script:Desc,
            (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::MenuItem))))
    } catch { $kids = @() }
    if ($kids.Count -gt 0) { return $kids }
    return @(Get-MenuPopupItems $ProcId)
}

# Does this menu element answer to $Seg? By AutomationId FIRST, then by label.
#
# Id-first is the point of the 2026-08-08 change: every menu item now carries its CommandRegistry row id
# ("file.new", "edit.cut"), and those do not move when the wording does. The labels are Danish and carry
# both access-key underscores and a real ellipsis character ("Gem projekt _som…"), which is exactly the
# kind of literal that goes stale silently -- report.generate shipped for months with a menuPath whose
# every segment was wrong. Matching the id first means a caller can use either and the robust one wins.
function Test-MenuSegmentMatch {
    param($Element, [string] $Seg)
    try {
        if ($Element.Current.AutomationId -eq $Seg) { return $true }
        if ($Element.Current.Name -eq $Seg) { return $true }
    } catch { }
    return $false
}

function Open-MenuPath {
    param($Window, [string[]] $Segments, $Opts = @{})
    $proc = Get-AppProcess
    if (-not $proc) { return @{ ok = $false; code = 'AppNotRunning'; message = 'App not running.' } }
    $last = $Segments.Count

    $topName = $Segments[0]
    # "Filer" -> MenuFiler is not a thing; the bar's ids are MenuFile/MenuEdit/... so the "Menu"+label
    # convention is tried first for compatibility, then the id verbatim ("MenuFile"), then the label.
    $top = Find-ByAutomationId $Window "Menu$topName"
    if (-not $top) { $top = Find-ByAutomationId $Window $topName }
    if (-not $top) { $top = Find-ByName $Window $topName ([System.Windows.Automation.ControlType]::MenuItem) }
    if (-not $top) { return @{ ok = $false; code = 'TargetNotFound'; message = "Menu bar has no '$topName'." } }
    if ($last -eq 0) { return @{ ok = $true; code = 'Ok'; message = "Resolved '$topName'."; element = $top } }

    $routes = @()
    $route = ''
    if (-not (Open-MenuItemElement $top ([ref]$route))) {
        return @{ ok = $false; code = 'TargetNotFound'; message = "Menu '$topName' could not be opened (no ExpandCollapse pattern and no on-screen rectangle)." }
    }
    $routes += $route
    Start-Sleep -Milliseconds 450

    $parent = $top
    for ($k = 1; $k -lt $last; $k++) {
        $seg = $Segments[$k]
        $items = @(Get-MenuChildItems $parent $proc.Id)
        $match = $null
        foreach ($i in $items) { if (Test-MenuSegmentMatch $i $seg) { $match = $i; break } }
        if (-not $match) {
            $avail = (@($items | ForEach-Object {
                $n = $_.Current.Name; $a = $_.Current.AutomationId
                if ($a) { "$n [$a]" } else { $n } }) -join ', ')
            Close-AllMenus $Window
            return @{ ok = $false; code = 'TargetNotFound'
                message = "Menu item '$seg' not found while walking '$($Segments -join '/')'. Realized at this level: [$avail]" }
        }
        if (-not $match.Current.IsEnabled) {
            Close-AllMenus $Window
            return @{ ok = $false; code = 'PreconditionMissing'; message = "Menu item '$seg' is present but disabled." }
        }
        # The last segment is the command; everything before it is a container to open. Asking a leaf to
        # Expand (or a container to Invoke) is how a walk half-runs and still reports success.
        if ($k -eq ($last - 1)) {
            $gated = Test-MenuTargetGate $match $Opts $Window
            if ($gated) { return @{ ok = $false; code = 'ConfirmationRequired'; message = $gated.message; gated = $gated } }
        }
        $ok = if ($k -eq ($last - 1)) { Invoke-MenuItemElement $match ([ref]$route) }
              else { Open-MenuItemElement $match ([ref]$route) }
        if (-not $ok) {
            Close-AllMenus $Window
            return @{ ok = $false; code = 'MutationFailed'; message = "Menu item '$seg' could not be activated (no pattern and no on-screen rectangle)." }
        }
        $routes += $route
        $parent = $match
        Start-Sleep -Milliseconds 500
    }
    return @{ ok = $true; code = 'Ok'; message = "Walked '$($Segments -join '/')'."; routes = $routes }
}

# The gate a menu/flyout TARGET needs, judged by what it does rather than by which row asked for it.
#
# The generic invokers (menu.invoke, menu.invokeContext) can reach ANY item, including the two the
# registry gates by name: node.delete/product.delete carry confirmDestructive and controller.send carries
# confirmCaution. Without this a caller could run `menu invoke --id edit.delete` and walk straight past a
# gate the skill advertises as a safety property -- the same side door that was closed on key.send when
# {DELETE} turned out to be node.delete by another name. The gate is on the EFFECT, so it keys on the
# resolved item (id first, label second) and not on the command that reached it.
#
# Returns the required flag name, or $null when the target is ungated.
function Get-MenuTargetGate {
    param([string] $Id, [string] $Label)
    $bare = ($Id -replace '^ctx\.', '')
    $clean = ($Label -replace '_', '')
    if ($bare -eq 'edit.delete' -or $clean -in @('Slet', 'Delete')) { return 'confirm-destructive' }
    if ($bare -eq 'controller.send' -or $clean -in @('Send projekt…', 'Send project…')) { return 'confirm-caution' }
    return $null
}

# Refuse an unconfirmed gated target. $null when the call may proceed.
function Test-MenuTargetGate {
    param($Element, $Opts, $Window)
    $id = ''; $label = ''
    try { $id = [string]$Element.Current.AutomationId; $label = [string]$Element.Current.Name } catch { }
    $gate = Get-MenuTargetGate $id $label
    if (-not $gate) { return $null }
    if ($Opts.ContainsKey($gate)) { return $null }
    Close-AllMenus $Window
    $what = if ($id) { $id } else { $label }
    $why = if ($gate -eq 'confirm-destructive') {
        "it performs an irreversible removal, which is exactly what node.delete is gated for"
    } else {
        "it writes the project to the controller, which is exactly what controller.send is gated for"
    }
    return (New-Result -Ok $false -Code 'ConfirmationRequired' `
        -Message "Refusing to invoke '$what' without --$gate`: $why. The gate follows the EFFECT, so reaching the item generically does not remove it." `
        -Context (Get-Context $Window))
}

# Resolve a menu-bar command by its AutomationId alone, with no path.
#
# The ids ARE the app's CommandRegistry row ids, so a caller that knows the command knows the id -- but
# an id says nothing about WHERE the item lives, so this opens each top-level menu in turn and searches
# it. That costs up to eight submenu expansions, which the pattern route makes cheap and side-effect-free
# (nothing is invoked while searching; a menu that does not hold the id is closed again).
function Find-MenuItemById {
    param($Window, [string] $Id)
    $proc = Get-AppProcess
    $menuBar = $Window.FindFirst($script:Desc,
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Menu)))
    if (-not $menuBar) { return $null }
    $tops = @($menuBar.FindAll($script:ChildScope,
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::MenuItem))))
    foreach ($t in $tops) {
        if ($t.Current.AutomationId -eq $Id) { return @{ element = $t; path = @($t.Current.Name) } }
    }
    foreach ($t in $tops) {
        Close-AllMenus $Window
        $route = ''
        if (-not (Open-MenuItemElement $t ([ref]$route))) { continue }
        Start-Sleep -Milliseconds 350
        $hit = Search-MenuSubtreeById $t $Id $proc.Id @($t.Current.Name)
        if ($hit) { return $hit }
        Close-AllMenus $Window
    }
    return $null
}

# Depth-first search below an already-open menu item. Opens containers as it descends (never invokes).
function Search-MenuSubtreeById {
    param($Parent, [string] $Id, $ProcId, [string[]] $Path, [int] $Depth = 0)
    if ($Depth -ge 4) { return $null }
    foreach ($i in @(Get-MenuChildItems $Parent $ProcId)) {
        $iid = ''
        try { $iid = [string]$i.Current.AutomationId } catch { continue }
        if ($iid -eq $Id) { return @{ element = $i; path = @($Path + $i.Current.Name) } }
    }
    # Not at this level -- descend into the containers (an item that carries ExpandCollapse).
    foreach ($i in @(Get-MenuChildItems $Parent $ProcId)) {
        $ec = Get-Pattern $i ([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
        if (-not $ec) { continue }
        try { $ec.Expand() } catch { continue }
        Start-Sleep -Milliseconds 250
        $hit = Search-MenuSubtreeById $i $Id $ProcId @($Path + $i.Current.Name) ($Depth + 1)
        if ($hit) { return $hit }
        try { $ec.Collapse() } catch { }
    }
    return $null
}

function Invoke-Mechanism-Menu {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }

    # --id addresses a bar command by its AutomationId (= the app's CommandRegistry row id) with no path
    # at all: `aui menu invoke --id file.saveAs`. Preferred over --menu-path wherever the id is known,
    # because a path is a list of Danish labels and an id is not.
    $byId = Get-OptValue $Opts @('id', 'menu-id') -NamedOnly
    if ($byId -and $byId -isnot [bool]) {
        $hit = Find-MenuItemById $Window ([string]$byId)
        if (-not $hit) {
            Close-AllMenus $Window
            return (New-Result -Ok $false -Code 'TargetNotFound' `
                -Message "No menu item with AutomationId '$byId'. Enumerate them with ``menu dump-bar --with-id --depth 3``." `
                -Context (Get-Context $Window))
        }
        if (-not $hit.element.Current.IsEnabled) {
            Close-AllMenus $Window
            return (New-Result -Ok $false -Code 'PreconditionMissing' `
                -Message "Menu item '$byId' is present but disabled." -Context (Get-Context $Window))
        }
        $gated = Test-MenuTargetGate $hit.element $Opts $Window
        if ($gated) { return $gated }
        $route = ''
        if (-not (Invoke-MenuItemElement $hit.element ([ref]$route))) {
            Close-AllMenus $Window
            return (New-Result -Ok $false -Code 'MutationFailed' -Message "Menu item '$byId' could not be activated." -Context (Get-Context $Window))
        }
        Start-Sleep -Milliseconds 350
        return (New-Result -Ok $true -Code 'Ok' -Message "Invoked menu item '$byId'." -Verified $false `
            -Context (Get-Context $Window) -Data ([ordered]@{ automationId = [string]$byId; menuPath = ($hit.path -join '/'); route = $route }))
    }

    $segs = @(Resolve-MenuSegments $Spec $Opts)
    if ($segs.Count -lt 2) {
        return (New-Result -Ok $false -Code 'InvalidInput' `
            -Message "$($Spec.id) needs a menu path of at least 'TopMenu/Item' (registry menuPath and/or --menu-path), or --id <AutomationId>." -Context (Get-Context $Window))
    }
    $r = Open-MenuPath $Window $segs $Opts
    if (-not $r.ok) {
        # A refusal is an answer, not a failure to reach the item: return it as-is rather than letting the
        # foreground guard below relabel it.
        if ($r.ContainsKey('gated')) { return $r.gated }
        # A walk that fell back to clicking needs the foreground; one carried by patterns does not. The
        # guard therefore runs HERE, on failure, where it can explain a click that went nowhere -- rather
        # than up front, where it refused pattern-driven walks that never touch the pointer at all.
        $guard = Assert-Foreground $Window
        if ($guard -and $r.code -ne 'PreconditionMissing') { return $guard }
        return (New-Result -Ok $false -Code $r.code -Message $r.message -Context (Get-Context $Window))
    }
    Start-Sleep -Milliseconds 350
    $routes = @(if ($r.ContainsKey('routes')) { $r.routes } else { @() })
    return (New-Result -Ok $true -Code 'Ok' -Message "Invoked menu '$($segs -join '/')'." -Verified $false `
        -Context (Get-Context $Window) -Data ([ordered]@{ menuPath = ($segs -join '/'); routes = $routes }))
}

function Invoke-Mechanism-DialogCancel {
    param($Spec, $Opts, $Window)
    $modal = Get-OpenModalWindow
    if (-not $modal) { return (New-Result -Ok $true -Code 'Ok' -Message 'No modal open.' -Verified $true -Context (Get-Context $Window)) }
    $title = $modal.Current.Name
    if (-not (Set-Foreground $modal)) {
        return (New-Result -Ok $false -Code 'PreconditionMissing' `
            -Message "Could not bring the modal '$title' to the foreground; refusing to send Esc so it cannot reach another application." `
            -Context (Get-Context $Window))
    }
    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    Start-Sleep -Milliseconds 250
    # Verify by effect: the modal must actually be gone.
    $still = Get-OpenModalWindow
    if (-not $still) {
        return (New-Result -Ok $true -Code 'Ok' -Message "Cancelled modal '$title' with Esc." -Verified $true `
            -Context (Get-Context $Window) -Data ([ordered]@{ route = 'esc' }))
    }
    # Not every dialog honours Esc — OpenVisual's Delete confirm does not (app finding F-018), and an
    # undismissable modal blocks every later command. Fall back to its negative button so the queue
    # cannot wedge. This is a workaround, NOT a fix for F-018: keep reporting which route worked.
    # Dismissal labels only, in both languages. Never 'OK'/'Yes': those COMMIT the dialog, and a
    # "cancel" that saves is worse than one that fails. 'Close'/'Luk' are here because the About
    # dialog's only button is Close.
    foreach ($name in @('No', 'Cancel', 'Annuller', 'Nej', 'Close', 'Luk')) {
        $btn = Invoke-DialogButton $still $name
        if ($btn) {
            Start-Sleep -Milliseconds 300
            if (-not (Get-OpenModalWindow)) {
                return (New-Result -Ok $true -Code 'Ok' -Message "Modal '$title' ignored Esc; dismissed it with '$name'." -Verified $true `
                    -Context (Get-Context $Window) `
                    -Warnings @("'$title' does not respond to Esc; dismissed via its '$name' button instead.") `
                    -Data ([ordered]@{ route = "button:$name" }))
            }
        }
    }
    return (New-Result -Ok $false -Code 'DialogError' `
        -Message "Modal '$($still.Current.Name)' survived Esc and had no No/Cancel button to invoke." `
        -Context (Get-Context $Window))
}

# Invoke a named button inside an open modal, by searching THAT MODAL's own subtree.
#
# (The comment here used to justify the subtree search with "OpenVisual's dialogs are IN-WINDOW
# OVERLAYS - the process has a single HWND". That is wrong, and was corrected in commands.json on
# 2026-07-17: every dialog is a separate top-level Window shown via Views\ResultDialog.cs ->
# ShowDialog(owner). The code was always right -- $Modal is whatever Get-OpenModalWindow resolved, and
# searching its subtree works either way -- but the stated reason was a disproved claim, and a wrong
# reason is what a later change reasons FROM.)
function Invoke-DialogButton {
    param($Modal, [string] $Name)
    if (-not $Modal) { return $false }
    $btns = $Modal.FindAll($script:Desc,
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Button)))
    # AutomationId FIRST, then UIA Name -- the same addressing rule dialog.setText already uses. A dialog can
    # carry two buttons with the SAME label (the enumerator editor has a "Ny", a "Slet" and an "Omdoeb" per
    # pane), and a name-only match silently took whichever came first in tree order, leaving one of each pair
    # unreachable rather than saying so.
    foreach ($b in $btns) {
        if ($b.Current.AutomationId -eq $Name) {
            $inv = Get-Pattern $b ([System.Windows.Automation.InvokePattern]::Pattern)
            if ($inv) { $inv.Invoke(); return $true }
        }
    }
    foreach ($b in $btns) {
        if ($b.Current.Name -eq $Name) {
            $inv = Get-Pattern $b ([System.Windows.Automation.InvokePattern]::Pattern)
            if ($inv) { $inv.Invoke(); return $true }
        }
    }
    return $false
}

# Type a value into a named field of the OPEN modal. The write half of dialog.read: without it a dialog
# could be inspected but never filled in, so every "edit via its properties dialog" flow stopped at the
# inventory. Addresses by AutomationId first (Avalonia falls back to x:Name, so NameBox/NoteBox work),
# then by UIA Name for controls the XAML did not name.
#
# Sets through ValuePattern rather than synthesized keystrokes: keystrokes need the foreground, land
# wherever focus happens to be, and silently APPEND to whatever the field already holds. A ValuePattern
# SetValue replaces the content outright, which is what "set this field to X" has to mean. The value is
# read back and reported in data.readBack, because a read-only or bound-one-way field accepts the call
# and keeps its old text.
function Invoke-Mechanism-DialogSetText {
    param($Spec, $Opts, $Window)
    # `dialog set-text NameBox "New name"` -- two distinct positionals, so --text declares index 1.
    $field = Get-OptValue $Opts @('field', 'control', 'id') -PositionalIndex 0
    $value = Get-OptValue $Opts @('text', 'value') -PositionalIndex 1
    if (-not $field -or $field -is [bool]) {
        return (New-Result -Ok $false -Code 'InvalidInput' -Message 'dialog.setText requires --field <name>.')
    }
    if ($null -eq $value -or $value -is [bool]) { $value = '' }
    $modal = Get-OpenModalWindow
    if (-not $modal) { return (New-Result -Ok $false -Code 'DialogNotFound' -Message 'No modal open.' -Context (Get-Context $Window)) }

    $target = Find-ByAutomationId $modal ([string]$field)
    if (-not $target) { $target = Find-ByName $modal ([string]$field) }
    if (-not $target) {
        $seen = @()
        foreach ($e in $modal.FindAll($script:Desc,
            (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Edit)))) {
            $seen += ($e.Current.AutomationId + '/' + $e.Current.Name)
        }
        return (New-Result -Ok $false -Code 'TargetNotFound' `
            -Message ("Modal '" + $modal.Current.Name + "' has no field '$field'. Edit fields: " + ($seen -join ', ') + '.') `
            -Context (Get-Context $Window))
    }

    $vp = Get-Pattern $target ([System.Windows.Automation.ValuePattern]::Pattern)
    if (-not $vp) {
        # A composite editor (a NumericUpDown/Spinner) exposes no Value of its own — the text lives in the Edit
        # inside it, whose AutomationId is the template's (PART_TextBox), the same for every such control on the
        # dialog and so unaddressable from the top. Descend into the NAMED control instead of asking the caller to
        # guess which PART_TextBox is which.
        $inner = $target.FindFirst($script:Desc,
            (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Edit)))
        if ($inner) {
            $target = $inner
            $vp = Get-Pattern $target ([System.Windows.Automation.ValuePattern]::Pattern)
        }
    }
    if (-not $vp) {
        return (New-Result -Ok $false -Code 'NotAllowed' -Message "Field '$field' has no Value pattern (not a text field)." -Context (Get-Context $Window))
    }
    if ($vp.Current.IsReadOnly) {
        return (New-Result -Ok $false -Code 'NotAllowed' -Message "Field '$field' is read-only." -Context (Get-Context $Window))
    }
    $vp.SetValue([string]$value)
    Start-Sleep -Milliseconds 150
    $back = (Get-Pattern $target ([System.Windows.Automation.ValuePattern]::Pattern)).Current.Value
    $landed = ($back -eq [string]$value)
    return (New-Result -Ok $landed -Code $(if ($landed) { 'Ok' } else { 'NoEffect' }) `
        -Message $(if ($landed) { "Set '$field'." } else { "Set '$field' but it reads back as '$back'." }) `
        -Verified $landed -Context (Get-Context $Window) `
        -Data ([ordered]@{ field = $field; value = [string]$value; readBack = $back }))
}

# Choose an item in a COMBO BOX (or list) of the open modal.
#
# Alignment F-37: dialog.setText answers "not a text field" on a ComboBox and dialog.click resolves buttons, so a
# drop-down was undrivable from this side -- the second dialog control type in that state after check boxes
# (F-31), while the vendor driver has had dialog.selectItem all along.
#
# EXPANDS before enumerating, deliberately: Avalonia realizes a ComboBox's items lazily, so a dropdown that has
# never been opened reports ZERO ListItem children -- which is why dialog.read shows `items: []` for one and why
# reading the list is not enough to drive it. The dropdown is collapsed again afterwards, so the dialog is left as
# it was found apart from the selection.
function Invoke-Mechanism-DialogSelectItem {
    param($Spec, $Opts, $Window)
    $field = Get-OptValue $Opts @('field', 'control', 'id', 'name') -PositionalIndex 0
    if (-not $field -or $field -is [bool]) {
        return (New-Result -Ok $false -Code 'InvalidInput' -Message 'dialog.selectItem requires --field <name>.')
    }
    $text = Get-OptValue $Opts @('text', 'value', 'item') -PositionalIndex 1
    # --index is NAMED ONLY and parsed, not cast: without that it swallows the positional FIELD name and the cast
    # surfaces as an unhandled MutationFailed instead of a clean InvalidInput (it did exactly that once).
    $idx = Get-OptInt $Opts @('index') -1
    if (-not $idx.ok) { return (New-Result -Ok $false -Code 'InvalidInput' -Message $idx.message) }
    $index = $idx.value
    if (($null -eq $text -or $text -is [bool]) -and $index -lt 0) {
        return (New-Result -Ok $false -Code 'InvalidInput' -Message 'dialog.selectItem requires --text <item> or --index <n>.')
    }

    $modal = Get-OpenModalWindow
    if (-not $modal) { return (New-Result -Ok $false -Code 'DialogNotFound' -Message 'No modal open.' -Context (Get-Context $Window)) }

    $target = Find-ByAutomationId $modal ([string]$field)
    if (-not $target) { $target = Find-ByName $modal ([string]$field) }
    if (-not $target) {
        $seen = @()
        foreach ($e in $modal.FindAll($script:Desc,
            (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::ComboBox)))) {
            $seen += ($e.Current.AutomationId + '/' + $e.Current.Name)
        }
        return (New-Result -Ok $false -Code 'TargetNotFound' `
            -Message ("Modal '" + $modal.Current.Name + "' has no field '$field'. Drop-downs: " + ($seen -join ', ') + '.') `
            -Context (Get-Context $Window))
    }

    $ecp = Get-Pattern $target ([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    if ($ecp) {
        $ecp.Expand()
        Start-Sleep -Milliseconds 200
    }
    $items = @()
    $elements = @()
    foreach ($i in $target.FindAll($script:Desc,
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::ListItem)))) {
        $items += $i.Current.Name
        $elements += $i
    }
    if ($items.Count -eq 0) {
        if ($ecp) { $ecp.Collapse() }
        return (New-Result -Ok $false -Code 'TargetNotFound' `
            -Message "Field '$field' realized no items even after expanding (is it a drop-down?)." -Context (Get-Context $Window))
    }

    $pick = -1
    if ($index -ge 0) {
        $pick = $index
        if ($pick -ge $items.Count) {
            if ($ecp) { $ecp.Collapse() }
            return (New-Result -Ok $false -Code 'TargetNotFound' `
                -Message ("Index $pick is outside '$field' (0.." + ($items.Count - 1) + "). Items: " + ($items -join ', ') + '.') `
                -Context (Get-Context $Window))
        }
    }
    else {
        # Exact first, then a UNIQUE case-insensitive contains -- an ambiguous prefix is refused rather than
        # resolved to whichever came first, the same rule the menu walkers use.
        for ($n = 0; $n -lt $items.Count; $n++) { if ($items[$n] -ceq [string]$text) { $pick = $n; break } }
        if ($pick -lt 0) {
            $hits = @()
            for ($n = 0; $n -lt $items.Count; $n++) { if ($items[$n] -like "*$text*") { $hits += $n } }
            if ($hits.Count -eq 1) { $pick = $hits[0] }
            elseif ($hits.Count -gt 1) {
                if ($ecp) { $ecp.Collapse() }
                return (New-Result -Ok $false -Code 'TargetAmbiguous' `
                    -Message ("'$text' matches " + $hits.Count + " items of '$field'. Items: " + ($items -join ', ') + '.') `
                    -Context (Get-Context $Window))
            }
        }
        if ($pick -lt 0) {
            if ($ecp) { $ecp.Collapse() }
            return (New-Result -Ok $false -Code 'TargetNotFound' `
                -Message ("'$field' has no item '$text'. Items: " + ($items -join ', ') + '.') -Context (Get-Context $Window))
        }
    }

    $sip = Get-Pattern $elements[$pick] ([System.Windows.Automation.SelectionItemPattern]::Pattern)
    if (-not $sip) {
        if ($ecp) { $ecp.Collapse() }
        return (New-Result -Ok $false -Code 'NotAllowed' -Message "Item '$($items[$pick])' has no SelectionItem pattern." -Context (Get-Context $Window))
    }
    $sip.Select()
    Start-Sleep -Milliseconds 150
    if ($ecp) { $ecp.Collapse() }
    Start-Sleep -Milliseconds 100

    # Verified by reading the selection back as TEXT, never by assuming Select() landed -- and text rather than an
    # index because a collapsed Avalonia ComboBox virtualizes its items away: GetSelection() comes back empty and
    # an index lookup would report -1 for a selection that in fact landed. The control's own ValuePattern keeps
    # the chosen item's text, which is what dialog.read surfaces as `text` for the same reason.
    $wanted = $items[$pick]
    $after = ''
    $vp = Get-Pattern $target ([System.Windows.Automation.ValuePattern]::Pattern)
    if ($vp) { $after = [string]$vp.Current.Value }
    if (-not $after) {
        $sel = Get-Pattern $target ([System.Windows.Automation.SelectionPattern]::Pattern)
        if ($sel) {
            $chosen = @($sel.Current.GetSelection())
            if ($chosen.Count -gt 0) { $after = [string]$chosen[0].Current.Name }
        }
    }
    if (-not $after) {
        # A LIST (as opposed to a drop-down) keeps its items alive and answers neither ValuePattern nor, in this
        # toolkit, SelectionPattern.GetSelection() -- so ask the ITEMS which one is selected. Without this the
        # command reported NoEffect on a selection that had visibly landed (the enum manager's type list), which is
        # the worst kind of wrong answer: a working command reported broken.
        foreach ($e in $elements) {
            $sip2 = Get-Pattern $e ([System.Windows.Automation.SelectionItemPattern]::Pattern)
            if ($sip2 -and $sip2.Current.IsSelected) { $after = [string]$e.Current.Name; break }
        }
    }
    $landed = ($after -eq $wanted)
    return (New-Result -Ok $landed -Code $(if ($landed) { 'Ok' } else { 'NoEffect' }) `
        -Message $(if ($landed) { "Selected '$wanted' in '$field'." } else { "Asked for '$wanted' but '$field' reads '$after'." }) `
        -Verified $landed -Context (Get-Context $Window) `
        -Data ([ordered]@{ field = $field; requestedIndex = $pick; requestedText = $wanted; selectedAfter = $after; itemCount = $items.Count; items = @($items) }))
}

# Tick or untick a CHECKBOX in the open modal.
#
# Alignment F-31: dialog.click resolves BUTTONS only, so a checkbox in a dialog was undrivable from this side --
# `dialog click --button 'Gem aktuel værdi'` answers "has no button named ...". That left every dialog checkbox
# unverifiable LIVE (the F-27 power-fail control had to be driven from the context flyout instead), while the
# vendor driver has had dialog.setCheck all along. The two sides were asymmetric on a whole control type.
#
# Idempotent by construction: TogglePattern.Toggle() FLIPS, so toggling a box already in the wanted state would
# move it away from it. The state is read first and the toggle sent only when it differs -- and the result is
# verified by reading the state back, never by assuming the click landed.
function Invoke-Mechanism-DialogSetCheck {
    param($Spec, $Opts, $Window)
    $field = Get-OptValue $Opts @('field', 'control', 'id', 'name') -PositionalIndex 0
    if (-not $field -or $field -is [bool]) {
        return (New-Result -Ok $false -Code 'InvalidInput' -Message 'dialog.setCheck requires --field <name>.')
    }
    $on  = Get-OptValue $Opts @('on')
    $off = Get-OptValue $Opts @('off')
    if ($on -eq $true -and $off -eq $true) {
        return (New-Result -Ok $false -Code 'InvalidInput' -Message 'dialog.setCheck takes --on or --off, not both.')
    }
    $want = -not ($off -eq $true)   # ticking is the default; --off unticks

    $modal = Get-OpenModalWindow
    if (-not $modal) { return (New-Result -Ok $false -Code 'DialogNotFound' -Message 'No modal open.' -Context (Get-Context $Window)) }

    $target = Find-ByAutomationId $modal ([string]$field)
    if (-not $target) { $target = Find-ByName $modal ([string]$field) }
    if (-not $target) {
        $seen = @()
        foreach ($e in $modal.FindAll($script:Desc,
            (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::CheckBox)))) {
            $seen += ($e.Current.AutomationId + '/' + $e.Current.Name)
        }
        return (New-Result -Ok $false -Code 'TargetNotFound' `
            -Message ("Modal '" + $modal.Current.Name + "' has no field '$field'. Check boxes: " + ($seen -join ', ') + '.') `
            -Context (Get-Context $Window))
    }

    $tp = Get-Pattern $target ([System.Windows.Automation.TogglePattern]::Pattern)
    if (-not $tp) {
        return (New-Result -Ok $false -Code 'NotAllowed' -Message "Field '$field' has no Toggle pattern (not a check box)." -Context (Get-Context $Window))
    }
    $before = [string]$tp.Current.ToggleState
    $wanted = $(if ($want) { 'On' } else { 'Off' })
    if ($before -ne $wanted) {
        $tp.Toggle()
        Start-Sleep -Milliseconds 150
    }
    $after = [string](Get-Pattern $target ([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState
    $landed = ($after -eq $wanted)
    return (New-Result -Ok $landed -Code $(if ($landed) { 'Ok' } else { 'NoEffect' }) `
        -Message $(if ($landed) { "Set '$field' to $wanted." } else { "Asked for $wanted but '$field' reads $after." }) `
        -Verified $landed -Context (Get-Context $Window) `
        -Data ([ordered]@{ field = $field; wanted = $wanted; before = $before; after = $after; toggled = ($before -ne $wanted) }))
}

function Invoke-Mechanism-DialogButton {
    param($Spec, $Opts, $Window)
    $name = Get-OptValue $Opts @('button', 'name')
    if (-not $name) { return (New-Result -Ok $false -Code 'InvalidInput' -Message 'dialog.click requires --button <name>.') }
    $modal = Get-OpenModalWindow
    if (-not $modal) { return (New-Result -Ok $false -Code 'DialogNotFound' -Message 'No modal open.' -Context (Get-Context $Window)) }
    $title = $modal.Current.Name
    if (-not (Invoke-DialogButton $modal $name)) {
        $labels = @()
        foreach ($b in $modal.FindAll($script:Desc,
            (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Button)))) { $labels += $b.Current.Name }
        return (New-Result -Ok $false -Code 'TargetNotFound' `
            -Message "Modal '$title' has no button named '$name'. Buttons: $($labels -join ', ')." -Context (Get-Context $Window))
    }
    Start-Sleep -Milliseconds 350
    $gone = -not (Get-OpenModalWindow)
    return (New-Result -Ok $true -Code 'Ok' -Message "Invoked '$name' on modal '$title'." -Verified $gone `
        -Context (Get-Context $Window) -Data ([ordered]@{ button = $name; modal = $title; modalClosed = $gone }))
}

# Enumerate the OPEN modal's controls. READ-ONLY: opens nothing, changes nothing, closes nothing.
#
# Until this, an OpenVisual dialog was WRITE-ONLY to automation: the only enumeration in the driver was
# the button-label list inside dialog.click's FAILURE path (labels only, no enabled, no visible, and
# unreachable on success). That is why four rows sit at "partial" with "reading fields back is not yet
# wired" -- node.getProperties, node.rename, projectInfo.get, modules.list.
#
# Shape mirrors the vendor's dialog.read {dialog:{title}, controlCount, controls:[{id, class, text,
# enabled, visible, ...}]} so the two sides' dialogs compare like-for-like (compare3 C1.2c compares the
# vendor's variable dialog against OpenVisual's).
#
# `enabled` and `visible` are INDEPENDENT, exactly as on the vendor side: visible=false means the
# control is not rendered at all; visible=true + enabled=false means it is greyed. Collapsing them
# would make "hidden" and "disabled" indistinguishable, and that distinction is the whole of C15.
#
# Addressing note: no OpenVisual dialog control sets AutomationProperties.AutomationId explicitly --
# Avalonia falls back to the control's x:Name, which is why Find-ByAutomationId 'InstallationTree'
# works against a MainWindow.axaml that only sets x:Name. The code-built message boxes are the
# exception: AvaloniaDialogService.ShowButtonsAsync news up its Buttons with no Name, so those carry no
# id and are addressable only by UIA Name (= the button text). Hence `id` may legitimately be empty,
# and `name` is reported alongside it rather than folded into it.
# A UIA BoundingRectangle is Rect.Empty for an OFFSCREEN element, and Rect.Empty's X/Y are +Infinity --
# so a bare [int] cast overflows and takes the whole dump down (live-verified 2026-07-17: the Product
# properties dialog has offscreen controls, and reading it threw "Cannot convert value INF to Int32").
# A rect that does not exist must report as null, not as a number and not as a crash.
# NOTE [double]::IsFinite is .NET Core only -- this driver also runs on Windows PowerShell 5.1
# (.NET Framework), so the test is IsInfinity/IsNaN.
# $Geometry is passed in rather than probed here: every control of one dialog is on one monitor, so
# probing per control would ask the same question 54 times per read. A $null geometry is legal and
# means the sibling is omitted (D07), which is also what a caller gets on a machine where the probe
# fails outright.
function ConvertTo-RectDump {
    param($Rect, $Geometry)
    if ($null -eq $Rect) { return $null }
    foreach ($v in @($Rect.X, $Rect.Y, $Rect.Width, $Rect.Height)) {
        if ([double]::IsInfinity($v) -or [double]::IsNaN($v)) { return $null }
    }
    return (New-DeclaredRect -X ([int]$Rect.X) -Y ([int]$Rect.Y) `
                             -Width ([int]$Rect.Width) -Height ([int]$Rect.Height) -Geometry $Geometry)
}

function Get-DialogControlDump {
    param($Modal)
    $out = @()
    # One probe for the whole dialog, taken from the modal's own top-left so it names the monitor the
    # dialog is actually on rather than the primary one.
    $modalRect = $Modal.Current.BoundingRectangle
    $geometry = $null
    if (-not ([double]::IsInfinity($modalRect.X) -or [double]::IsNaN($modalRect.X))) {
        $geometry = Get-MonitorGeometry -X ([int]$modalRect.X) -Y ([int]$modalRect.Y)
    }
    $els = $Modal.FindAll($script:Desc, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($el in $els) {
        $c = $null
        try { $c = $el.Current } catch { continue }   # an element torn down mid-walk is not a finding
        $ct = $c.ControlType.ProgrammaticName -replace '^ControlType\.', ''
        # Structural containers carry no information a caller can act on and would triple the dump -- but a NAMED
        # one does: a captioned group box is the caption. Keeping only id-bearing containers hid OpenVisual's
        # three Projektinfo group captions ("Projekt oplysninger", "Installatør information", "Kunde oplysninger")
        # from every inventory, while the vendor's same three group boxes DO appear in its dialog.read (as
        # captioned Buttons) -- so the two sides' dialogs could not be diffed on grouping at all (alignment F-38).
        if ($ct -in @('Pane', 'Group', 'Custom') -and -not $c.AutomationId -and -not $c.Name) { continue }

        $row = [ordered]@{
            id      = $c.AutomationId
            name    = $c.Name
            class   = $ct
            enabled = $c.IsEnabled
            visible = -not $c.IsOffscreen
            rect    = ConvertTo-RectDump $c.BoundingRectangle $geometry
        }

        # A text field's CONTENT is what a caller reads back; Name is its label, not its value.
        $vp = Get-Pattern $el ([System.Windows.Automation.ValuePattern]::Pattern)
        if ($vp) { $row['text'] = $vp.Current.Value }

        # A ComboBox/List's fixed list -- the oracle for "what can this field be set to?".
        if ($ct -in @('ComboBox', 'List')) {
            $items = @()
            foreach ($i in $el.FindAll($script:Desc,
                (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::ListItem)))) {
                $items += $i.Current.Name
            }
            $row['items'] = @($items)
            $sel = Get-Pattern $el ([System.Windows.Automation.SelectionPattern]::Pattern)
            $row['selectedIndex'] = -1
            if ($sel) {
                $chosen = @($sel.Current.GetSelection())
                if ($chosen.Count -gt 0) {
                    for ($n = 0; $n -lt $items.Count; $n++) {
                        if ($items[$n] -eq $chosen[0].Current.Name) { $row['selectedIndex'] = $n; break }
                    }
                }
            }
        }

        # A checkbox/radio's checked state is its value.
        $tp = Get-Pattern $el ([System.Windows.Automation.TogglePattern]::Pattern)
        if ($tp) { $row['toggleState'] = [string]$tp.Current.ToggleState }

        $out += $row
    }
    return $out
}

function Invoke-Mechanism-DialogRead {
    param($Spec, $Opts, $Window)
    $modal = Get-OpenModalWindow
    if (-not $modal) {
        return (New-Result -Ok $false -Code 'DialogNotFound' `
            -Message 'No modal open. Open one first (e.g. node.doubleClick or node.properties), then read it.' `
            -Context (Get-Context $Window))
    }
    $title = $modal.Current.Name
    $controls = @(Get-DialogControlDump $modal)
    $warn = @()
    if ($controls.Count -eq 0) {
        $warn += "The modal '$title' resolved but enumerated ZERO controls. That is a walk failure, not a fact about the dialog -- capture it with capture.modal before recording anything from this."
    }
    $data = [ordered]@{
        dialog       = [ordered]@{ title = $title }
        controlCount = $controls.Count
        controls     = @($controls)
    }
    # Ok/Verified: this is a pure reader, so the successful enumeration IS the effect -- there is no
    # separate thing to read back. The no-modal path returned DialogNotFound above.
    return (New-Result -Ok $true -Code 'Ok' -Message "Modal '$title': $($controls.Count) controls." `
        -Verified $true -Warnings $warn -Context (Get-Context $Window) -Data $data)
}

# The open modal = a top-level Window of the app's process that is NOT the main window.
#
# "Not the main window" is decided by WINDOW HANDLE, not by title. The title test this used to do
# (`-notlike "*IHC OpenVisual"`) silently excluded any dialog whose caption ends with the product name,
# and Views\AboutWindow.axaml is titled exactly "About IHC OpenVisual". So after `help about` -- a
# status=confirmed command -- the About modal was INVISIBLE to the whole dialog surface: context.openModal
# read null, dialog.read/capture.modal answered DialogNotFound, and dialog.cancel returned
# ok:true "No modal open." while that modal blocked every command after it. A resolver that reports
# "nothing is open" about a window sitting in front of the app is the confident-wrong-answer failure
# this driver refuses everywhere else.
function Get-OpenModalWindow {
    $p = Get-AppProcess
    if (-not $p) { return $null }
    $root = $script:AE::RootElement
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ProcessIdProperty) $p.Id),
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Window)))
    $wins = $root.FindAll($script:Desc, $cond)
    $mainHandle = [IntPtr]::Zero
    if ($script:MainWindow) { try { $mainHandle = [IntPtr]$script:MainWindow.Current.NativeWindowHandle } catch { } }
    foreach ($w in $wins) {
        if ($mainHandle -ne [IntPtr]::Zero) {
            $h = [IntPtr]::Zero
            try { $h = [IntPtr]$w.Current.NativeWindowHandle } catch { }
            if ($h -eq $mainHandle) { continue }
            return $w
        }
        # No main window resolved (nothing is drivable then anyway): fall back to the old title test.
        if ($w.Current.Name -notlike "*$($script:WindowSuffix)") { return $w }
    }
    return $null
}

function Invoke-Mechanism-Capture {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $scope = $Spec.scope
    $target = $Window
    # A UIA control may have no native window handle; foreground its owning window while capturing its bounding rectangle.
    $foregroundTarget = $Window
    $captureData = $null
    if ($scope -eq 'modal') {
        $target = Get-OpenModalWindow
        if (-not $target) { return (New-Result -Ok $false -Code 'DialogNotFound' -Message 'No modal open to capture.' -Context (Get-Context $Window)) }
        $foregroundTarget = $target
    } elseif ($scope -eq 'control') {
        $id = Get-OptValue $Opts @('id') -NamedOnly
        if (-not $id -or $id -is [bool] -or [string]::IsNullOrWhiteSpace([string]$id)) {
            return (New-Result -Ok $false -Code 'InvalidInput' -Message 'capture control requires --id <AutomationId>.' -Context (Get-Context $Window))
        }
        $target = Find-ByAutomationId $Window ([string]$id)
        if (-not $target) {
            return (New-Result -Ok $false -Code 'TargetNotFound' -Message "No control with AutomationId '$id'." -Context (Get-Context $Window))
        }
        $captureData = [ordered]@{ automationId = [string]$id; name = [string]$target.Current.Name }
    }
    # Capture scrapes the screen, so a target that is not in front yields another app's pixels.
    # Failing to foreground is not fatal here (the rect may still be unoccluded) but it must be
    # surfaced rather than silently producing a misleading image.
    $fgOk = Set-Foreground $foregroundTarget
    $capWarn = @()
    if (-not $fgOk) { $capWarn += 'Could not foreground the capture target; the image may be occluded by another window.' }
    Start-Sleep -Milliseconds 200
    $rect = $target.Current.BoundingRectangle
    if ($rect.Width -lt 1 -or $rect.Height -lt 1) {
        return (New-Result -Ok $false -Code 'CaptureFailed' -Message 'Target has no on-screen rectangle.' -Context (Get-Context $Window))
    }
    $outDir = Join-Path $env:TEMP 'AuiOpenVisualCaptures'
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $outPath = Join-Path $outDir "aui-$scope-$stamp.png"
    $bmp = New-Object System.Drawing.Bitmap([int]$rect.Width, [int]$rect.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    $shot = New-ScreenshotMetadata -Path $outPath -Width ([int]$rect.Width) -Height ([int]$rect.Height) -Scope $scope
    # ok tracks the Code. CaptureOccluded is an exit-tier-4 failure, and returning it with ok:true made
    # this the one command where the envelope and the exit code disagreed -- a caller reading `ok` saw a
    # good screenshot, a caller reading the exit code saw a failure, and the file might be another app's
    # pixels. The PNG path is still reported either way, so an occluded capture stays inspectable.
    return (New-Result -Ok $fgOk -Code ($(if ($fgOk) { 'Ok' } else { 'CaptureOccluded' })) `
        -Message ($(if ($fgOk) { "Captured $scope to $outPath." } else { "Captured $scope to $outPath, but the target could not be brought to the front, so the image may show another window." })) `
        -Verified $fgOk -Warnings $capWarn `
        -Context (Get-Context $Window) -Screenshot $shot -Data $captureData)
}

# ── OS file picker ───────────────────────────────────────────────────────────
# The driver launches the app with no arguments (a startup path would bypass the picker this exists
# to exercise), and changing the app is out of scope for the comparison, so the picker must be driven. It is a
# modern IFileDialog hosted IN THE APP'S OWN PROCESS (class #32770), not a separate one. Its control
# ids are NOT the classic Win32 ones -- "1148" resolves to a Pane with no Edit child, and ids "1"/"2"
# collide with file-list ListItems -- so control-based entry is unreliable. What is reliable: the
# dialog opens with keyboard focus already in the file-name field, so typing the path and pressing
# Enter works regardless of the shell's internal layout. Verified live 2026-07-15.

function Get-FileDialogWindow {
    $p = Get-AppProcess
    if (-not $p) { return $null }
    $wins = $script:AE::RootElement.FindAll($script:Desc, (New-Object System.Windows.Automation.AndCondition(
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ProcessIdProperty) $p.Id),
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Window)))))
    foreach ($w in $wins) { if ($w.Current.ClassName -eq '#32770') { return $w } }
    return $null
}

function Wait-FileDialog {
    param([int] $TimeoutMs = 10000, [switch] $Gone)
    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    while ((Get-Date) -lt $deadline) {
        $d = Get-FileDialogWindow
        if ($Gone) { if (-not $d) { return $true } } elseif ($d) { return $d }
        Start-Sleep -Milliseconds 200
    }
    return $(if ($Gone) { $false } else { $null })
}

function ConvertTo-SendKeys {
    param([string] $Text)
    # SendKeys treats +^%~(){}[] as syntax; a path containing any of them must be escaped or it is
    # silently mistyped (e.g. "Program Files (x86)").
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $Text.ToCharArray()) {
        if ('+^%~(){}[]'.IndexOf($ch) -ge 0) { [void]$sb.Append('{'); [void]$sb.Append($ch); [void]$sb.Append('}') }
        else { [void]$sb.Append($ch) }
    }
    return $sb.ToString()
}

function Invoke-Mechanism-FileDialog {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $isSave = ($Spec.PSObject.Properties.Name -contains 'dialogKind') -and $Spec.dialogKind -eq 'save'
    $raw = Get-OptValue $Opts @('path', 'file')
    if (-not $raw -or $raw -is [bool]) {
        return (New-Result -Ok $false -Code 'InvalidInput' -Message "$($Spec.id) requires --path <file>." -Context (Get-Context $Window))
    }
    # Resolve to an absolute path: the picker's CWD is the app's, not this script's. Join-Path must
    # only be applied to a relative input -- joining an already-rooted path yields "D:\cwd\D:\file".
    $rawStr = [string]$raw
    $path = if ([System.IO.Path]::IsPathRooted($rawStr)) { [System.IO.Path]::GetFullPath($rawStr) }
            else { [System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $rawStr)) }
    if ($isSave) {
        $dir = Split-Path -Parent $path
        if ($dir -and -not (Test-Path -LiteralPath $dir)) {
            return (New-Result -Ok $false -Code 'InvalidInput' -Message "Target directory does not exist: $dir" -Context (Get-Context $Window))
        }
        if ((Test-Path -LiteralPath $path) -and -not $Opts.ContainsKey('overwrite')) {
            return (New-Result -Ok $false -Code 'TargetExists' `
                -Message "$path already exists; pass --overwrite to accept the picker's replace prompt." -Context (Get-Context $Window))
        }
    } elseif (-not (Test-Path -LiteralPath $path)) {
        return (New-Result -Ok $false -Code 'InvalidInput' -Message "No such file: $path" -Context (Get-Context $Window))
    }

    if (Get-FileDialogWindow) {
        return (New-Result -Ok $false -Code 'DialogBlocked' -Message 'A file picker is already open; close it first (dialog.cancel).' -Context (Get-Context $Window))
    }
    $guard = Assert-Foreground $Window
    if ($guard) { return $guard }

    # Raise the picker by whichever route the registry row declares.
    if (($Spec.PSObject.Properties.Name -contains 'automationId') -and $Spec.automationId) {
        $el = Find-ByAutomationId $Window $Spec.automationId
        if (-not $el) { return (New-Result -Ok $false -Code 'ControlNotFound' -Message "No control '$($Spec.automationId)'." -Context (Get-Context $Window)) }
        $inv = Get-Pattern $el ([System.Windows.Automation.InvokePattern]::Pattern)
        if (-not $inv) { return (New-Result -Ok $false -Code 'NotAllowed' -Message "Control '$($Spec.automationId)' has no Invoke pattern." -Context (Get-Context $Window)) }
        $inv.Invoke()
    } else {
        $segs = @(Resolve-MenuSegments $Spec $Opts)
        if ($segs.Count -lt 2) { return (New-Result -Ok $false -Code 'InvalidInput' -Message "$($Spec.id) needs a menuPath or automationId." -Context (Get-Context $Window)) }
        $r = Open-MenuPath $Window $segs
        if (-not $r.ok) { return (New-Result -Ok $false -Code $r.code -Message $r.message -Context (Get-Context $Window)) }
    }

    $dlg = Wait-FileDialog 12000
    if (-not $dlg) { return (New-Result -Ok $false -Code 'DialogNotFound' -Message 'The file picker did not appear.' -Context (Get-Context $Window)) }
    $dlgTitle = $dlg.Current.Name
    # Remembered so the replace prompt can be told apart from the picker by identity rather than caption.
    $pickerHandle = [IntPtr]::Zero
    try { $pickerHandle = [IntPtr]$dlg.Current.NativeWindowHandle } catch { }
    if (-not (Set-Foreground $dlg)) {
        return (New-Result -Ok $false -Code 'PreconditionMissing' -Message "Could not foreground the picker '$dlgTitle'; refusing to type the path blindly." -Context (Get-Context $Window))
    }
    Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait((ConvertTo-SendKeys $path))
    Start-Sleep -Milliseconds 400
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    Start-Sleep -Milliseconds 700

    # A save over an existing file raises a second #32770, "Confirm Save As", OWNED by the picker.
    # It must be answered by handle, not by keystroke: UI Automation cannot see an owned dialog at all
    # (Get-FileDialogWindow keeps returning the picker underneath it, so a foreground+Enter went to the
    # wrong window), and its DEFAULT button is "No", so even a correctly aimed Enter would decline the
    # replace and the picker would just sit there looking like a rejected path.
    $overwriteWarn = @()
    if ($isSave -and $Opts.ContainsKey('overwrite')) {
        $p = Get-AppProcess
        if ($p) {
            $deadline = (Get-Date).AddMilliseconds(4000)
            while ((Get-Date) -lt $deadline) {
                $confirm = [Aui.Win32]::FindDialogExcept($p.Id, $pickerHandle)
                if ($confirm -ne [IntPtr]::Zero) {
                    # The affirmative label is localized too; report what was there rather than
                    # clicking something arbitrary, so a miss is diagnosable instead of silent.
                    $seen = $null
                    foreach ($yes in @('Yes', 'Ja')) {
                        $seen = [Aui.Win32]::ClickDialogButton($confirm, $yes)
                        if (-not $seen) { break }
                    }
                    if ($seen) {
                        $overwriteWarn += "The replace prompt appeared but has no Yes/Ja button to click ($seen). Answer it by hand, or add the local label to the affirmative list in Invoke-Mechanism-FileDialog."
                    }
                    Start-Sleep -Milliseconds 500
                    break
                }
                Start-Sleep -Milliseconds 150
            }
        }
    }
    if (-not (Wait-FileDialog 20000 -Gone)) {
        $still = Get-FileDialogWindow
        return (New-Result -Ok $false -Code 'DialogTimeout' -Warnings $overwriteWarn `
            -Message "The picker is still open after entering the path (now '$($still.Current.Name)'); the path may have been rejected." -Context (Get-Context $Window))
    }

    # Verify by effect: the title bar must name the file we asked for.
    $leaf = Split-Path -Leaf $path
    $title = $Window.Current.Name
    $data = [ordered]@{ path = $path; dialogTitle = $dlgTitle; windowTitle = $title; verifiedBy = 'titleBaseName' }
    # A leftover modal is a stronger, explicit oracle than the title. In particular, the dirty-document
    # guard appears after the picker closes and otherwise gets misreported as a generic unchanged-title failure.
    $stuck = Get-OpenModalWindow
    if ($stuck) {
        $data['blockingModal'] = $stuck.Current.Name
        return (New-Result -Ok $false -Code 'DialogBlocked' `
            -Message ("$($Spec.id) did not complete: '" + $stuck.Current.Name + "' is still open (answer it, then retry). " +
                      "The title bar reads '$title', but cannot verify a flow blocked by that modal.") `
            -Context (Get-Context $Window) -Data $data)
    }
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 500
        $title = $Window.Current.Name
        if (Test-TitleNamesFile $title $leaf) { break }
    }
    $landed = (Test-TitleNamesFile $title $leaf)
    $data['windowTitle'] = $title
    if (-not $landed) {
        return (New-Result -Ok $false -Code 'NoEffect' `
            -Message "The picker closed but the title bar still reads '$title' rather than '$leaf'; the project did not load/save." `
            -Context (Get-Context $Window) -Data $data)
    }
    # The caption carries the BASE NAME only, so on its own it cannot tell "written where I asked" from "a
    # file of that name was already open" (the same blind spot the earlier leftover-modal check covers for
    # open). For a save the full path is checkable, so check it and stop relying on the weaker signal.
    # Only when ROOTED: the picker resolves a relative path against ITS working directory, not this
    # session's, so a Test-Path miss there would mean "looked in the wrong place", not "did not save".
    if ($isSave -and [System.IO.Path]::IsPathRooted($path)) {
        if (Test-Path -LiteralPath $path) { $data['verifiedBy'] = 'fileOnDisk' }
        else {
            return (New-Result -Ok $false -Code 'NoEffect' `
                -Message "The title bar reads '$title', but no file exists at '$path'; the save did not land where it was asked to." `
                -Context (Get-Context $Window) -Data $data)
        }
    }
    return (New-Result -Ok $true -Code 'Ok' -Message "$($Spec.id): '$leaf' -- title bar now '$title'." -Verified $true `
        -Warnings $overwriteWarn -Context (Get-Context $Window) -Data $data)
}

# ── Structural dumps (the oracle for the comparison census) ──────────────────
# Shared dump schema -- both drivers emit this shape so the vendor and OpenVisual dumps diff
# mechanically: { pane, capturedAfter, root: { label, expanded, children[] } }. Labels are never
# string-compared across the two apps (the vendor UI is Danish) -- only order, nesting and counts.

function ConvertTo-NodeDump {
    param($El, [int] $Depth)
    if ($script:DumpCount -ge $script:DumpMaxNodes) { $script:DumpTruncated = $true; return $null }
    $script:DumpCount++

    $ecp = Get-Pattern $El ([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $state = if ($ecp) { "$($ecp.Current.ExpandCollapseState)" } else { 'LeafNode' }
    # Expansion state is itself under comparison, so NEVER expand implicitly: a collapsed node is
    # reported collapsed with children:[] (its children are not realized in the UIA tree at all).
    # --expand-all opts into full structural coverage instead.
    if ($script:DumpExpandAll -and $ecp -and $state -eq 'Collapsed') {
        try { $ecp.Expand(); Start-Sleep -Milliseconds 90; $state = "$($ecp.Current.ExpandCollapseState)" } catch { }
    }
    $expanded = switch ($state) { 'Expanded' { $true } 'Collapsed' { $false } default { $null } }

    $kids = @()
    if ($Depth -lt $script:DumpMaxDepth) {
        # Get-TreeItemChildren, NOT FindAll(Children): see the note there. This dump is the comparison's
        # structural ORACLE, and it was built by the enumeration Resolve-TreePath rejects as returning the
        # wrong children under an expanded row — while stamping the result verified:true. A dump whose
        # hierarchy can be wrong is worse than no dump, because runs are diffed against it.
        $childEls = Get-TreeItemChildren $El
        foreach ($c in $childEls) {
            $d = ConvertTo-NodeDump $c ($Depth + 1)
            if ($d) { $kids += $d }
        }
    } elseif ($state -eq 'Expanded') { $script:DumpTruncated = $true }

    # Schema is exactly {label, expanded, children} to match the vendor-side tree.read dump, so the
    # two diff mechanically (tools\tree-diff.ps1). `kind` is ADDITIVE and opt-in for that reason: a new
    # mandatory field would break that tool, whose whole premise is the two schemas matching.
    #
    # SUPERSEDED RECORD (2026-07-17): this comment used to reject the field outright, reasoning that
    # "Avalonia TreeItems carry no AutomationId (verified empty on every node)". That WAS true and is
    # no longer -- enabler3 V1 binds AutomationProperties.AutomationId on both TreeViewItem styles to
    # TreeNodeViewModel.NodeKind, precisely because it was empty and therefore free. The reading was
    # right; the conclusion expired when the app changed.
    #
    # `kind` is NOT a duplicate of `label`, which is the objection that killed the earlier
    # `automationName` proposal: in programming mode the label is USER DATA and cannot identify a row
    # ("Kip Udgang" is a command, "Kip ved kort tryk -> ON" an event), which is exactly what the
    # comparison census has to partition by.
    $node = [ordered]@{
        label    = $El.Current.Name
        expanded = $expanded
        children = @($kids)
    }
    if ($script:DumpWithKind) {
        # The row's AutomationId is "<kind>#<element id>" as of the SPEC-01 locator fix (it used to be the bare
        # kind, which collided across every sibling of a type -- ten localities all read `locality`). Both halves
        # are reported, and they are different questions: `kind` is what the CENSUS partitions by (and must stay
        # the bare token, or every row becomes its own partition), `id` is what ADDRESSES one row.
        # 'unknown' when the app did not classify the row, so an empty string can never read as a kind.
        $k = [string]$El.Current.AutomationId
        $node['kind'] = $(if ([string]::IsNullOrEmpty($k)) { 'unknown' } else { ($k -split '#', 2)[0] })
        $node['id']   = $(if ([string]::IsNullOrEmpty($k)) { $null } else { $k })
    }
    return $node
}

function Invoke-Mechanism-TreeDump {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $treeId = Resolve-TreeId $Opts
    $tree = Find-ByAutomationId $Window $treeId
    if (-not $tree) { return (New-Result -Ok $false -Code 'ControlNotFound' -Message "Tree '$treeId' not found." -Context (Get-Context $Window)) }

    $script:DumpExpandAll  = $Opts.ContainsKey('expand-all')
    $script:DumpWithKind   = $Opts.ContainsKey('with-kind')
    $script:DumpCount      = 0
    $script:DumpTruncated  = $false
    $script:DumpMaxNodes   = 20000

    # --depth bounds the walk. It EXISTS because the mode oracle this driver's own docs prescribe is
    # `tree dump --depth 1` (commands.json: view.configuration, programming.enter) -- and until now
    # --depth was silently swallowed by Parse-Options, so that documented one-node probe realized the
    # ENTIRE pane on every call: ~20 000 UIA round trips against a 2.9 MB project, to read one label.
    # menuBarDump has taken --depth since enabler2; the asymmetry was an oversight, not a design.
    $depthOpt = Get-OptInt $Opts @('depth') 40
    if (-not $depthOpt.ok) { return (New-Result -Ok $false -Code 'InvalidInput' -Message $depthOpt.message -Context (Get-Context $Window)) }
    if ($depthOpt.value -lt 1) { return (New-Result -Ok $false -Code 'InvalidInput' -Message '--depth must be 1 or more.' -Context (Get-Context $Window)) }
    $script:DumpMaxDepth = $depthOpt.value

    # Optional --path scopes the dump to one subtree; otherwise dump every root TreeItem.
    #
    # RESOLVE, never Select: this is a reader (mutating:noState only because reaching a deep row expands
    # its ancestors). Selecting used to be a hidden side effect -- `tree dump --path X` moved the caret,
    # which silently pre-answers exactly the question node.rightClick exists to measure ("did the gesture
    # move the selection?") for anyone who dumps the tree first to pick a target.
    $path = Get-PathOpt $Opts @('path')
    $roots = @()
    if ($path) {
        $sel = Resolve-TreePath $Window $treeId $path
        if (-not $sel.ok) { return (New-Result -Ok $false -Code $sel.code -Message $sel.message -Context (Get-Context $Window)) }
        $roots = @($sel.element)
    } else {
        $roots = Get-TreeItemChildren $tree
    }

    $dumped = @()
    foreach ($r in $roots) { $d = ConvertTo-NodeDump $r 0; if ($d) { $dumped += $d } }

    # One pane always has exactly one conceptual root; when the pane has several top-level items we
    # wrap them so the schema stays { root: {...} } on both sides of the comparison.
    $rootNode = if ($dumped.Count -eq 1) { $dumped[0] } else {
        [ordered]@{ label = "($treeId)"; expanded = $true; children = @($dumped) }
    }

    $tv = if ($Opts.ContainsKey('tree') -and $Opts['tree'] -is [string] -and $Opts['tree'] -ne '') { [string]$Opts['tree'] } else { 'TV1' }
    # NamedOnly: the positional is the --path, and stamping it as capturedAfter labelled the dump with
    # the subtree it scoped to instead of the step it was captured after.
    $after = Get-OptValue $Opts @('after') -NamedOnly
    $data = [ordered]@{
        pane          = $tv.ToUpper()
        capturedAfter = $(if ($after -and $after -isnot [bool]) { [string]$after } else { $null })
        expandAll     = $script:DumpExpandAll
        withKind      = $script:DumpWithKind   # says whether nodes carry `kind`, so a reader never has to
        maxDepth      = $script:DumpMaxDepth   # infer the schema from whether the field happens to be there
        nodeCount     = $script:DumpCount
        root          = $rootNode
    }
    $warn = @()
    if ($script:DumpTruncated) { $warn += "Dump truncated at maxNodes=$($script:DumpMaxNodes)/maxDepth=$($script:DumpMaxDepth); the tree is larger than the reported structure." }
    return (New-Result -Ok $true -Code 'Ok' -Message "Dumped $($script:DumpCount) nodes from $($tv.ToUpper())." `
        -Verified $true -Warnings $warn -Context (Get-Context $Window) -Data $data)
}

# ── Raw key gesture passthrough ──────────────────────────────────────────────

# Gestures that destroy through the app's OWN key routing, and so need the same confirmation as the
# command that performs them by name.
#
# {DELETE} (and its {DEL} spelling) is the whole list today: MainWindow.axaml.cs routes Key.Delete to the
# edit.delete registry row, so `key send --gesture "{DELETE}"` is node.delete with the gate taken off — and
# key.send is deliberately ungated, so nothing else stopped it. The skill advertises "irreversible removal
# needs --confirm-destructive" as a safety property; a raw-gesture side door makes that claim false, which
# is worse than not claiming it. The confirmation PROMPT that follows stays answerable by ungated
# dialog.click: it cannot initiate anything, and a modal a caller cannot dismiss blocks every later command.
function Test-DestructiveGesture {
    param([string] $Gesture)
    return ([bool]($Gesture -match '\{DEL(ETE)?\}'))
}

function Invoke-Mechanism-KeySend {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $gesture = Get-OptValue $Opts @('gesture', 'key')
    if (-not $gesture -or $gesture -is [bool]) {
        return (New-Result -Ok $false -Code 'InvalidInput' `
            -Message 'key.send requires a gesture in SendKeys syntax, e.g. --gesture "{F2}" / "{DOWN}" / "+{F10}" / "^z".' -Context (Get-Context $Window))
    }
    $gesture = [string]$gesture
    # Controller traffic is a hard exclusion for the comparison: F5 is Send-project. Refuse it here
    # rather than rely on the operator remembering, because key.send bypasses every per-command gate.
    if ($gesture -match '\{F5\}') {
        return (New-Result -Ok $false -Code 'NotAllowed' `
            -Message 'Refusing to send {F5}: it is the controller Send-project gesture, and controller traffic is excluded. Use controller.send deliberately if that is really intended.' `
            -Context (Get-Context $Window))
    }
    if ((Test-DestructiveGesture $gesture) -and -not $Opts.ContainsKey('confirm-destructive')) {
        return (New-Result -Ok $false -Code 'ConfirmationRequired' `
            -Message "Refusing to send '$gesture' without --confirm-destructive: the app routes it to an irreversible removal (MainWindow sends Key.Delete to edit.delete), which is exactly what node.delete is gated for." `
            -Context (Get-Context $Window))
    }
    # Resolved UNCONDITIONALLY, above the optional --path: the delta below reads $treeId whether or not a
    # path was given, and with Set-StrictMode an unassigned variable THROWS. That threw after SendWait had
    # already fired, so the documented plain form (`key send --gesture "{DOWN}"`) performed the gesture and
    # then reported MutationFailed — inviting a retry that would perform it a second time.
    $treeId = Resolve-TreeId $Opts
    # Optionally target a specific node/pane first so the key lands on a known selection.
    # NamedOnly: the positional here is the GESTURE, and reading it as a path made `key send "{F2}"`
    # fail TargetNotFound trying to select a node named after the gesture.
    $path = Get-OptValue $Opts @('path') -NamedOnly
    $selInfo = $null
    if ($path -and $path -isnot [bool]) {
        $sel = Select-TreePath $Window $treeId $path
        if (-not $sel.ok) { return (New-Result -Ok $false -Code $sel.code -Message $sel.message -Context (Get-Context $Window)) }
        $selInfo = "$treeId/$path"
    }
    $guard = Assert-Foreground $Window
    if ($guard) { return $guard }
    $before = Get-Context $Window
    [System.Windows.Forms.SendKeys]::SendWait($gesture)
    Start-Sleep -Milliseconds 350
    $after = Get-Context $Window
    # Report the observable delta so the census can record the effect from the envelope alone.
    $delta = [ordered]@{
        gesture        = $gesture
        selectedBefore = (@($before.selections) | Where-Object { $_ -and $_.tree -eq $treeId } | Select-Object -First 1)
        selectedAfter  = (@($after.selections)  | Where-Object { $_ -and $_.tree -eq $treeId } | Select-Object -First 1)
        modalBefore    = $before.openModal
        modalAfter     = $after.openModal
        titleBefore    = $before.windowTitle
        titleAfter     = $after.windowTitle
        targeted       = $selInfo
    }
    return (New-Result -Ok $true -Code 'Ok' -Message "Sent '$gesture'." -Verified $false -Context $after -Data $delta)
}

# ── Menu inventories ─────────────────────────────────────────────────────────
function Invoke-Mechanism-ContextMenuDump {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $treeId = Resolve-TreeId $Opts
    $path = Get-PathOpt $Opts @('path')

    # Same opener as the act route: one ordering, one definition of "the flyout opened". This used to
    # return Ok/Verified:true with ZERO items and a mere warning -- an inventory of nothing, presented
    # as a verified reading of the menu, which is precisely how a driver failure gets recorded as a
    # census cell ("this node type has no context menu").
    $flyout = Open-ContextFlyout $Window $treeId $path
    if (-not $flyout.ok) {
        return (New-Result -Ok $false -Code $flyout.code -Message $flyout.message -Context (Get-Context $Window))
    }
    $target = $flyout.target
    # Screenshot BEFORE descending: hovering a submenu changes what is on screen, so taking the PNG
    # first keeps it a stable picture of the top level regardless of --depth.
    $shot = $null
    try {
        $rect = $Window.Current.BoundingRectangle
        if ($rect.Width -ge 1 -and $rect.Height -ge 1) {
            $outDir = Join-Path $env:TEMP 'AuiOpenVisualCaptures'
            New-Item -ItemType Directory -Force -Path $outDir | Out-Null
            $outPath = Join-Path $outDir ("aui-contextmenu-{0}.png" -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
            $bmp = New-Object System.Drawing.Bitmap([int]$rect.Width, [int]$rect.Height)
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
            $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
            $g.Dispose(); $bmp.Dispose()
            $shot = New-ScreenshotMetadata -Path $outPath -Width ([int]$rect.Width) -Height ([int]$rect.Height) -Scope 'window'
        }
    } catch { }

    # A context flyout can NEST (OpenVisual's section row offers 'Insert variable >' over the whole typed
    # variable palette). Enumerating only the flat top level reported that palette as a single item, while
    # the vendor's `menu dump-context --depth 2` lists its 20 leaves -- a driver asymmetry that reads as an
    # OpenVisual capability gap. Depth defaults to 1 so the previously verified flat payload is unchanged.
    $depthOpt = Get-OptInt $Opts @('depth') 1
    if (-not $depthOpt.ok) {
        Close-AllMenus $Window
        return (New-Result -Ok $false -Code 'InvalidInput' -Message $depthOpt.message -Context (Get-Context $Window))
    }
    if ($depthOpt.value -lt 1) {
        Close-AllMenus $Window
        return (New-Result -Ok $false -Code 'InvalidInput' -Message '--depth must be 1 or more.' -Context (Get-Context $Window))
    }
    $script:MenuTruncated = $false
    $script:MenuMaxDepth = $depthOpt.value
    $script:MenuWithId = [bool](Get-OptValue $Opts @('with-id') -NamedOnly)
    # Same walker the menu bar uses, so both surfaces emit the same row shape and diff mechanically.
    $rows = @(Get-MenuLevel $Window @($flyout.items) 1)
    Close-AllMenus $Window

    # attempts rides along for the same reason the MCP's data.attempts does: a retry that SAVED a run is
    # a fact about the driver, and hiding it lets an ordering regression masquerade as a healthy route.
    $data = [ordered]@{ target = $target; itemCount = @($rows).Count; maxDepth = $script:MenuMaxDepth;
                        attempts = $flyout.attempts; items = @($rows) }
    $warn = @()
    if ($script:MenuTruncated) { $warn += "Walk stopped at depth $($script:MenuMaxDepth); deeper submenus were not enumerated (raise with --depth)." }
    if ($flyout.attempts -gt 1) {
        $warn += "The flyout only opened on attempt $($flyout.attempts). The inventory below is from the retry and is complete, but a RECURRING retry means the foreground/focus order regressed -- investigate."
    }
    # The zero-items warning is gone because the zero-items CASE is gone: Open-ContextFlyout fails
    # TargetNotFound rather than returning an empty inventory dressed as a verified reading.
    return (New-Result -Ok $true -Code 'Ok' -Message "Context menu on '$target': $(@($rows).Count) items." `
        -Verified $true -Warnings $warn -Context (Get-Context $Window) -Screenshot $shot -Data $data)
}

function Get-ElementKey {
    param($El)
    # RuntimeId is the only exact identity for a UIA element; labels repeat across menu levels.
    try { return (($El.GetRuntimeId()) -join '.') } catch { return $null }
}

function Invoke-ElementHover {
    param($El)
    # HOVER, never click. Opening a submenu by clicking would INVOKE a leaf -- walking File would
    # press Exit, and walking Controller would press Send project (controller traffic is a hard
    # exclusion for this comparison). Hovering opens submenus exactly like a real user and cannot
    # activate anything. The two-step move guarantees a WM_MOUSEMOVE even if the cursor already
    # sits on the target.
    $r = $El.Current.BoundingRectangle
    if ($r.Width -lt 1 -or $r.Height -lt 1) { return $false }
    $cx = [int]($r.X + $r.Width / 2); $cy = [int]($r.Y + $r.Height / 2)
    [Aui.Win32]::SetCursorPos($cx - 3, $cy) | Out-Null
    Start-Sleep -Milliseconds 40
    [Aui.Win32]::SetCursorPos($cx, $cy) | Out-Null
    return $true
}

function Get-PopupKeySet {
    param([int] $AppPid)
    $set = @{}
    foreach ($i in @(Get-MenuPopupItems $AppPid)) { $k = Get-ElementKey $i; if ($k) { $set[$k] = $true } }
    return $set
}

function Get-MenuLevel {
    param($Window, $LevelItems, [int] $Depth)
    # Depth-first walk INSIDE ONE OPEN MENU SESSION. Never reopen mid-walk: a menu's UIA RuntimeIds
    # are regenerated every time it is reopened (verified live: 'Theme' moved from 42.5178760.4.x to
    # 42.5309832.4.x across a close/open), so identities from two different openings can never be
    # compared. Within one session they are stable, which makes the before/after set difference an
    # exact way to attribute newly realized items to the item just hovered.
    # Hovering a sibling closes the previous sibling's submenu, which is exactly what we want: the
    # snapshot taken before each hover reflects that collapse, so `new` is only ever this item's own
    # children. Leaves realize nothing and come back with children:[].
    $proc = Get-AppProcess
    $out = @()
    foreach ($el in @($LevelItems)) {
        $label = ''
        $enabled = $false
        try { $label = $el.Current.Name; $enabled = [bool]$el.Current.IsEnabled }
        catch { continue }   # element died with a collapsing popup; skip it rather than throw
        # A TEMPLATED item (ItemTemplate over a bound collection, e.g. File > Recent projects) has no Name
        # of its own -- its text lives in the TextBlock the template built. Reporting '' there would make a
        # populated list look like a list of blank rows, which is how the recent-projects list first read.
        if ([string]::IsNullOrEmpty($label)) {
            try {
                $t = $el.FindFirst($script:Desc,
                    (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Text)))
                if ($t) { $label = $t.Current.Name }
            } catch { }
        }
        # The accelerator is a SEPARATE TextBlock in Avalonia's item template (InputGesture), so it never
        # appears in Current.Name -- the vendor puts it in the label after a tab, and a facet comparing
        # accelerators saw nothing on this side until this was added (uxparity S-27).
        # UIA has a dedicated property for this (AcceleratorKey); the gesture is NOT part of Name and is not a
        # Text descendant either, so both of those come back empty.
        $accel = ''
        try { $accel = [string]$el.Current.AcceleratorKey } catch { }
        if (-not $accel) {
            try {
                foreach ($t in @($el.FindAll($script:Desc,
                    (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Text))))) {
                    $txt = $t.Current.Name
                    if ($txt -and $txt -ne $label -and $txt.Replace('_','') -ne $label) { $accel = $txt; break }
                }
            } catch { }
        }
        $isSeparator = $el.Current.ControlType.Id -eq ([System.Windows.Automation.ControlType]::Separator).Id
        $kids = @()
        # A separator has no submenu, so declining to walk one is not truncation. Sharing one condition with
        # the depth limit meant every menu that merely GROUPS its items — File, and every context flyout —
        # came back warning that deeper submenus had been omitted, about menus that have none. A truncation
        # flag that cries wolf on ordinary output is one a reader learns to ignore on the run that matters.
        if (-not $isSeparator) {
            if ($Depth -lt $script:MenuMaxDepth) {
                $before = Get-PopupKeySet $proc.Id
                if (Invoke-ElementHover $el) {
                    Start-Sleep -Milliseconds 450
                    $new = @()
                    foreach ($i in @(Get-MenuPopupItems $proc.Id)) {
                        $k = Get-ElementKey $i
                        if ($k -and -not $before.ContainsKey($k)) { $new += $i }
                    }
                    if (@($new).Count -gt 0) { $kids = @(Get-MenuLevel $Window $new ($Depth + 1)) }
                }
            } else { $script:MenuTruncated = $true }
        }
        $row = [ordered]@{ label = $label; accelerator = $accel; separator = $isSeparator;
                           enabled = $enabled; children = @($kids) }
        # --with-id appends the item's AutomationId (the app's CommandRegistry row id -- "file.new",
        # "ctx.edit.cut"). ADDITIVE and off by default, the same discipline tree.dump's --with-kind
        # follows and for the same reason: the default shape is the one the vendor driver's menu dump
        # emits, and tools that diff the two mechanically break on an extra key. Turn it on when the run
        # is about THIS app -- an id survives a rewording, a Danish label does not.
        if ($script:MenuWithId) {
            $iid = ''
            try { $iid = [string]$el.Current.AutomationId } catch { }
            $row['id'] = $iid
        }
        $out += $row
    }
    return @($out)
}

function Invoke-Mechanism-MenuBarDump {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    $guard = Assert-Foreground $Window
    if ($guard) { return $guard }
    $script:MenuTruncated = $false
    $depthOpt = Get-OptInt $Opts @('depth') 2
    if (-not $depthOpt.ok) { return (New-Result -Ok $false -Code 'InvalidInput' -Message $depthOpt.message -Context (Get-Context $Window)) }
    if ($depthOpt.value -lt 1) { return (New-Result -Ok $false -Code 'InvalidInput' -Message '--depth must be 1 or more.' -Context (Get-Context $Window)) }
    $script:MenuMaxDepth = $depthOpt.value
    $script:MenuWithId = [bool](Get-OptValue $Opts @('with-id') -NamedOnly)

    # Roots are the AutomationId-bearing menu-bar items, in bar order.
    $menuBar = $Window.FindFirst($script:Desc,
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::Menu)))
    if (-not $menuBar) { return (New-Result -Ok $false -Code 'ControlNotFound' -Message 'Menu bar not found.' -Context (Get-Context $Window)) }
    $tops = @($menuBar.FindAll($script:ChildScope,
        (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) ([System.Windows.Automation.ControlType]::MenuItem))))

    # --menu narrows the walk to one title; a registry row may pin it (e.g. project.recent.list).
    $only = Get-OptValue $Opts @('menu')
    if ((-not $only -or $only -is [bool]) -and ($Spec.PSObject.Properties.Name -contains 'menu') -and $Spec.menu) { $only = $Spec.menu }
    $proc = Get-AppProcess
    $titles = @()
    foreach ($t in $tops) {
        $name = $t.Current.Name
        if ($only -and $only -isnot [bool] -and $name -ne [string]$only) { continue }
        $kids = @()
        Close-AllMenus $Window
        # Clicking a menu-bar root is safe: the eight roots are always containers, never commands.
        # Everything below this point is opened by hover only, so no leaf is ever invoked.
        if (Invoke-ElementClick $t) {
            Start-Sleep -Milliseconds 500
            $lvl1 = @(Get-MenuPopupItems $proc.Id)
            $kids = @(Get-MenuLevel $Window $lvl1 1)
        }
        Close-AllMenus $Window
        $title = [ordered]@{ label = $name; enabled = [bool]$t.Current.IsEnabled; children = @($kids) }
        if ($script:MenuWithId) { $title['id'] = [string]$t.Current.AutomationId }
        $titles += $title
    }
    $warn = @()
    if ($script:MenuTruncated) { $warn += "Walk stopped at depth $($script:MenuMaxDepth); deeper submenus were not enumerated (raise with --depth)." }
    $data = [ordered]@{ titleCount = @($titles).Count; maxDepth = $script:MenuMaxDepth; titles = @($titles) }
    return (New-Result -Ok $true -Code 'Ok' -Message "Menu bar: $(@($titles).Count) titles walked to depth $($script:MenuMaxDepth)." `
        -Verified $true -Warnings $warn -Context (Get-Context $Window) -Data $data)
}

# Enumerate the toolbar in BAR ORDER: every button with its id, name and enabled state, and every separator as
# its own row. Shape mirrors the vendor driver's toolbar.dump ({buttonCount, buttons:[{index, id, name, enabled,
# separator}]}) so the two bars diff mechanically -- which is the point, since alignment F-5 (the vendor draws
# ONE rule where OpenVisual drew three) sat open for a campaign on a vendor-side measurement with nothing to
# compare against on this side (F-7).
#
# Separators are read as real rows because OpenVisual now publishes them: they were Rectangles, which are
# invisible to automation, so a dump could not have seen them at all (alignment F-45). A run of buttons with no
# rule between them and a run with one look identical to any client that cannot see a shape.
#
# ORDER is what makes this a layout reader rather than an inventory, so the walk is over the toolbar's own
# descendants in tree (document) order rather than over a set of ids.
function Invoke-Mechanism-ToolbarDump {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }

    $toolbar = Find-ByAutomationId $Window 'Toolbar'
    if (-not $toolbar) {
        return (New-Result -Ok $false -Code 'ControlNotFound' `
            -Message 'Toolbar not found. It is hidden while Vis > Vaerktoejslinie is off -- turn it back on with view.toolbar.toggle.' `
            -Context (Get-Context $Window))
    }

    $geometry = $null
    $barRect = $toolbar.Current.BoundingRectangle
    if (-not ([double]::IsInfinity($barRect.X) -or [double]::IsNaN($barRect.X))) {
        $geometry = Get-MonitorGeometry -X ([int]$barRect.X) -Y ([int]$barRect.Y)
    }

    $rows = @()
    $index = 0
    foreach ($el in $toolbar.FindAll($script:Desc, [System.Windows.Automation.Condition]::TrueCondition)) {
        $c = $null
        try { $c = $el.Current } catch { continue }   # an element torn down mid-walk is not a finding
        $ct = $c.ControlType.ProgrammaticName -replace '^ControlType\.', ''
        if ($ct -notin @('Button', 'Separator')) { continue }
        $rows += [ordered]@{
            index     = $index
            id        = [string]$c.AutomationId
            name      = [string]$c.Name
            enabled   = [bool]$c.IsEnabled
            separator = ($ct -eq 'Separator')
            rect      = ConvertTo-RectDump $c.BoundingRectangle $geometry
        }
        $index++
    }

    $buttons = @($rows | Where-Object { -not $_.separator })
    $seps = @($rows | Where-Object { $_.separator })
    $data = [ordered]@{
        entryCount     = @($rows).Count
        buttonCount    = $buttons.Count
        separatorCount = $seps.Count
        buttons        = @($rows)
    }
    return (New-Result -Ok $true -Code 'Ok' `
        -Message "Toolbar: $($buttons.Count) buttons, $($seps.Count) separators." `
        -Verified $true -Context (Get-Context $Window) -Data $data)
}

# Activate a ROW INSIDE AN OPEN DIALOG with real mouse input -- the gesture checklist dimension 13 is about
# ("clickable/activatable rows and resulting subdialogs"), and the one verb this driver could not perform.
# dialog.click drives NAMED BUTTONS; node.doubleClick drives TREE rows; neither reaches a row in a dialog's
# grid, so every "does activating this row open an editor?" question was driver-blind and therefore
# UNRESOLVED (checklist: a driver that reports itself blind fails a comparison exactly as a mismatch does).
#
# Deliberately GENERAL: any list-like container in any dialog, addressed by the container's AutomationId or
# Name, and the row by INDEX or by its text. It knows nothing about module maps, terminals or scenes -- one
# verb answers the question for all of them.
#
# Mirrors the vendor driver's dialog.clickRow contract (control + 0-based row + doubleClick) so the two
# sides' transcripts compare like for like; only the addressing differs, because Avalonia has no numeric
# control ids.
#
# Single vs double click is the POINT, not a convenience: they are different questions, and a selection
# message would set the selection without ever delivering a click, so it could answer neither.
function Invoke-Mechanism-DialogClickRow {
    param($Spec, $Opts, $Window)
    $field = Get-OptValue $Opts @('field', 'control', 'list', 'id', 'name') -PositionalIndex 0
    if (-not $field -or $field -is [bool]) {
        return (New-Result -Ok $false -Code 'InvalidInput' -Message 'dialog.clickRow requires --field <list>.')
    }
    # --row is NAMED ONLY and parsed rather than cast: a positional would swallow the field name, and the cast
    # would surface as an unhandled fault instead of a clean InvalidInput -- the lesson dialog.selectItem
    # already paid for.
    $rowOpt = Get-OptInt $Opts @('row', 'index') -1
    if (-not $rowOpt.ok) { return (New-Result -Ok $false -Code 'InvalidInput' -Message $rowOpt.message) }
    $row = $rowOpt.value
    $text = Get-OptValue $Opts @('text', 'value', 'item') -NamedOnly
    if ($row -lt 0 -and ($null -eq $text -or $text -is [bool])) {
        return (New-Result -Ok $false -Code 'InvalidInput' -Message 'dialog.clickRow requires --row <n> or --text <row text>.')
    }
    # A CELL is a refinement of a row, not a separate gesture -- omit --column and this clicks the row (what
    # the vendor's own verb does); supply it and the click lands on that column of that row. Two verbs would
    # duplicate every resolution and verification step here and make the caller choose between them.
    $column = Get-OptValue $Opts @('column', 'cell') -NamedOnly
    $double = [bool](Get-OptValue $Opts @('double', 'double-click') -NamedOnly)

    $modal = Get-OpenModalWindow
    if (-not $modal) {
        return (New-Result -Ok $false -Code 'DialogNotFound' -Message 'No modal open.' -Context (Get-Context $Window))
    }

    $target = Find-ByAutomationId $modal ([string]$field)
    if (-not $target) { $target = Find-ByName $modal ([string]$field) }
    if (-not $target) {
        $seen = @()
        foreach ($kind in @([System.Windows.Automation.ControlType]::List,
                            [System.Windows.Automation.ControlType]::Table,
                            [System.Windows.Automation.ControlType]::DataGrid)) {
            foreach ($e in $modal.FindAll($script:Desc,
                (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) $kind))) {
                $seen += ($e.Current.AutomationId + '/' + $e.Current.Name)
            }
        }
        return (New-Result -Ok $false -Code 'TargetNotFound' `
            -Message ("Modal '" + $modal.Current.Name + "' has no list '$field'. Lists: " + ($seen -join ', ') + '.') `
            -Context (Get-Context $Window))
    }

    # Rows are whatever the container realizes as items -- ListItem for a list, DataItem for a grid.
    $rows = @()
    foreach ($kind in @([System.Windows.Automation.ControlType]::ListItem,
                        [System.Windows.Automation.ControlType]::DataItem)) {
        foreach ($i in $target.FindAll($script:Desc,
            (New-PropCondition ([System.Windows.Automation.AutomationElement]::ControlTypeProperty) $kind))) {
            $rows += $i
        }
    }
    if ($rows.Count -eq 0) {
        return (New-Result -Ok $false -Code 'TargetNotFound' `
            -Message "List '$field' realizes no rows. A virtualized list realizes none until scrolled into view." `
            -Context (Get-Context $Window))
    }

    if ($row -lt 0) {
        for ($n = 0; $n -lt $rows.Count; $n++) {
            if ($rows[$n].Current.Name -eq [string]$text) { $row = $n; break }
        }
        if ($row -lt 0) {
            for ($n = 0; $n -lt $rows.Count; $n++) {
                if ($rows[$n].Current.Name -like "*$text*") { $row = $n; break }
            }
        }
        if ($row -lt 0) {
            return (New-Result -Ok $false -Code 'TargetNotFound' `
                -Message ("List '$field' has no row matching '$text'. Rows: " +
                          (($rows | ForEach-Object { $_.Current.Name }) -join ' | ') + '.') `
                -Context (Get-Context $Window))
        }
    }
    if ($row -ge $rows.Count) {
        return (New-Result -Ok $false -Code 'InvalidInput' `
            -Message "List '$field' has $($rows.Count) rows; row $row does not exist." -Context (Get-Context $Window))
    }

    $guard = Assert-Foreground $Window
    if ($guard) { return $guard }

    $element = $rows[$row]
    $hit = 'row'
    if ($column -and $column -isnot [bool]) {
        # Cells are whatever the row renders: a real grid exposes them, an Avalonia template row exposes its
        # leaf children. Addressed by header/AutomationId/text first, then by 0-based position -- so the same
        # option works whether or not the framework models columns at all.
        $cells = @($element.FindAll($script:Desc, [System.Windows.Automation.Condition]::TrueCondition))
        $pick = $null
        foreach ($c in $cells) {
            $cc = $null
            try { $cc = $c.Current } catch { continue }
            if ($cc.AutomationId -eq [string]$column -or $cc.Name -eq [string]$column) { $pick = $c; break }
        }
        if (-not $pick) {
            $ci = 0
            if ([int]::TryParse([string]$column, [ref]$ci) -and $ci -ge 0 -and $ci -lt $cells.Count) {
                $pick = $cells[$ci]
            }
        }
        if (-not $pick) {
            # No cells at all is a DIFFERENT answer from 'that column is missing', and the difference is
            # usually the finding: a row that realizes nothing beneath it is one flat string, so the list
            # only LOOKS like a grid -- headers painted above rows that have no columns to click.
            $detail = if ($cells.Count -eq 0) {
                "the row realizes NO cells, so it is a single flat item rather than a grid row (its whole text is '" +
                $element.Current.Name + "')"
            } else {
                'cells are ' + (($cells | ForEach-Object { $_.Current.AutomationId + '/' + $_.Current.Name }) -join ' | ')
            }
            return (New-Result -Ok $false -Code 'TargetNotFound' `
                -Message "Row $row of '$field' has no column '$column' -- $detail." `
                -Context (Get-Context $Window))
        }
        $element = $pick
        $hit = "column:$column"
    }
    $rect = $element.Current.BoundingRectangle
    if ($rect.Width -lt 1 -or $rect.Height -lt 1 -or [double]::IsInfinity($rect.X)) {
        return (New-Result -Ok $false -Code 'TargetNotFound' `
            -Message "Row $row of '$field' has no on-screen rectangle (scrolled out of view or collapsed); refusing to click blind." `
            -Context (Get-Context $Window))
    }
    $x = [int]($rect.X + $rect.Width / 2)
    $y = [int]($rect.Y + $rect.Height / 2)
    $modalBefore = $modal.Current.Name

    [Aui.Win32]::Click($x, $y)
    if ($double) {
        Start-Sleep -Milliseconds 60
        [Aui.Win32]::Click($x, $y)
    }
    Start-Sleep -Milliseconds 300

    # Effect-verified the way the vendor's verb is: the selection must actually LAND on the requested row, or
    # a dialog must have been raised -- otherwise this reports NoEffect rather than a click that went nowhere.
    # Any dialog raised is named, because that is usually the whole question being asked.
    $selected = $false
    $sp = Get-Pattern $rows[$row] ([System.Windows.Automation.SelectionItemPattern]::Pattern)
    if ($sp) { $selected = [bool]$sp.Current.IsSelected }
    $after = Get-OpenModalWindow
    $openedDialog = $null
    if ($after -and $after.Current.Name -ne $modalBefore) { $openedDialog = $after.Current.Name }

    $data = [ordered]@{
        field            = [string]$field
        row              = $row
        doubleClick      = $double
        rowText          = $rows[$row].Current.Name
        hitArea          = $hit
        rowSelectedAfter = $selected
        openedDialog     = $openedDialog
        modalBefore      = $modalBefore
        modalAfter       = $(if ($after) { $after.Current.Name } else { $null })
        point            = (New-DeclaredPoint -X $x -Y $y -Geometry (Get-MonitorGeometry -X $x -Y $y))
    }
    if (-not $selected -and -not $openedDialog) {
        return (New-Result -Ok $false -Code 'NoEffect' `
            -Message "Clicked $hit of row $row in '$field' but the selection did not land on it and no dialog opened." `
            -Verified $true -Context (Get-Context $Window) -Data $data)
    }
    $what = if ($double) { 'Double-clicked' } else { 'Clicked' }
    $raised = if ($openedDialog) { " It opened '$openedDialog'." } else { '' }
    return (New-Result -Ok $true -Code 'Ok' -Message "$what $hit of row $row in '$field'.$raised" `
        -Verified $true -Context (Get-Context $Window) -Data $data)
}

function Invoke-Mechanism-NotImplemented {
    param($Spec, $Opts, $Window)
    $note = if ($Spec.PSObject.Properties.Name -contains 'note' -and $Spec.note) { " $($Spec.note)" } else { '' }
    return (New-Result -Ok $false -Code 'NotImplemented' `
        -Message "Command '$($Spec.id)' is declared (status=$($Spec.status)) but its mechanism is not yet wired.$note" `
        -Verified $false -Context (Get-Context $Window))
}

# ─────────────────────────────────────────────────────────────────────────────
# Option parsing (CLI grammar: <domain> <verb> [positional...] [--flag value] [--switch])
# ─────────────────────────────────────────────────────────────────────────────
function Test-HelpToken {
    param([string] $Token)
    return $Token -in @('--help', '-h')
}

function Test-HelpRequested {
    param([string[]] $Tokens)
    foreach ($token in $Tokens) {
        if (Test-HelpToken $token) { return $true }
    }
    return $false
}

function Parse-Options {
    param([string[]] $Tokens)
    $opts = @{ _positional = @() }
    $i = 0
    while ($i -lt $Tokens.Count) {
        $t = $Tokens[$i]
        if ($t -like '--*') {
            $key = $t.Substring(2)
            if (($i + 1) -lt $Tokens.Count -and ($Tokens[$i + 1] -notlike '--*')) {
                $opts[$key] = $Tokens[$i + 1]; $i += 2
            } else { $opts[$key] = $true; $i += 1 }
        } else {
            $opts['_positional'] += $t; $i += 1
        }
    }
    return $opts
}

# Read an option: the named flag first, then -- unless -NamedOnly -- the positional at $PositionalIndex.
#
# THE POSITIONAL FALLBACK IS OPT-OUT FOR A REASON, and getting it wrong is not cosmetic. It used to be
# unconditional and index-0 for every key, so ANY option read on a command that also takes a positional
# path silently received THAT PATH:
#   * `node double-click "Localities/Kitchen"` -- the positional form this skill documents -- had its
#     --x-offset resolve to "Localities/Kitchen", and the [int] cast threw. The unhandled error became
#     Code=MutationFailed (tier 4, a RUNTIME/INTERACTION failure), so a plain usage form read as though
#     the app had misbehaved. Same for `tree dump <path>` and `menu dump-bar <Title>` via --depth.
#   * `tree dump "Localities"` stamped capturedAfter="Localities" from the same fallback.
#   * `key send "{F2}"` resolved --path to "{F2}" and failed TargetNotFound trying to select a node
#     named after the gesture.
#   * `node drag A B` gave --from AND --to the SAME positional, i.e. it dragged A onto A and reported
#     whatever that did -- the wrong-target class this driver refuses everywhere else.
# So: a command's PRIMARY positional (the path/gesture) reads at index 0, a SECOND positional (drag's
# --to, set-text's --text) declares its index, and everything else is -NamedOnly.
function Get-OptValue {
    param($Opts, [string[]] $Keys, [int] $PositionalIndex = 0, [switch] $NamedOnly)
    foreach ($k in $Keys) { if ($Opts.ContainsKey($k)) { return $Opts[$k] } }
    if ($NamedOnly) { return $null }
    $pos = @($Opts['_positional'])
    if ($pos.Count -gt $PositionalIndex) { return $pos[$PositionalIndex] }
    return $null
}

# An integer option, parsed rather than cast. Returns @{ ok; value; message }: a bad value is the
# caller's mistake and must come back as InvalidInput (tier 2), never as an unhandled cast that the
# top-level catch relabels MutationFailed. Always -NamedOnly -- see Get-OptValue's note.
function Get-OptInt {
    param($Opts, [string[]] $Keys, [int] $Default)
    $raw = Get-OptValue $Opts $Keys -NamedOnly
    if ($null -eq $raw -or $raw -is [bool]) { return @{ ok = $true; value = $Default } }
    $parsed = 0
    if (-not [int]::TryParse([string]$raw, [ref] $parsed)) {
        return @{ ok = $false; value = $Default
                  message = "--$($Keys[0]) expects a whole number, got '$raw'." }
    }
    return @{ ok = $true; value = $parsed }
}

# A --path that was passed as a bare switch arrives as [bool] $true, which would then be stringified
# and looked up as a node literally named "True". That is a usage error, so report it as one.
# Does the title bar name this file? An ORDINAL prefix test, not -like "$Leaf*".
#
# -like reads '[' and ']' as a wildcard character class and both are legal in a Windows filename, so
# "project[1].vis" was verified against a pattern that cannot match its own title bar: a save that worked
# reported NoEffect, tier 4, indistinguishable from the picker having rejected the path. Case-insensitive,
# as -like was and as the filesystem is.
function Test-TitleNamesFile {
    param([string] $Title, [string] $Leaf)
    if (-not $Title -or -not $Leaf) { return $false }
    return $Title.StartsWith($Leaf, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-PathOpt {
    param($Opts, [string[]] $Keys = @('path', 'name'))
    $p = Get-OptValue $Opts $Keys
    if ($p -is [bool]) { return $null }
    return $p
}

function Resolve-TreeId {
    param($Opts, [string[]] $Keys = @('tree'))
    $t = $null
    foreach ($key in $Keys) {
        if ($Opts.ContainsKey($key)) { $t = $Opts[$key]; break }
    }
    # Preserve node.drag's legacy grammar: omitted endpoint selectors inherit --tree.
    if (($null -eq $t -or $t -is [bool] -or $t -eq '') -and -not ($Keys -contains 'tree') -and $Opts.ContainsKey('tree')) {
        $t = $Opts['tree']
    }
    if ($null -eq $t) { $t = 'TV1' }
    # A bare `--tree` (no value) arrives as [bool]; .ToUpper() on it throws. Fall back to the default.
    if ($t -isnot [string] -or $t -eq '') { $t = 'TV1' }
    switch ($t.ToUpper()) {
        'TV1' { 'InstallationTree' }
        'TV2' { 'FunctionsTree' }
        default { $t }   # allow passing a raw AutomationId
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# Registry + dispatch
# ─────────────────────────────────────────────────────────────────────────────
function Import-Registry {
    if (-not (Test-Path $script:RegistryPath)) {
        Write-Result (New-Result -Ok $false -Code 'InvalidInput' -Message "Registry not found at $($script:RegistryPath).")
    }
    # -Encoding UTF8 is load-bearing: commands.json is BOM-less UTF-8 and several `item` values carry
    # U+2026 ("Save project as…"). Windows PowerShell 5.1 defaults to the ANSI codepage, which decodes
    # those bytes to "â€¦" and makes the menu lookup miss. PS7 auto-detects, so this is a no-op there.
    $script:Registry = Get-Content -Raw -LiteralPath $script:RegistryPath -Encoding UTF8 | ConvertFrom-Json
}

function Find-Spec {
    param([string] $Id)
    foreach ($c in $script:Registry.commands) { if ($c.id -eq $Id) { return $c } }
    return $null
}

# What a verb's transcript is evidence OF — see commands.json's `routeNote`. Derived from the mechanism
# rather than stored per row, because every row sharing a mechanism shares its route by construction: the
# mechanism IS how the verb reaches the app. A per-row copy could disagree with the code that dispatches it,
# which is the one way this field could mislead. An unmapped mechanism reports 'unknown' rather than
# defaulting to 'user' — silently promoting a new mechanism to valid route evidence is the failure this
# whole field exists to prevent.
function Get-Route {
    param([string] $Mechanism)
    $map = $script:Registry.routes
    if ($map -and $map.PSObject.Properties.Name -contains $Mechanism) { return $map.$Mechanism }
    return 'unknown'
}

function New-HelpResult {
    param($Spec = $null)
    $command = $null
    if ($Spec) {
        $command = [ordered]@{
            id          = [string]$Spec.id
            status      = [string]$Spec.status
            mutating    = [string]$Spec.mutating
            mechanism   = [string]$Spec.mechanism
            gates       = @($Spec.gates)
            description = [string]$Spec.description
        }
    }
    $kind = if ($Spec) { 'commandHelp' } else { 'globalHelp' }
    $usage = if ($Spec) { "aui $($Spec.id) [options]" } else { 'aui <domain> <verb> [args] [options]' }
    $message = if ($Spec) { "Help for '$($Spec.id)'. No command was executed." } else { 'AUI OpenVisual command help.' }
    $data = [ordered]@{
        kind             = $kind
        usage            = $usage
        helpFlags        = @('--help', '-h')
        commandDiscovery = 'aui catalog commands'
        command          = $command
    }
    return (New-Result -Ok $true -Code 'Ok' -Message $message -Verified $true -Data $data)
}

# ─────────────────────────────────────────────────────────────────────────────
# Problemer panel
#
# The panel is a findings LIST that the app keeps up to date in the background, which makes it unlike every
# other surface this driver reads: its content arrives asynchronously, so a read taken straight after a launch
# or an open is not wrong, it is EARLY. That is why `problems state` reports a readiness state and
# `problems wait` exists at all — a caller that asserts without waiting is asserting about the moment before
# the answer arrived.
#
# The panel publishes exactly what a driver needs, so none of this scrapes layout: the three counts carry their
# own AutomationIds, the state line carries one, the busy indicator carries one, and each row publishes its
# finding's code as its id and the whole row as its accessible name.
# ─────────────────────────────────────────────────────────────────────────────

function Get-ProblemsPanel {
    param($Window)
    return (Find-ByAutomationId $Window 'ProblemsPanel')
}

function Get-ProblemsCount {
    param($Panel, [string] $Tier)
    $el = Find-ByAutomationId $Panel "problems.count.$Tier"
    if (-not $el) { return $null }
    $text = [string]$el.Current.Name
    $value = 0
    if ([int]::TryParse($text, [ref] $value)) { return $value }
    return $null
}

# The four-state model, read off the two things the panel shows. "validating" is the one a caller must not
# assert through: it means no result is bound yet.
function Get-ProblemsState {
    param($Panel)

    $stateEl = Find-ByAutomationId $Panel 'ProblemsStateText'
    $stateText = if ($stateEl) { [string]$stateEl.Current.Name } else { '' }
    $stateShown = ($stateEl -and -not $stateEl.Current.IsOffscreen)

    $spinner = Find-ByAutomationId $Panel 'ProblemsSpinner'
    $busy = ($spinner -and -not $spinner.Current.IsOffscreen)

    # ORDER MATTERS. Validating wins over everything: it is the state that says "no result yet", and reporting
    # it as clean is exactly the lie the panel itself refuses to tell. Stale is next, because a stale panel is
    # showing a PREVIOUS result. Only then do the two up-to-date states divide.
    $state =
        if ($stateShown -and $stateText -eq 'Validerer projektet…') { 'validating' }
        elseif ($busy) { 'stale' }
        elseif ($stateShown -and $stateText -eq 'Ingen problemer fundet') { 'clean' }
        else { 'findings' }

    return [ordered]@{
        state          = $state
        stateText      = $stateText
        bound          = ($state -ne 'validating')
        staleIndicator = $busy
    }
}

function Get-ProblemsRowElements {
    param($Panel)
    $list = Find-ByAutomationId $Panel 'ProblemsList'
    if (-not $list) { return @() }
    $rows = @()
    foreach ($el in $list.FindAll($script:Desc, [System.Windows.Automation.Condition]::TrueCondition)) {
        $c = $null
        try { $c = $el.Current } catch { continue }   # an element torn down mid-walk is not a finding
        if (($c.ControlType.ProgrammaticName -replace '^ControlType\.', '') -ne 'ListItem') { continue }
        $rows += $el
    }
    return $rows
}

# A row's accessible name is "<Alvor>: <Besked> (<Element>)" — composed by the app precisely so one read
# answers what a row says. Splitting it here beats walking the cells: cell text is layout, the name is contract.
function ConvertTo-ProblemsRow {
    param($Element, [int] $Index, $Geometry)
    $c = $Element.Current
    $name = [string]$c.Name
    $severity = ''
    $message = $name
    $element = ''
    # The message is matched LAZILY and the element GREEDILY, so the split lands on the FIRST " (" rather than
    # the last. Element names contain parentheses of their own -- a terminal reads "Tryk (øverst venstre)" -- and
    # a greedy message would swallow "Ikke forbundet (Tryk" and leave "øverst venstre" as the element.
    if ($name -match '^(?<sev>[^:]+):\s*(?<msg>.*?)\s\((?<el>.*)\)$') {
        $severity = $Matches['sev']
        $message = $Matches['msg']
        $element = $Matches['el']
    }
    return [ordered]@{
        index    = $Index
        code     = [string]$c.AutomationId
        severity = $severity
        message  = $message
        element  = $element
        name     = $name
        rect     = ConvertTo-RectDump $c.BoundingRectangle $Geometry
    }
}

# Scroll the control under a point by whole wheel notches. Positive scrolls UP, negative DOWN.
#
# Built from the P/Invokes the native helper already exposes rather than as a new method on it: the compiled
# type is resolved once per process and a freshly added member was not visible to the running script, so the
# two-line composition here is what actually works from a cold invocation. SetCursorPos and mouse_event are
# both public on that type, so nothing is reached around.
#
# THE WHEEL, not Page Down, wherever this is used: paging a list moves its SELECTION, and in this app a
# selection is wired to navigation — a search scrolling past ninety rows would fire ninety navigations. A wheel
# scroll changes what is visible and nothing else.
function Invoke-WheelScroll {
    param([int] $X, [int] $Y, [int] $Notches)
    [Aui.Win32]::SetCursorPos($X, $Y) | Out-Null
    Start-Sleep -Milliseconds 30
    # A negative delta travels as an unsigned value; wrap it explicitly rather than casting a negative int.
    $delta = [uint32](([long]$Notches * 120 + 4294967296) % 4294967296)
    [Aui.Win32]::mouse_event(0x0800, 0, 0, $delta, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 30
}

# Find a row anywhere in the list, scrolling to reach it.
#
# The findings list VIRTUALIZES, so a row outside the viewport has no UIA element at all: it cannot be found,
# read or clicked until it is scrolled into view. At the panel's default height only a handful of rows exist at
# any moment, which without this would make every verb here reach the first few findings and nothing else --
# a driver that can only ever click the top of a 150-row list.
#
# Paged with the list's own ScrollPattern, i.e. the same movement a Page Down produces, and bounded so a list
# that refuses to move cannot spin.
function Find-ProblemsRowByCode {
    param($Panel, [string] $Code, [int] $MaxPages = 60)

    $list = Find-ByAutomationId $Panel 'ProblemsList'
    if (-not $list) { return $null }

    # The scrollable is the list's own ScrollViewer, not the list: Avalonia gives the ListBox peer no Scroll
    # pattern and puts it on the viewer inside the control template. Looking only at the list found nothing, and
    # the search then "scrolled" a whole 150-row list without moving a pixel — reporting, perfectly politely,
    # that a row present in the oracle did not exist.
    $scroll = Get-Pattern $list ([System.Windows.Automation.ScrollPattern]::Pattern)
    if (-not $scroll) {
        foreach ($el in $list.FindAll($script:Desc, [System.Windows.Automation.Condition]::TrueCondition)) {
            $candidate = Get-Pattern $el ([System.Windows.Automation.ScrollPattern]::Pattern)
            if ($candidate -and $candidate.Current.VerticallyScrollable) { $scroll = $candidate; break }
        }
    }
    # Where to put the cursor for a wheel scroll: the middle of the list.
    $listRect = $list.Current.BoundingRectangle
    $wheelX = [int]($listRect.X + $listRect.Width / 2)
    $wheelY = [int]($listRect.Y + $listRect.Height / 2)

    # START FROM THE TOP, always. The scroll offset survives between commands, so a search that began wherever
    # the previous one stopped could only ever find rows BELOW that point — and would report a row that plainly
    # exists as missing, depending on what ran before it. That is precisely how this failed the first time.
    if ($scroll -and $scroll.Current.VerticallyScrollable) {
        try { $scroll.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, 0) } catch { }
        Start-Sleep -Milliseconds 120
    } elseif ($listRect.Width -gt 0) {
        for ($up = 0; $up -lt $MaxPages; $up++) {
            $seen = @(Get-ProblemsRowElements $Panel | ForEach-Object { [string]$_.Current.AutomationId })
            Invoke-WheelScroll $wheelX $wheelY 5
            Start-Sleep -Milliseconds 100
            $now = @(Get-ProblemsRowElements $Panel | ForEach-Object { [string]$_.Current.AutomationId })
            if (($now -join '|') -eq ($seen -join '|')) { break }
        }
    }

    for ($page = 0; $page -le $MaxPages; $page++) {
        foreach ($el in Get-ProblemsRowElements $Panel) {
            $c = $null
            try { $c = $el.Current } catch { continue }
            if ([string]$c.AutomationId -eq $Code -or [string]$c.Name -like "*$Code*") { return $el }
        }

        if ($scroll -and $scroll.Current.VerticallyScrollable) {
            if ($scroll.Current.VerticalScrollPercent -ge 100) { break }
            $scroll.ScrollVertical([System.Windows.Automation.ScrollAmount]::LargeIncrement)
            Start-Sleep -Milliseconds 120
            continue
        }

        # NO SCROLL PATTERN ANYWHERE. Avalonia gives neither the ListBox peer nor its inner ScrollViewer one, so
        # the programmatic route does not exist here and the wheel is the remaining honest one.
        #
        # The WHEEL rather than Page Down, and the distinction is load-bearing: paging a list moves its
        # SELECTION, the panel navigates from its selection, and a search scrolling past ninety findings would
        # therefore fire ninety navigations and leave the tree wherever the last one landed. One of this
        # feature's own tests asserts a selection is UNCHANGED across a search; with Page Down it could not be
        # written truthfully. A wheel scroll changes what is visible and nothing else.
        $before = @(Get-ProblemsRowElements $Panel | ForEach-Object { [string]$_.Current.AutomationId })
        if ($before.Count -eq 0) { break }
        Invoke-WheelScroll $wheelX $wheelY -3
        Start-Sleep -Milliseconds 120
        $after = @(Get-ProblemsRowElements $Panel | ForEach-Object { [string]$_.Current.AutomationId })
        # A list that will not move any further has nothing left to show.
        if (($after -join '|') -eq ($before -join '|')) { break }
    }
    return $null
}

function Invoke-Mechanism-ProblemsState {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }

    $wait = [bool](Test-SpecFlag $Opts 'wait')
    $timeout = Get-OptInt $Opts @('timeout') 15000
    if (-not $timeout.ok) { return (New-Result -Ok $false -Code 'InvalidInput' -Message $timeout.message) }
    $timeoutMs = $timeout.value
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)

    while ($true) {
        $panel = Get-ProblemsPanel $Window
        if (-not $panel) {
            return (New-Result -Ok $false -Code 'ControlNotFound' `
                -Message 'Problemer panel not found. It is hidden while Vis > Problemer is off -- turn it back on with view.problems.toggle.' `
                -Context (Get-Context $Window))
        }

        $visible = -not $panel.Current.IsOffscreen
        $state = Get-ProblemsState $panel
        $rows = @(Get-ProblemsRowElements $panel)

        $data = [ordered]@{
            visible        = $visible
            state          = $state.state
            stateText      = $state.stateText
            bound          = $state.bound
            staleIndicator = $state.staleIndicator
            errors         = Get-ProblemsCount $panel 'error'
            warnings       = Get-ProblemsCount $panel 'warning'
            infos          = Get-ProblemsCount $panel 'info'
            visibleRows    = $rows.Count
        }

        if (-not $wait -or $state.bound) {
            $msg = "Problemer: $($state.state), $($data.errors) fejl / $($data.warnings) advarsler / $($data.infos) oplysninger."
            return (New-Result -Ok $true -Code 'Ok' -Message $msg -Verified $true `
                -Context (Get-Context $Window) -Data $data)
        }

        if ((Get-Date) -gt $deadline) {
            return (New-Result -Ok $false -Code 'Timeout' `
                -Message "Still validating after ${timeoutMs}ms -- no result has been bound yet." `
                -Context (Get-Context $Window) -Data $data)
        }
        Start-Sleep -Milliseconds 150
    }
}

function Invoke-Mechanism-ProblemsRows {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }

    $panel = Get-ProblemsPanel $Window
    if (-not $panel) {
        return (New-Result -Ok $false -Code 'ControlNotFound' `
            -Message 'Problemer panel not found (Vis > Problemer is off).' -Context (Get-Context $Window))
    }

    $geometry = $null
    $r = $panel.Current.BoundingRectangle
    if (-not ([double]::IsInfinity($r.X) -or [double]::IsNaN($r.X))) {
        $geometry = Get-MonitorGeometry -X ([int]$r.X) -Y ([int]$r.Y)
    }

    $elements = @(Get-ProblemsRowElements $panel)
    $rows = @()
    for ($i = 0; $i -lt $elements.Count; $i++) {
        $rows += ConvertTo-ProblemsRow $elements[$i] $i $geometry
    }

    $state = Get-ProblemsState $panel
    # The list VIRTUALIZES, so this is what is realized, not necessarily every finding. Saying so in the
    # envelope keeps a caller from reading a short list as a small result.
    $data = [ordered]@{
        rowCount     = $rows.Count
        virtualized  = $true
        bound        = $state.bound
        state        = $state.state
        rows         = @($rows)
    }
    return (New-Result -Ok $true -Code 'Ok' -Message "Problemer: $($rows.Count) realized rows." `
        -Verified $true -Context (Get-Context $Window) -Data $data)
}

function Invoke-Mechanism-ProblemsClick {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }
    # Assert-Foreground returns $NULL on success and the failure result otherwise — so it is returned, never
    # tested as a boolean. Read as one, `-not $null` is true and the verb reports "denied" in exactly the case
    # where the foreground WAS acquired: the click then never fired, and the refusal named an environment
    # problem that did not exist.
    $denied = Assert-Foreground $Window
    if ($denied) { return $denied }

    $panel = Get-ProblemsPanel $Window
    if (-not $panel) {
        return (New-Result -Ok $false -Code 'ControlNotFound' `
            -Message 'Problemer panel not found (Vis > Problemer is off).' -Context (Get-Context $Window))
    }

    $elements = @(Get-ProblemsRowElements $panel)
    if ($elements.Count -eq 0) {
        return (New-Result -Ok $false -Code 'ControlNotFound' `
            -Message 'No rows are realized in the Problemer panel.' -Context (Get-Context $Window))
    }

    $rowOpt = Get-OptValue $Opts @('row') -NamedOnly
    if (-not $rowOpt) {
        return (New-Result -Ok $false -Code 'InvalidInput' -Message 'Pass --row <index|text>.')
    }

    $target = $null
    $index = 0
    if ([int]::TryParse($rowOpt, [ref] $index)) {
        if ($index -lt 0 -or $index -ge $elements.Count) {
            return (New-Result -Ok $false -Code 'InvalidInput' `
                -Message "Row $index is out of range (0..$($elements.Count - 1) are realized).")
        }
        $target = $elements[$index]
    } else {
        # By code or text, SCROLLING to reach it: the list virtualizes, so a row further down does not exist as
        # an element until the viewport reaches it. An index addresses realized rows only, which is why a caller
        # wanting a specific finding should name its code rather than guess a position.
        $target = Find-ProblemsRowByCode $panel $rowOpt
        if (-not $target) {
            return (New-Result -Ok $false -Code 'ControlNotFound' `
                -Message "No row matches '$rowOpt' by code or by text, after scrolling the whole list.")
        }
    }

    # Bring it fully into view before measuring: a row the scroll left straddling the viewport edge has bounds
    # whose midpoint is outside the list, and the click would land on whatever is there instead.
    Show-ScrollableItem $target

    # A POINTER click, not a selection call: the panel navigates from the selection a click produces, and
    # setting the selection directly would reach the outcome by a path no user can take -- which would make
    # the transcript evidence of something other than the gesture under test.
    $rect = $target.Current.BoundingRectangle
    if ([double]::IsInfinity($rect.X) -or [double]::IsNaN($rect.X) -or $rect.Width -le 0) {
        return (New-Result -Ok $false -Code 'ControlNotFound' -Message 'The row has no clickable bounds.')
    }
    $x = [int]($rect.X + [Math]::Min(60, $rect.Width / 2))
    $y = [int]($rect.Y + $rect.Height / 2)
    [Aui.Win32]::Click($x, $y)
    Start-Sleep -Milliseconds 250

    $data = [ordered]@{
        clicked = ConvertTo-ProblemsRow $target 0 $null
        point   = [ordered]@{ x = $x; y = $y }
    }
    return (New-Result -Ok $true -Code 'Ok' -Message "Clicked Problemer row '$($target.Current.AutomationId)'." `
        -Verified $true -Context (Get-Context $Window) -Data $data)
}

function Invoke-Mechanism-ProblemsToggle {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }

    $tier = Get-OptValue $Opts @('tier') -NamedOnly
    if ($tier -notin @('error', 'warning', 'info')) {
        return (New-Result -Ok $false -Code 'InvalidInput' -Message 'Pass --tier <error|warning|info>.')
    }

    $panel = Get-ProblemsPanel $Window
    if (-not $panel) {
        return (New-Result -Ok $false -Code 'ControlNotFound' `
            -Message 'Problemer panel not found (Vis > Problemer is off).' -Context (Get-Context $Window))
    }
    $button = Find-ByAutomationId $panel "problems.filter.$tier"
    if (-not $button) {
        return (New-Result -Ok $false -Code 'ControlNotFound' -Message "Filter toggle for '$tier' not found.")
    }

    # Through the Toggle PATTERN, which is the same door a keyboard or a screen-reader user goes through.
    $pattern = Get-Pattern $button ([System.Windows.Automation.TogglePattern]::Pattern)
    if (-not $pattern) {
        return (New-Result -Ok $false -Code 'PatternUnavailable' `
            -Message "The '$tier' toggle exposes no Toggle pattern.")
    }
    $before = $pattern.Current.ToggleState
    $pattern.Toggle()
    Start-Sleep -Milliseconds 200
    $after = (Get-Pattern $button ([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState

    $rows = @(Get-ProblemsRowElements $panel).Count
    $data = [ordered]@{ tier = $tier; before = "$before"; after = "$after"; visibleRows = $rows }
    return (New-Result -Ok $true -Code 'Ok' -Message "Tier '$tier': $before -> $after ($rows rows shown)." `
        -Verified ($before -ne $after) -Context (Get-Context $Window) -Data $data)
}

function Invoke-Mechanism-ProblemsSort {
    param($Spec, $Opts, $Window)
    if (-not $Window) { return (New-Result -Ok $false -Code 'AppNotRunning' -Message 'App not running.') }

    $column = Get-OptValue $Opts @('column') -NamedOnly
    if ($column -notin @('severity', 'code', 'message', 'element', 'category')) {
        return (New-Result -Ok $false -Code 'InvalidInput' `
            -Message 'Pass --column <severity|code|message|element|category>.')
    }

    $panel = Get-ProblemsPanel $Window
    if (-not $panel) {
        return (New-Result -Ok $false -Code 'ControlNotFound' `
            -Message 'Problemer panel not found (Vis > Problemer is off).' -Context (Get-Context $Window))
    }
    $header = Find-ByAutomationId $panel "problems.sort.$column"
    if (-not $header) {
        return (New-Result -Ok $false -Code 'ControlNotFound' -Message "Sort header for '$column' not found.")
    }

    $before = @(Get-ProblemsRowElements $panel | ForEach-Object { [string]$_.Current.AutomationId })
    $invoke = Get-Pattern $header ([System.Windows.Automation.InvokePattern]::Pattern)
    if (-not $invoke) {
        return (New-Result -Ok $false -Code 'PatternUnavailable' -Message "The '$column' header exposes no Invoke pattern.")
    }
    $invoke.Invoke()
    Start-Sleep -Milliseconds 250
    $after = @(Get-ProblemsRowElements $panel | ForEach-Object { [string]$_.Current.AutomationId })

    $data = [ordered]@{
        column   = $column
        before   = @($before)
        after    = @($after)
        reordered = (($before -join '|') -ne ($after -join '|'))
    }
    return (New-Result -Ok $true -Code 'Ok' -Message "Sorted Problemer by '$column'." `
        -Verified $true -Context (Get-Context $Window) -Data $data)
}

function Invoke-Command-Spec {
    param($Spec, $Opts, $Window)
    switch ($Spec.mechanism) {
        'passive'        { Invoke-Mechanism-Passive        $Spec $Opts $Window }
        'static'         { Invoke-Mechanism-Static         $Spec $Opts $Window }
        'invoke'         { Invoke-Mechanism-Invoke         $Spec $Opts $Window }
        'key'            { Invoke-Mechanism-Key            $Spec $Opts $Window }
        'treeSelect'     { Invoke-Mechanism-TreeSelect     $Spec $Opts $Window }
        'treeDump'       { Invoke-Mechanism-TreeDump       $Spec $Opts $Window }
        'expandCollapse' { Invoke-Mechanism-ExpandCollapse $Spec $Opts $Window }
        'doubleClick'    { Invoke-Mechanism-DoubleClick    $Spec $Opts $Window }
        'rightClick'     { Invoke-Mechanism-RightClick     $Spec $Opts $Window }
        'nodeDrag'       { Invoke-Mechanism-NodeDrag       $Spec $Opts $Window }
        'readProperty'   { Invoke-Mechanism-ReadProperty   $Spec $Opts $Window }
        'keySend'        { Invoke-Mechanism-KeySend        $Spec $Opts $Window }
        'fileDialog'     { Invoke-Mechanism-FileDialog     $Spec $Opts $Window }
        'contextMenu'    { Invoke-Mechanism-ContextMenu    $Spec $Opts $Window }
        'contextMenuDump' { Invoke-Mechanism-ContextMenuDump $Spec $Opts $Window }
        'menuBarDump'    { Invoke-Mechanism-MenuBarDump    $Spec $Opts $Window }
        'toolbarDump'    { Invoke-Mechanism-ToolbarDump    $Spec $Opts $Window }
        'menu'           { Invoke-Mechanism-Menu           $Spec $Opts $Window }
        'capture'        { Invoke-Mechanism-Capture        $Spec $Opts $Window }
        'dialogCancel'   { Invoke-Mechanism-DialogCancel   $Spec $Opts $Window }
        'dialogButton'   { Invoke-Mechanism-DialogButton   $Spec $Opts $Window }
        'dialogSetText'  { Invoke-Mechanism-DialogSetText  $Spec $Opts $Window }
        'dialogSetCheck' { Invoke-Mechanism-DialogSetCheck $Spec $Opts $Window }
        'dialogSelectItem' { Invoke-Mechanism-DialogSelectItem $Spec $Opts $Window }
        'dialogClickRow' { Invoke-Mechanism-DialogClickRow $Spec $Opts $Window }
        'dialogRead'     { Invoke-Mechanism-DialogRead     $Spec $Opts $Window }
        'problemsState'  { Invoke-Mechanism-ProblemsState  $Spec $Opts $Window }
        'problemsRows'   { Invoke-Mechanism-ProblemsRows   $Spec $Opts $Window }
        'problemsClick'  { Invoke-Mechanism-ProblemsClick  $Spec $Opts $Window }
        'problemsToggle' { Invoke-Mechanism-ProblemsToggle $Spec $Opts $Window }
        'problemsSort'   { Invoke-Mechanism-ProblemsSort   $Spec $Opts $Window }
        'notImplemented' { Invoke-Mechanism-NotImplemented $Spec $Opts $Window }
        default          { Invoke-Mechanism-NotImplemented $Spec $Opts $Window }
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# Main
# ─────────────────────────────────────────────────────────────────────────────
Assert-Windows

$tokens = @($CmdArgs)
if ($tokens.Count -eq 0) {
    Write-Result (New-Result -Ok $false -Code 'InvalidInput' -Message 'Usage: aui <domain> <verb> [args]. Try: aui catalog commands')
}
$helpRequested = Test-HelpRequested $tokens
$commandTokens = @($tokens | Where-Object { -not (Test-HelpToken $_) })
if ($helpRequested -and $commandTokens.Count -eq 0) {
    Write-Result (New-HelpResult)
}

# Resolve the command id from the leading non-flag words. The reference CLI expresses multi-segment
# ids in mixed styles — nested words (`project recent list` → project.recent.list), kebab verbs
# (`view toolbar-toggle` → view.toolbar.toggle), and kebab→camel (`node get-properties` →
# node.getProperties, `project save-as` → project.saveAs). Resolve by trying, longest-prefix first,
# each of those normalizations against the registry so the same invocations work here.
# In a try because ConvertFrom-Json THROWS on a malformed registry (with $ErrorActionPreference='Stop'),
# and an escaping exception here prints a PowerShell error record with NO JSON — breaking the "exactly one
# envelope per invocation" contract on the very failure a scripted caller most needs to read. The
# not-found case already answered in JSON; the unparseable case did not.
try { Import-Registry } catch {
    Write-Result (New-Result -Ok $false -Code 'InvalidInput' `
        -Message "Could not read the command registry at $($script:RegistryPath): $($_.Exception.Message)")
}

function ConvertTo-Camel { param([string] $S)
    $parts = $S -split '-'
    if ($parts.Count -le 1) { return $S }
    $head = $parts[0]
    $tail = $parts[1..($parts.Count - 1)] | ForEach-Object { if ($_.Length -gt 0) { $_.Substring(0,1).ToUpper() + $_.Substring(1) } }
    return ($head + (($tail) -join ''))
}

function Resolve-Command {
    param([string[]] $Tokens)
    $words = @(); foreach ($t in $Tokens) { if ($t -like '--*') { break }; $words += $t }
    $ids = @($script:Registry.commands | ForEach-Object { $_.id })
    $maxK = [Math]::Min(3, $words.Count)
    for ($k = $maxK; $k -ge 1; $k--) {
        $seg = @($words[0..($k - 1)])
        $variants = @(
            ($seg -join '.'),
            (($seg -join '.') -replace '-', '.'),
            (@($seg | ForEach-Object { ConvertTo-Camel $_ }) -join '.')
        )
        foreach ($v in ($variants | Select-Object -Unique)) {
            $match = $ids | Where-Object { $_ -ieq $v } | Select-Object -First 1
            if ($match) {
                $rest = @(); if ($Tokens.Count -gt $k) { $rest = @($Tokens[$k..($Tokens.Count - 1)]) }
                return @{ id = $match; rest = $rest }
            }
        }
    }
    return $null
}

$resolved = Resolve-Command $commandTokens
if (-not $resolved) {
    Write-Result (New-Result -Ok $false -Code 'InvalidInput' -Message "Unknown command '$($commandTokens -join ' ')'. Run 'aui catalog commands' to list the vocabulary.")
}
$id = $resolved.id
$spec = Find-Spec $id
# Help must terminate before gates, UIA bootstrap, app lookup, and mechanism dispatch. Otherwise a
# documentation probe can execute the command it is asking about (capture.window did exactly that).
if ($helpRequested) {
    Write-Result (New-HelpResult $spec)
}
$opts = Parse-Options @($resolved.rest)

# Gate 1: planned commands are stubs — require --allow-unverified to even attempt them.
# (partial commands run freely; only planned is blocked here.)
if ($spec.status -eq 'planned' -and -not ($opts.ContainsKey('allow-unverified'))) {
    Write-Result (New-Result -Ok $false -Code 'Unverified' `
        -Message "Command '$id' is status=planned; re-run with --allow-unverified to attempt it.")
}

# Gate 2: destructive/caution commands refuse unless the matching confirmation flag is passed.
if (($spec.gates -contains 'confirmDestructive') -and -not ($opts.ContainsKey('confirm-destructive'))) {
    Write-Result (New-Result -Ok $false -Code 'ConfirmationRequired' `
        -Message "Command '$id' is destructive; re-run with --confirm-destructive to proceed.")
}
if (($spec.gates -contains 'confirmCaution') -and -not ($opts.ContainsKey('confirm-caution'))) {
    Write-Result (New-Result -Ok $false -Code 'ConfirmationRequired' `
        -Message "Command '$id' is caution-gated; re-run with --confirm-caution to proceed.")
}

# Any escaping exception would otherwise surface as a PowerShell error record with NO JSON on
# stdout, breaking the "exactly one JSON envelope per invocation" contract that callers (and the
# comparison transcripts) depend on. Convert it into a proper envelope instead.
#
# BOOTSTRAP IS INSIDE THE TRY, not above it. Add-Type failing on a host without the UI-Automation
# assemblies, and any UIA fault while resolving the main window, are exactly the machine-level failures a
# caller cannot diagnose from an exit code alone — and they were the two steps still outside the guarantee.
# Write-Result's `exit` is engine flow control rather than a catchable exception (verified), so a command
# that reports from inside here still terminates rather than being swallowed as an error.
$window = $null
try {
    Initialize-Uia
    $launch = $opts.ContainsKey('launch')
    # --path alongside --launch names the project the app should COME UP ON. Only meaningful while launching;
    # every other verb reads --path for its own purpose, and passing it here changes nothing for them.
    $launchPath = if ($launch) { [string](Get-OptValue $opts @('path') -NamedOnly) } else { $null }
    $window = Resolve-MainWindow -Launch:$launch -ProjectPath $launchPath
    # Publish it so "is this the main window?" is answerable by identity anywhere below (Get-OpenModalWindow,
    # Get-MenuBarRootKeySet) without re-resolving it or guessing from a title.
    $script:MainWindow = $window
    $result = Invoke-Command-Spec $spec $opts $window
} catch {
    $result = New-Result -Ok $false -Code 'MutationFailed' `
        -Message "Unhandled error in '$id': $($_.Exception.Message)" `
        -Context $(try { if ($window) { Get-Context $window } else { $null } } catch { $null })
}
Write-Result $result
