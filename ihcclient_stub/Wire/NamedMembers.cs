using System.Reflection;

namespace Ihc.Stubbing
{
    /// <summary>
    /// Maps a generated wire type and an SDK model whose member names follow one rule: the wire name is the model's,
    /// ignoring case, behind a prefix some services put on every member. Case is ignored whole, not only in the
    /// first letter, because some wire names are a constant's upper-case name with only that letter lowered
    /// (<c>rUNNING</c> for <c>Running</c>). A type whose names
    /// follow the rule needs no mapping of its own, and the rule is a reading of the wire independent of the
    /// SDK's member-by-member mappers, which the round trips check it against.
    /// </summary>
    internal static class NamedMembers
    {
        internal static TWire ToWire<TWire>(object model, string prefix = "") where TWire : new()
        {
            var wire = new TWire();
            foreach ((PropertyInfo wireMember, PropertyInfo modelMember) in Pairs(typeof(TWire), model.GetType(), prefix))
            {
                wireMember.SetValue(wire, modelMember.GetValue(model));
            }
            return wire;
        }

        internal static TModel FromWire<TModel>(object wire, string prefix = "") where TModel : new()
        {
            var model = new TModel();
            foreach ((PropertyInfo wireMember, PropertyInfo modelMember) in Pairs(wire.GetType(), typeof(TModel), prefix))
            {
                modelMember.SetValue(model, wireMember.GetValue(wire));
            }
            return model;
        }

        /// <summary>Every wire member with the model member it names. A wire member that names none is a stub
        /// defect, never a member quietly left unmapped.</summary>
        private static IEnumerable<(PropertyInfo Wire, PropertyInfo Model)> Pairs(Type wire, Type model, string prefix) =>
            wire.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(wireMember =>
                (wireMember, model.GetProperty(ModelName(wireMember.Name, prefix), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?? throw new InvalidOperationException($"{model.FullName} has no member for {wire.FullName}.{wireMember.Name}")));

        private static string ModelName(string wireName, string prefix)
        {
            if (!wireName.StartsWith(prefix, StringComparison.Ordinal) || wireName.Length == prefix.Length)
            {
                throw new InvalidOperationException($"{wireName} does not follow the {prefix} naming rule");
            }
            return wireName[prefix.Length..];
        }
    }
}
