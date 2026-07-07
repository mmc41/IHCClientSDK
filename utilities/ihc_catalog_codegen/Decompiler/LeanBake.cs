#nullable enable
using Ihc.Vis.Schema;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// The catalog-vs-project lean-reconstruction rule shared by <see cref="ProductDecompiler"/> and
    /// <see cref="FunctionBlockDecompiler"/>: decides whether a resource attribute must be baked into the lean body or
    /// can be dropped as a DTD default. Keeping it in one place is load-bearing — the product and function-block
    /// decompilers must apply the identical rule, or one family would bake attributes the other would not and the
    /// self-verify against the source file would diverge between them.
    /// </summary>
    internal static class LeanBake
    {
        // Whether to bake an attribute into the lean body. An attribute is safe to OMIT only when the canonicalizer
        // would drop it under BOTH the source (catalog) grammar AND the project registry grammar — i.e. its value
        // equals the declared default in each. This is the catalog-vs-project DTD-default bake (B1c): a value that
        // equals the catalog default but differs from the registry default (e.g. a family's `locked`/`enduser_report`,
        // `dimmer_setting_load_mode`) must still be baked, or on insert the registry default would silently override it
        // and change the placed instance. Baking a value that equals the catalog default is harmless — it drops again
        // under catalog-grammar canonicalization, so the self-verify against the source file stays green.
        public static bool ShouldEmit(ElementSchema? catalogSchema, string tag, string name, string value)
        {
            if (!IsDroppableUnder(catalogSchema, name, value))
            {
                return true;   // #REQUIRED / #IMPLIED / undeclared / value ≠ catalog default
            }
            ElementSchema? registrySchema = ProjectSchemaView.RegistryOnly.TryGet(tag);
            if (registrySchema is null)
            {
                return false;   // open-world tag: its captured inline-DTD block (== catalog grammar) governs on insert
            }
            return !IsDroppableUnder(registrySchema, name, value);   // bake when the registry default differs
        }

        public static bool IsDroppableUnder(ElementSchema? schema, string name, string value)
        {
            AttrSchema? attr = schema?.FindAttr(name);
            return attr is not null && attr.Kind == AttrKind.Defaulted && value == attr.Default;
        }
    }
}
