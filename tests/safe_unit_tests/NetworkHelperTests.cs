using System;
using System.Net;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Soap.Configuration;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The integer form a DNS server takes on the wire.
    ///
    /// <see cref="NetworkHelper.ConvertIPAddressToInt"/> is the only writer of that field, and the field is
    /// 32 bits wide, so only dotted-decimal IPv4 text has a representation there. The conversion used to hand the
    /// text to <see cref="IPAddress.Parse"/>, which accepts far more: an IPv6 literal, of which four bytes were
    /// read, and the legacy BSD forms, which read a leading zero as octal and widen a short address. Each sent a
    /// DNS server nobody typed. Now only four decimal groups pass, and everything else is refused before the
    /// request is built.
    /// </summary>
    [TestFixture]
    public class NetworkHelperTests
    {
        [TestCase("192.168.1.1")]
        [TestCase("0.0.0.0")]
        [TestCase("255.255.255.255")]
        [TestCase("8.8.8.8")]
        public void AnIPv4Address_RoundTripsThroughBothDirections(string address)
        {
            int wire = NetworkHelper.ConvertIPAddressToInt(address);

            Assert.That(NetworkHelper.ConvertIntToIPAddress(wire), Is.EqualTo(address));
        }

        /// <summary>
        /// The three shapes that reach the four-byte read: a compressed literal, a full one, and the
        /// IPv4-mapped form - which is the trap, because it CONTAINS a legitimate IPv4 address and yet
        /// parses to an <c>InterNetworkV6</c> whose first four bytes are zeroes.
        /// </summary>
        [TestCase("::1")]
        [TestCase("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
        [TestCase("fe80::1")]
        [TestCase("::ffff:192.168.1.1")]
        public void AnIPv6Address_IsRefusedRatherThanNarrowedToThirtyTwoBits(string address)
        {
            var ex = Assert.Throws<ArgumentException>(() => NetworkHelper.ConvertIPAddressToInt(address));

            Assert.That(ex!.Message, Does.Contain(address),
                "the refusal must name the value that was submitted, so a caller can tell which entry was rejected");
        }

        /// <summary>
        /// A group with a leading zero is a decimal number, as IHC Administrator reads it. <see cref="IPAddress.Parse"/>
        /// would read <c>010</c> as octal 8 and refuse <c>08</c>, sending a server nobody typed or none at all.
        /// </summary>
        [TestCase("010.010.010.010", "10.10.10.10")]
        [TestCase("192.168.001.010", "192.168.1.10")]
        [TestCase("08.8.8.8", "8.8.8.8")]
        [TestCase("000.000.000.000", "0.0.0.0")]
        public void ALeadingZero_IsADecimalDigit(string typed, string meant)
        {
            Assert.That(NetworkHelper.ConvertIntToIPAddress(NetworkHelper.ConvertIPAddressToInt(typed)), Is.EqualTo(meant));
        }

        /// <summary>
        /// Only four dot-separated groups of one to three ASCII digits, each 0-255, are an address. The legacy forms
        /// <see cref="IPAddress.Parse"/> also accepts -- fewer groups widening the last, hexadecimal, one 32-bit number
        /// -- and digits of other scripts are refused, as is text that is no address at all, each naming what was
        /// given.
        /// </summary>
        [TestCase("10.1")]
        [TestCase("192.168.1")]
        [TestCase("0x0A.1.1.1")]
        [TestCase("3232235777")]
        [TestCase("١٩٢.١٦٨.١.١")]
        [TestCase("192.168.1.256")]
        [TestCase("0001.1.1.1")]
        [TestCase("1.2.3.4.5")]
        [TestCase("1..2.3")]
        [TestCase(" 1.2.3.4")]
        [TestCase("not-an-address")]
        public void TextThatIsNotFourDecimalGroups_IsRefused_NamingIt(string address)
        {
            var ex = Assert.Throws<ArgumentException>(() => NetworkHelper.ConvertIPAddressToInt(address));

            Assert.That(ex!.Message, Does.Contain($"'{address}'"));
        }

        [Test]
        public void EmptyText_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => NetworkHelper.ConvertIPAddressToInt(""));
        }

        /// <summary>
        /// The second, independent check the plan's top tier asks for: the refusal has to protect the WIRE,
        /// not merely the helper. An IPv6 entry submitted to <see cref="ConfigurationService.SetDNSServers"/>
        /// must never reach the SOAP layer, because the value that would arrive there is already the
        /// narrowed one.
        /// </summary>
        [Test]
        public void AnIPv6DnsEntry_IsRefusedBeforeTheRequestIsSent()
        {
            var soap = A.Fake<Ihc.Soap.Configuration.ConfigurationService>();
            A.CallTo(() => soap.setDNSServersAsync(A<inputMessageName8>._))
                .Returns(Task.FromResult(new outputMessageName8()));
            var service = new ConfigurationService(FakeSession.Over(), soap);

            Assert.ThrowsAsync<ArgumentException>(() =>
                service.SetDNSServers(new DNSServers { PrimaryDNS = "2001:db8::1", SecondaryDNS = "8.8.4.4" }));

            A.CallTo(() => soap.setDNSServersAsync(A<inputMessageName8>._)).MustNotHaveHappened();
        }

        /// <summary>A pair of legitimate IPv4 servers still reaches the wire, so the guard narrowed nothing.</summary>
        [Test]
        public async Task AnIPv4DnsPair_StillReachesTheWire()
        {
            var soap = A.Fake<Ihc.Soap.Configuration.ConfigurationService>();
            inputMessageName8? sent = null;
            A.CallTo(() => soap.setDNSServersAsync(A<inputMessageName8>._))
                .Invokes((inputMessageName8 m) => sent = m)
                .Returns(Task.FromResult(new outputMessageName8()));
            var service = new ConfigurationService(FakeSession.Over(), soap);

            await service.SetDNSServers(new DNSServers { PrimaryDNS = "8.8.8.8", SecondaryDNS = "8.8.4.4" });

            Assert.Multiple(() =>
            {
                Assert.That(sent!.setDNSServers1!.ipAddress, Is.EqualTo(NetworkHelper.ConvertIPAddressToInt("8.8.8.8")));
                Assert.That(sent.setDNSServers2!.ipAddress, Is.EqualTo(NetworkHelper.ConvertIPAddressToInt("8.8.4.4")));
            });
        }
    }
}
