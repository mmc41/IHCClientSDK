using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Ihc {
    public static class UserConstants {
      /// <summary>
      /// Safe value of password when it is scrubbed from view.
      /// </summary>
      public const string REDACTED_PASSWORD = "**REDACTED**";
    };

    /// <summary>
    /// High level enumeration for UserGroup values without soap distractions.
    /// </summary>
    public enum IhcUserGroup
    {
      /// <summary>
      /// Only used with not specified - not supported by IHC.
      /// </summary>
      None,
      
      Administrators,

      Users
    };

    /// <summary>
    /// High level model of a IHC user without soap distractions.
    /// </summary>
    public record IhcUser : IComparable<IhcUser>
  {
      [StringLength(20, MinimumLength = 1, ErrorMessage = "Username length can't be more than 20.")]
      [Required(ErrorMessage = "Username is required")]
      public string? Username { get; init; }

      [StringLength(20, ErrorMessage = "Password length can't be more than 20.")]
      [Required(ErrorMessage = "Password is required")]
      [DeniedValues(UserConstants.REDACTED_PASSWORD, ErrorMessage = "Password cannot be set to reserved redacted value.")]
      [SensitiveData]
      public string? Password { get; init; }

      [StringLength(25, ErrorMessage = "Email length can't be more than 25.")]
      public string? Email { get; init; }

      [StringLength(15, ErrorMessage = "Firstname length can't be more than 15.")]
      public string? Firstname { get; init; }

      [StringLength(15, ErrorMessage = "Lastname length can't be more than 15.")]
      public string? Lastname { get; init; }

      [StringLength(15, ErrorMessage = "Phone length can't be more than 15.")]
      public string? Phone { get; init; }

      [AllowedValues(IhcUserGroup.Administrators, IhcUserGroup.Users, ErrorMessage = "Group must be either 'Administrators' or 'Users'.")]
      [Required(ErrorMessage = "Group is required")]
      public IhcUserGroup Group { get; init; }
      public string? Project { get; init; }

      /// <summary>
      /// Creation date of user.
      /// </summary>
      public DateTimeOffset CreatedDate { get; init; }

      /// <summary>
      /// Last login date of user.
      /// </summary>
      public DateTimeOffset LoginDate { get; init; }

      /// <summary>
      /// Creates a safe copy of this user definition without a password.
      /// </summary>
      /// <returns>Safe user</returns>
      public IhcUser RedactPassword()
      {
        return this with { Password = UserConstants.REDACTED_PASSWORD };
      }

      /// <summary>
      /// Creates a safe copy of this user definition without a password.
      /// </summary>
      /// <returns>Safe user</returns>
      [Obsolete("Misspelled; use " + nameof(RedactPassword) + " instead. This forwarder will be removed at the next version boundary.")]
      public IhcUser RedactPasword() => RedactPassword();

      /// <summary>
      /// This default ToString method should not be used! Use alternative with bool parameter.
      /// </summary>
      /// <returns></returns>
      public override string ToString()
      {
        return this.ToString(true); // Unsecure - will output password
      }

      /// <summary>
      /// Safely convert to string. Only convert password if LogSensitiveData set to true.
      /// </summary>
      /// <returns></returns>
      public string ToString(bool LogSensitiveData)
      {
        return $"IhcUser(Username={Username}, Password={(LogSensitiveData ? Password : UserConstants.REDACTED_PASSWORD)}, Email={Email}, Firstname={Firstname}, Lastname={Lastname}, Phone={Phone}, Group={Group}, Project={Project}, CreatedDate={CreatedDate}, LoginDate={LoginDate})";
      }

      /// <summary>
      /// Username is unique identifier so use this for hashcode.
      /// </summary>
      /// <returns>Hash code based on Username</returns>
      /// <remarks>
      /// Note: While GetHashCode uses only Username as the unique identifier,
      /// the record's default Equals method (inherited) performs deep comparison of all properties.
      /// This is intentional - Username identifies the user, but Equals detects any property changes.
      /// </remarks>
      public override int GetHashCode()
      {
        return string.IsNullOrEmpty(Username) ? 0 : Username.GetHashCode();
      }

      /// <summary>
      /// Compares only the administrative properties of two IhcUser instances.
      /// Excludes automatic timestamp fields (CreatedDate, LoginDate) that change independentl.
      /// Use this method to detect if changes should be persisted.
      /// </summary>
      public bool EqualsChangeableProperties(IhcUser other)
      {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Username == other.Username &&
               Password == other.Password &&
               Email == other.Email &&
               Firstname == other.Firstname &&
               Lastname == other.Lastname &&
               Phone == other.Phone &&
               Group == other.Group &&
               Project == other.Project;
      }

      /// <summary>How a user list is ordered — alphabetically, the way a reader expects to read it. Danish
      /// collation, because the controller this SDK talks to is a Danish product and da-DK sorts Æ/Ø/Å
      /// after Z where neither the invariant comparer nor an English one would. NAMED rather than taken from
      /// the ambient culture: the ordering is a property of the SDK, so a user list must not come out in a
      /// different order on an English machine than on a Danish one. Ihc.Vis.EnumTypeDisplayOrder names the
      /// same culture for the same reason; it compares case-INSENSITIVELY because a display label's casing is
      /// incidental, where a username's is part of the credential.</summary>
      private static readonly StringComparer UsernameOrder = CreateUsernameOrder(DanishCulture);

      private const string DanishCulture = "da-DK";

      /// <summary>
      /// The ordering comparer, degrading rather than throwing when <paramref name="cultureName"/> cannot be
      /// resolved.
      /// <para>A host published with <c>InvariantGlobalization=true</c> has no named cultures at all — that
      /// switch defaults <c>PredefinedCulturesOnly</c> to true, and <see cref="CultureInfo.GetCultureInfo(string)"/>
      /// then throws for every name but the invariant one. Left to throw inside a static field initializer that
      /// would take out the whole type: <c>TypeInitializationException</c> on any <see cref="CompareTo(IhcUser)"/>, not
      /// merely the loss of Danish collation. Ordering by the invariant culture is the closest such a host can
      /// get, and it is what the SDK did before this comparer existed.</para>
      /// </summary>
      internal static StringComparer CreateUsernameOrder(string cultureName)
      {
        try
        {
          return StringComparer.Create(CultureInfo.GetCultureInfo(cultureName), ignoreCase: false);
        }
        catch (CultureNotFoundException)
        {
          return StringComparer.InvariantCulture;
        }
      }

      /// <summary>
      /// Order users by username, the way a reader expects to see a user list. The comparison is linguistic
      /// rather than ordinal on purpose: ordinal would group every capitalised name ahead of every lower-case
      /// one, and would order the Danish letters by code point instead of by the alphabet.
      /// </summary>
      /// <param name="other">The other IhcUser to compare with</param>
      /// <returns>Comparison result for ordering</returns>
      public int CompareTo(IhcUser? other)
      {
        // IComparable: every instance sorts after null. Username is nullable, so comparing the two names
        // straight through would call a user with no username yet EQUAL to null rather than greater.
        if (other is null)
          return 1;

        return UsernameOrder.Compare(this.Username, other.Username);
      }
    }
}