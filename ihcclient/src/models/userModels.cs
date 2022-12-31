using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;

namespace Ihc {
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
    }
}