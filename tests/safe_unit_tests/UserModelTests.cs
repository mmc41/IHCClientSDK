using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ihc;

namespace Ihc.Tests
{
    /// <summary>
    /// IhcUser.CompareTo is the SDK's ordering for a user LIST, so it must order names the way a reader
    /// expects to see them rather than by code unit. Ordinal would sort every capitalised name ahead of
    /// every lower-case one, which is the ordering these tests exist to keep out.
    ///
    /// <para>The fixture runs under en-US ON PURPOSE. The ordering is a property of the SDK, not of the
    /// machine it runs on, so asserting Danish collation from a non-Danish culture is what proves that —
    /// pinning the fixture to da-DK would make these tests pass for the wrong reason and hide a
    /// regression to the ambient culture.</para>
    /// </summary>
    [TestFixture]
    [SetCulture("en-US")]
    public class UserModelTests
    {
        private static IhcUser User(string username) => new IhcUser() { Username = username };

        [Test]
        public void CompareTo_OrdersNamesLinguistically_NotByCase()
        {
            Assert.That(User("anna").CompareTo(User("Bo")), Is.LessThan(0));
            Assert.That(User("Bo").CompareTo(User("anna")), Is.GreaterThan(0));
        }

        [Test]
        public void CompareTo_SortsAMixedCaseListAlphabetically()
        {
            List<IhcUser> users = [User("Zoe"), User("adam"), User("Bo"), User("charlie")];

            users.Sort();

            Assert.That(users.Select(u => u.Username), Is.EqualTo(new[] { "adam", "Bo", "charlie", "Zoe" }));
        }

        /// <summary>
        /// Danish collation puts Æ, Ø and Å AFTER Z; every other common culture, and the invariant comparer,
        /// put them among the a/o letters. The SDK names the culture rather than following the host's, so a
        /// controller's user list reads the same on a Danish and an English machine.
        /// </summary>
        [Test]
        public void CompareTo_OrdersDanishLettersAfterZ()
        {
            Assert.That(User("æble").CompareTo(User("zebra")), Is.GreaterThan(0), "æ sorts after z in Danish");
            Assert.That(User("øst").CompareTo(User("zebra")), Is.GreaterThan(0), "ø sorts after z in Danish");
            Assert.That(User("århus").CompareTo(User("zebra")), Is.GreaterThan(0), "å sorts after z in Danish");
        }

        [Test]
        public void CompareTo_SortsAListWithDanishLettersInAlphabeticalOrder()
        {
            List<IhcUser> users = [User("øst"), User("Bo"), User("århus"), User("zebra"), User("æble"), User("anna")];

            users.Sort();

            Assert.That(users.Select(u => u.Username),
                Is.EqualTo(new[] { "anna", "Bo", "zebra", "æble", "øst", "århus" }));
        }

        [Test]
        public void CompareTo_EqualUsernames_ComparesEqual()
        {
            Assert.That(User("anna").CompareTo(User("anna")), Is.EqualTo(0));
        }

        [Test]
        public void CompareTo_Null_SortsAfterEveryName()
        {
            Assert.That(User("anna").CompareTo(null), Is.GreaterThan(0));
        }

        /// <summary>
        /// Username is nullable, so a user built but not yet filled in compares against null too. IComparable
        /// says every instance sorts after null; comparing the two usernames directly would call that pair equal.
        /// </summary>
        [Test]
        public void CompareTo_Null_SortsAfterAUserThatHasNoUsernameYet()
        {
            Assert.That(new IhcUser().CompareTo(null), Is.GreaterThan(0));
        }

        [Test]
        public void CompareTo_MissingUsername_SortsBeforeAName()
        {
            Assert.That(new IhcUser().CompareTo(User("anna")), Is.LessThan(0));
            Assert.That(User("anna").CompareTo(new IhcUser()), Is.GreaterThan(0));
        }

        /// <summary>
        /// The comparer is built in a STATIC FIELD INITIALIZER, so a culture it cannot resolve does not cost
        /// Danish collation — it throws TypeInitializationException out of every CompareTo on the type. That is
        /// what a host published with InvariantGlobalization=true does to every named culture, and this SDK
        /// cannot see such a host from its own build. The name is passed in because that is the only way to
        /// reach the branch in-process; the production call site passes "da-DK".
        /// </summary>
        [Test]
        public void CreateUsernameOrder_UnresolvableCulture_DegradesInsteadOfThrowing()
        {
            StringComparer comparer = IhcUser.CreateUsernameOrder("not a culture name!");

            Assert.That(comparer, Is.Not.Null);
            Assert.That(comparer.Compare("anna", "Bo"), Is.LessThan(0),
                "the fallback still has to ORDER names, not just avoid throwing");
        }

        [Test]
        public void CreateUsernameOrder_ResolvableCulture_KeepsDanishCollation()
        {
            StringComparer comparer = IhcUser.CreateUsernameOrder("da-DK");

            Assert.That(comparer.Compare("æble", "zebra"), Is.GreaterThan(0));
        }
    }
}
