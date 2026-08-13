using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ihc_openvisual.Controls;

/// <summary>
/// The column widths of the product dialog's pseudo-tables, each written ONCE and handed to every grid that
/// draws that table — the header and each realized row alike.
///
/// <para>A header authored outside the list and rows realized inside it are separate <c>Grid</c>s, so they
/// only line up while their column specs agree. Kept as literals in the markup, that is three copies of the
/// terminal spec and two of the settings spec with nothing to hold them in step, and a drift shows up as
/// headers standing over the wrong columns.</para>
///
/// <para><b>Why a markup extension and not a resource.</b> The obvious answers are all closed off:
/// <c>ColumnDefinitions</c> is an OWNED collection — Avalonia's definition list "moves a definition from its
/// current parent tree" on assignment — so one shared instance out of a <c>ResourceDictionary</c> or a style
/// <c>Setter</c> would be re-parented by every grid that used it, and since the row template is instantiated
/// once per row, each new row would steal the columns from the row before it. A <c>string</c> resource is not
/// converted on its way into the property either. An extension is evaluated per instantiation, so every grid
/// gets its OWN definitions parsed from the one spec, which is exactly the sharing wanted and none of the
/// sharing that breaks.</para>
///
/// <para>It also cannot live on a view-model: <c>ColumnDefinitions</c> is an Avalonia type and view-models are
/// barred from depending on Avalonia (arch-enforced). This is view-layer presentation policy, and it lives in
/// the view layer.</para>
/// </summary>
public sealed class TerminalColumnsExtension : MarkupExtension
{
    /// <summary>Navn | Adresse | Ledningsfarve | Note (US-012).</summary>
    public const string Spec = "1.4*,1.3*,*,1.6*";

    public override object ProvideValue(IServiceProvider serviceProvider) => ColumnDefinitions.Parse(Spec);
}

/// <summary>The settings grid's three columns, on the same terms as <see cref="TerminalColumnsExtension"/>.</summary>
public sealed class SettingColumnsExtension : MarkupExtension
{
    /// <summary>Navn | Note | Værdi (T070).</summary>
    public const string Spec = "1.6*,2*,*";

    public override object ProvideValue(IServiceProvider serviceProvider) => ColumnDefinitions.Parse(Spec);
}
