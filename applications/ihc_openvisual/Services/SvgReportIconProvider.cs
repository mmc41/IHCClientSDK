using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Ihc.Vis;

namespace ihc_openvisual.Services;

/// <summary>
/// OpenVisual's report icon mapping (spec R11/D5): serves the SAME <c>Assets/*.svg</c> glyphs the app ships,
/// read as plain embedded resources so the provider works without a running Avalonia platform (S09 — usable
/// from headless tests and report generation alike). For HTML it returns per-instance
/// <c>&lt;svg class="icon icon-&lt;key&gt;"&gt;&lt;use …/&gt;&lt;/svg&gt;</c> fragments plus the
/// once-per-document inline <c>&lt;symbol&gt;</c> sprite; for text it returns null so generation falls back
/// to the default unicode stand-ins. A key with no matching asset also returns null (same fallback rule).
/// </summary>
public sealed class SvgReportIconProvider : IReportIconProvider
{
    /// <summary>The banner logo's semantic key; its asset is <c>openvisual-logo.svg</c>.</summary>
    private const string LogoKey = "logo";

    // The sprite's fixed symbol order (provider-owned per R11; the HTML report oracles pin it): the icon
    // keys in canonical order with the logo LAST. Keys requested but not listed are appended (sorted)
    // before the logo so the block stays deterministic for foreign keys too.
    private static readonly string[] CanonicalOrder =
    {
        "command", "command-group", "condition", "cond-and", "cond-or", "event", "event-group",
        "pin-in", "pin-out", "scenario",
        "section-input", "section-output", "section-settings", "section-internal-vars",
        "prog-program", "prog-subprogram",
        "var-timer", "var-timer-duration", "var-enum", "var-time", "var-flag", "var-counter",
        "var-light-level", "var-weekday", "var-integer", "var-date", "var-decimal", "var-temperature",
        "var-illuminance", "var-humidity", "var-holiday", "var-energy",
        "locality", "fb-lk", "fb-editable", "link-from", "link-to",
        "product-button", "product-lamp", "product-socket", "product-sensor", "product-s0", "rs485-module",
    };

    // Attributes never copied from an asset's root <svg> onto its <symbol>: the sprite carries its own
    // xmlns, and per-use accessibility lives on the <use> site (aria-hidden), not the definition.
    private static readonly Regex DroppedAttributes = new("^(xmlns(:.*)?|role|aria-.*)$", RegexOptions.Compiled);

    private static readonly Regex Attribute = new(@"([a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*""([^""]*)""", RegexOptions.Compiled);

    public string? GetFragment(string mimeType, string iconKey) =>
        mimeType == ReportMimeTypes.Html && Assets.ContainsKey(AssetStem(iconKey))
            ? $"""<svg class="icon icon-{iconKey}" aria-hidden="true"><use href="#icon-{iconKey}"/></svg>"""
            : null;

    public string? GetDefinitionsBlock(string mimeType, IReadOnlyCollection<string> iconKeys)
    {
        string? block = null;
        if (mimeType == ReportMimeTypes.Html)
        {
            var symbols = OrderCanonically(iconKeys)
                .Select(key => (Key: key, Asset: ReadAsset(key)))
                .Where(entry => entry.Asset is not null)
                .Select(entry => Symbol(entry.Key, entry.Asset!))
                .ToList();
            if (symbols.Count > 0)
            {
                block = """<svg xmlns="http://www.w3.org/2000/svg" style="display:none">"""
                    + string.Concat(symbols) + "</svg>";
            }
        }
        return block;
    }

    // Requested keys in the provider's fixed sprite order: canonical list order, unknown keys sorted in
    // between, logo always last.
    private static IEnumerable<string> OrderCanonically(IReadOnlyCollection<string> iconKeys)
    {
        var requested = new HashSet<string>(iconKeys, StringComparer.Ordinal);
        foreach (string key in CanonicalOrder.Where(requested.Contains))
        {
            yield return key;
        }
        foreach (string key in requested
            .Where(k => k != LogoKey && !CanonicalOrder.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal))
        {
            yield return key;
        }
        if (requested.Contains(LogoKey))
        {
            yield return LogoKey;
        }
    }

    // One <symbol id="icon-<key>" …>…</symbol>: the asset root's attributes minus the dropped set, in asset
    // order with whitespace collapsed, then the root's inner content verbatim but trimmed at both ends.
    private static string Symbol(string key, string asset)
    {
        int open = asset.IndexOf("<svg", StringComparison.Ordinal);
        int openEnd = asset.IndexOf('>', open);
        int close = asset.LastIndexOf("</svg>", StringComparison.Ordinal);
        string rootTag = asset.Substring(open, openEnd - open + 1);
        string inner = asset.Substring(openEnd + 1, close - openEnd - 1).Trim();

        var symbol = new StringBuilder($"<symbol id=\"icon-{key}\"");
        foreach (Match attribute in Attribute.Matches(rootTag))
        {
            string name = attribute.Groups[1].Value;
            if (!DroppedAttributes.IsMatch(name))
            {
                // Invariant: this is SVG markup for the report, not display text. Both holes are strings
                // taken straight off the regex match, so the provider changes nothing TODAY — it is stated so
                // that a value formatted here later cannot pick up a locale's decimal separator and write an
                // attribute no SVG parser accepts.
                symbol.Append(CultureInfo.InvariantCulture, $" {name}=\"{attribute.Groups[2].Value}\"");
            }
        }
        return symbol.Append('>').Append(inner).Append("</symbol>").ToString();
    }

    // Every shipped Assets/*.svg, read ONCE at type init and keyed by file stem. A single report references
    // the same handful of icons hundreds of times (1099 instances over 28 distinct keys in the largest
    // fixture), so a manifest scan plus a stream read per instance dominated generation; the whole asset set
    // is ~20 KB, so holding it costs less than one such scan.
    private static readonly FrozenDictionary<string, string> Assets = LoadAssets();

    private static FrozenDictionary<string, string> LoadAssets()
    {
        const string marker = ".Assets.";
        const string extension = ".svg";
        Assembly assembly = typeof(SvgReportIconProvider).Assembly;
        var assets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in assembly.GetManifestResourceNames())
        {
            int start = name.LastIndexOf(marker, StringComparison.Ordinal);
            if (start >= 0 && name.EndsWith(extension, StringComparison.Ordinal))
            {
                using Stream stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                // Reports emit LF-only bytes (spec S06); an asset checked out with CRLF must not leak \r
                // into the sprite, so line endings normalize here regardless of checkout/embedding EOLs.
                assets[name[(start + marker.Length)..^extension.Length]] = reader.ReadToEnd().Replace("\r\n", "\n");
            }
        }
        return assets.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>The asset file stem a key maps to: "logo" → <c>openvisual-logo</c>, otherwise the key itself.</summary>
    private static string AssetStem(string iconKey) => iconKey == LogoKey ? "openvisual-logo" : iconKey;

    // The embedded asset for a key, or null when the key has no shipped glyph — the caller then falls back
    // per the R11 rule.
    private static string? ReadAsset(string iconKey) =>
        Assets.TryGetValue(AssetStem(iconKey), out string? content) ? content : null;
}
