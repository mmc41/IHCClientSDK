using System;
using System.Linq;

using ihc_openvisual.Services;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests;

/// <summary>
/// RF Tier-4: an OMITTED field constraint is unconstrained, not silently stricter than unconstrained.
///
/// <para><see cref="FieldConstraintMetadata"/> is a record STRUCT, so a dialog DTO that declares
/// <c>FieldConstraintMetadata Level = default</c> gets the all-zero value — and one of its members did not read
/// the same as <see cref="FieldConstraintMetadata.Unconstrained"/> at zero. <c>WhitespaceAllowed</c> was
/// <see langword="true"/> when unconstrained and <see langword="false"/> at <c>default</c>, so a window that
/// simply did not pass a constraint was told the field forbids whitespace — a rule nothing declared, applied to
/// the fields nobody had constrained.</para>
///
/// <para>The fix is on the TYPE rather than on each call site: a default parameter must be a compile-time
/// constant, so <c>= FieldConstraintMetadata.Unconstrained</c> cannot be written even where the omission is
/// visible. Making the struct's own zero mean "no constraint" fixes every omission at once, including ones that
/// have not been written yet.</para>
/// </summary>
public class OmittedConstraintTests
{
    [Test]
    public void TheStructsDefaultIsExactlyTheUnconstrainedValue()
    {
        Assert.That(default(FieldConstraintMetadata), Is.EqualTo(FieldConstraintMetadata.Unconstrained),
            "an omitted constraint and an explicitly unconstrained one must be the same value, or `= default` "
            + "quietly means something stricter than 'no rule said anything'");
    }

    [Test]
    public void AnOmittedConstraintPermitsWhitespace()
    {
        Assert.Multiple(() =>
        {
            Assert.That(default(FieldConstraintMetadata).WhitespaceAllowed, Is.True,
                "no declared constraint means no whitespace ban");
            Assert.That(FieldConstraintMetadata.Unconstrained.WhitespaceAllowed, Is.True);
        });
    }

    /// <summary>
    /// The dialog DTOs that omit their constraints are the reason this matters: every one of their optional
    /// members lands on the struct's zero, and a window binds what it is given.
    /// </summary>
    [Test]
    public void ADialogInputThatOmitsItsConstraintsOffersUnconstrainedFields()
    {
        SceneValueInput scene = new("Titel", IsDimmer: true, On: true, LevelPercent: 50,
            RampMinutes: 0, RampSeconds: 0);

        Assert.Multiple(() =>
        {
            Assert.That(scene.Level, Is.EqualTo(FieldConstraintMetadata.Unconstrained), "SceneValueInput.Level");
            Assert.That(scene.RampPart, Is.EqualTo(FieldConstraintMetadata.Unconstrained), "SceneValueInput.RampPart");
        });
    }

    /// <summary>
    /// A DECLARED whitespace ban still bans whitespace. The fix must not make the constraint unexpressible — it
    /// only changes which way the ZERO reads.
    /// </summary>
    [Test]
    public void ADeclaredWhitespaceBanIsStillExpressible()
    {
        FieldConstraintMetadata banned = FieldConstraintMetadata.Unconstrained with { WhitespaceForbidden = true };

        Assert.Multiple(() =>
        {
            Assert.That(banned.WhitespaceAllowed, Is.False);
            Assert.That(banned, Is.Not.EqualTo(FieldConstraintMetadata.Unconstrained));
        });
    }
}
