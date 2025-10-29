using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;

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
      public string Username { get; init; }

      [StringLength(20, ErrorMessage = "Password length can't be more than 20.")]
      [Required(ErrorMessage = "Username is required")]
      [DeniedValues(UserConstants.REDACTED_PASSWORD, ErrorMessage = "Password cannot be set to reserved redacted value.")]
      public string Password { get; init; }

      [StringLength(25, ErrorMessage = "Email length can't be more than 25.")]
      public string Email { get; init; }

      [StringLength(15, ErrorMessage = "Firstname length can't be more than 15.")]
      public string Firstname { get; init; }

      [StringLength(15, ErrorMessage = "Lastname length can't be more than 15.")]
      public string Lastname { get; init; }

      [StringLength(15, ErrorMessage = "Phone length can't be more than 15.")]
      public string Phone { get; init; }
      
      [AllowedValues(IhcUserGroup.Administrators, IhcUserGroup.Users, ErrorMessage = "Group must be either 'Administrators' or 'Users'.")]
      public IhcUserGroup Group { get; init; }
      public string Project { get; init; }

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
      public IhcUser RedactPasword()
      {
        return this with { Password = UserConstants.REDACTED_PASSWORD };
      }

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
      /// <returns></returns>
      public override int GetHashCode()
      {
        return string.IsNullOrEmpty(Username) ? 0 : Username.GetHashCode();
      }
      
      /// <summary>
      /// Order users by username.
      /// </summary>

      public int CompareTo(IhcUser other)
      {
        return string.Compare(this.Username, other?.Username); 
      }
    }
}