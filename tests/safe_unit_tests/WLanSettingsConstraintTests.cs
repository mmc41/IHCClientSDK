using NUnit.Framework;
using Ihc;

namespace Ihc.Tests
{
    /// <summary>
    /// The declared length limits on <see cref="WLanSettings"/> must admit the values the standard allows, because
    /// <c>ConfigurationService.SetWLanSettings</c> validates this object before it sends anything: a limit that is
    /// too tight does not merely warn, it refuses to write settings a real access point requires.
    ///
    /// <para>IEEE 802.11 puts an SSID at up to 32 octets, and a WPA2-PSK passphrase at 8 to 63 characters (the
    /// 64-character form is the raw hex PSK, not a passphrase). Both limits are pinned at their boundary here —
    /// the largest legal value and the first illegal one — so the caps can be seen to have MOVED rather than
    /// disappeared.</para>
    ///
    /// <para>Validation runs through the same helper the service calls, so these tests exercise the check that
    /// actually gates a write, not a re-implementation of it.</para>
    /// </summary>
    public class WLanSettingsConstraintTests
    {
        /// <summary>A settings block that is valid apart from the field under test — the two [Required] address
        /// fields are filled so a failure can only come from the field being examined.</summary>
        private static WLanSettings WithKeyAndSsid(string key, string ssid) => new WLanSettings
        {
            Enabled = true,
            Ssid = ssid,
            Key = key,
            SecurityType = "WPA2",
            EncryptionType = "AES",
            IpAddress = "192.168.2.1",
            Netmask = "255.255.255.0",
            Gateway = "192.168.2.254",
        };

        private static void Validate(WLanSettings settings) =>
            ValidationHelper.ValidateDataAnnotations(settings, nameof(settings));

        // ----- the passphrase -----

        [TestCase(8, Description = "the shortest legal WPA2-PSK passphrase")]
        [TestCase(16, Description = "the previous limit, still legal")]
        [TestCase(32, Description = "a typical generated passphrase")]
        [TestCase(63, Description = "the longest legal WPA2-PSK passphrase")]
        public void Key_OfLegalLength_IsAccepted(int length) =>
            Assert.DoesNotThrow(() => Validate(WithKeyAndSsid(new string('k', length), "HomeNet")));

        [Test]
        public void Key_LongerThanAPassphrase_IsRejected() =>
            Assert.Throws<System.ArgumentException>(
                () => Validate(WithKeyAndSsid(new string('k', 64), "HomeNet")),
                "64 characters is the raw hex PSK, not a passphrase, and stays out of range");

        // ----- the network name -----

        [TestCase(1)]
        [TestCase(16, Description = "the previous limit, still legal")]
        [TestCase(32, Description = "the longest SSID IEEE 802.11 allows")]
        public void Ssid_OfLegalLength_IsAccepted(int length) =>
            Assert.DoesNotThrow(() => Validate(WithKeyAndSsid("passphrase", new string('s', length))));

        [Test]
        public void Ssid_LongerThan32_IsRejected() =>
            Assert.Throws<System.ArgumentException>(
                () => Validate(WithKeyAndSsid("passphrase", new string('s', 33))));

        /// <summary>The neighbouring fields keep their own limits: widening these two must not have widened
        /// anything else in the block. Every other field is filled with a legal value and the message is checked,
        /// so the refusal can only be the over-long address — the three address fields are also [Required], and a
        /// blank one would raise the same exception type for an entirely different reason.</summary>
        [Test]
        public void TheAddressFields_KeepTheirOwnLimits()
        {
            WLanSettings tooLongAddress = WithKeyAndSsid("passphrase", "HomeNet") with
            {
                IpAddress = new string('1', 16),   // [StringLength(15)]
            };

            Assert.That(Assert.Throws<System.ArgumentException>(() => Validate(tooLongAddress))!.Message,
                Does.Contain("IpAddress"), "the refusal must be the length of the address, not a missing field");
        }
    }
}
