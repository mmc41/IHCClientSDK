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
    /// The account groups; a group decides which client applications its accounts may log in with.
    /// </summary>
    public enum IhcUserGroup
    {
      /// <summary>
      /// No group: what a reply without a group maps to, and one with a group key the SDK does not know, which the
      /// read's span names in an <c>UnrecognizedUserGroup</c> warning. The controller has no such group, and the SDK
      /// refuses it on a write.
      /// </summary>
      None,
      
      /// <summary>
      /// The Administrators group (Danish UI: "Administratorer"; reported as
      /// <c>text.usermanager.group_administrators</c>); its accounts may log in with every client application.
      /// </summary>
      Administrators,

      /// <summary>
      /// The Users group (Danish UI: "Brugere"; reported as <c>gtext.users</c>); its accounts may not log in as IHC
      /// Administrator, ServiceView or SceneDesign, but may use SceneView and the online documentation.
      /// </summary>
      Users
    };

    /// <summary>
    /// A user account on the controller, as <see cref="IUserManagerService.GetUsers"/> lists it and a login returns
    /// it.
    /// </summary>
    public record IhcUser : IComparable<IhcUser>
  {
      /// <summary>
      /// Account name, case-sensitive; updates and removals find the account by it, so it cannot be changed.
      /// </summary>
      [Required(ErrorMessage = "Username is required")]
      public string? Username { get; init; }

      /// <summary>
      /// Account password, case-sensitive. The controller returns the stored one with every listed account;
      /// <see cref="IUserManagerService.GetUsers"/> replaces it with <see cref="UserConstants.REDACTED_PASSWORD"/>
      /// unless asked to include it.
      /// </summary>
      /// <remarks>
      /// The SDK refuses an empty or blank password on a write. That is the SDK's own policy, not a known controller
      /// minimum: IHC Administrator lets an empty password through, and whether a controller stores one is not known.
      /// </remarks>
      [Required(ErrorMessage = "Password is required")]
      [DeniedValues(UserConstants.REDACTED_PASSWORD, ErrorMessage = "Password cannot be set to reserved redacted value.")]
      [SensitiveData]
      public string? Password { get; init; }

      /// <summary>
      /// Optional e-mail address of the account holder, free text for display only; <c>null</c> when unset, which the
      /// controller answers as a nil element.
      /// </summary>
      public string? Email { get; init; }

      /// <summary>
      /// Optional first name of the account holder, free text for display only; <c>null</c> or empty when unset.
      /// </summary>
      public string? Firstname { get; init; }

      /// <summary>
      /// Optional last name of the account holder, free text for display only; <c>null</c> or empty when unset.
      /// </summary>
      public string? Lastname { get; init; }

      /// <summary>
      /// Optional phone number of the account holder, free text for display only; <c>null</c> when unset, which the
      /// controller answers as a nil element.
      /// </summary>
      public string? Phone { get; init; }

      /// <summary>
      /// The account's group, which decides the client applications it may log in with; <see cref="IhcUserGroup.None"/>
      /// when the reply carries none or a group the SDK does not know, which the SDK refuses on a write.
      /// </summary>
      [AllowedValues(IhcUserGroup.Administrators, IhcUserGroup.Users, ErrorMessage = "Group must be either 'Administrators' or 'Users'.")]
      // [Required] never fails on a non-nullable enum; it states the outbound contract, and [AllowedValues] above is
      // what refuses None.
      [Required(ErrorMessage = "Group is required")]
      public IhcUserGroup Group { get; init; }
      /// <summary>
      /// Project field of the account, of unknown purpose; <c>null</c> when unset, which the controller answers as a nil
      /// element.
      /// </summary>
      public string? Project { get; init; }

      /// <summary>
      /// Creation date of user, in the controller's local time; an update that sends no date re-stamps it. The SDK
      /// reads it at a fixed +01:00 offset, so the instant is right only for a time stamped at UTC+1;
      /// <c>DateTimeOffset.MinValue</c> when the reply carries no readable date.
      /// </summary>
      public DateTimeOffset CreatedDate { get; init; }

      /// <summary>
      /// Last login date of user, in the controller's local time; an add or update that sends no date sets it to the
      /// time of the write. Offset and absence are handled as for <see cref="CreatedDate"/>.
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