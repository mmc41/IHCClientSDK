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
    /// 32 bits wide - so an address that is not IPv4 has no representation there at all. What made this a
    /// defect rather than a limitation is that <see cref="IPAddress.Parse"/> accepts an IPv6 literal happily,
    /// <c>GetAddressBytes</c> hands back sixteen bytes, and the conversion then read four of them: a
    /// controller ended up configured with a DNS server nobody submitted, derived from a quarter of one that
    /// somebody did. The refusal is the contract now, and it fires before the request is built.
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
        /// The pre-existing refusal for text that is no address at all stays where it was - in
        /// <see cref="IPAddress.Parse"/> - so the new guard is proven to add a case rather than to
        /// replace one.
        /// </summary>
        [TestCase("")]
        [TestCase("not-an-address")]
        [TestCase("192.168.1.256")]
        public void TextThatIsNoAddressAtAll_StillRaisesFormatException(string address)
        {
            Assert.Throws<FormatException>(() => NetworkHelper.ConvertIPAddressToInt(address));
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
