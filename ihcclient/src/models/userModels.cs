using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;

namespace Ihc {
    public static class UserConstants {
      /// <summary>
      /// Safe value of password when it is scrubbed from view.
      /// </summary>
      public const string REDACTED_PASSWORD = "**REDACTED**";
    };

    /**
     * High level model of a IHC user without soap distractions.
     */
    public record IhcUser {
      public string Username { get; init; }
      public string Password { get; init; }
      public string Email { get; init; }
      public string Firstname { get; init; }
      public string Lastname { get; init; }
      public string Phone { get; init; }
      public string Group { get; init; }
      public string Project  { get; init; }
      public DateTimeOffset CreatedDate { get; init; }
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
        return this.ToString(false); // No password output by default. 
      }
          
      /// <summary>
      /// Safely convert to string. Only convert password if LogSensitiveData set to true.
      /// </summary>
      /// <returns></returns>
      public string ToString(bool LogSensitiveData)
      {
        return $"IhcUser(Username={Username}, Password={(LogSensitiveData ? Password : UserConstants.REDACTED_PASSWORD)}, Email={Email}, Firstname={Firstname}, Lastname={Lastname}, Phone={Phone}, Group={Group}, Project={Project}, CreatedDate={CreatedDate}, LoginDate={LoginDate})";
      }
          
    }
}