using System;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Soap.Smsmodem;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <see cref="SmsModemSettings.RecipientSlots"/> is a mask of one <c>0</c> or <c>1</c> per slot of the modem's
    /// 30-slot phone list. IHC Administrator builds exactly 30 such characters and ignores any other length on read,
    /// so the SDK refuses any other mask before it is sent; a null mask is left out of the record as before.
    /// </summary>
    [TestFixture]
    public class SmsModemServiceValidationTests
    {
        /// <summary>The mask IHC Administrator was seen sending: slot 24 marked.</summary>
        private const string CapturedMask = "000000000000000000000001000000";

        private static (SmsModemService Service, SMSModemService Soap) NewService()
        {
            var soap = A.Fake<SMSModemService>();
            A.CallTo(() => soap.setSMSModemSettingsAsync(A<inputMessageName1>._)).Returns(Task.FromResult(new outputMessageName1()));
            return (new SmsModemService(FakeSession.Over(), soap), soap);
        }

        [TestCase(CapturedMask)]
        [TestCase(null)]
        public async Task SetSmsModemSettings_SendsAThirtySlotMask_OrNone(string? mask)
        {
            var (service, soap) = NewService();

            await service.SetSmsModemSettings(new SmsModemSettings { RecipientSlots = mask });

            A.CallTo(() => soap.setSMSModemSettingsAsync(A<inputMessageName1>.That.Matches(m => m.setSMSModemSettings1.m_powerdownNumber == mask)))
                .MustHaveHappenedOnceExactly();
        }

        [TestCase("00000000000000000000000100000", TestName = "SetSmsModemSettings_RefusesAMask(29 characters)")]
        [TestCase("0000000000000000000000010000000", TestName = "SetSmsModemSettings_RefusesAMask(31 characters)")]
        [TestCase("000000000000000000000002000000", TestName = "SetSmsModemSettings_RefusesAMask(a 2)")]
        [TestCase("+4512345678", TestName = "SetSmsModemSettings_RefusesAMask(a phone number)")]
        [TestCase("", TestName = "SetSmsModemSettings_RefusesAMask(empty)")]
        public void SetSmsModemSettings_RefusesAMask(string mask)
        {
            var (service, soap) = NewService();

            Assert.That(async () => await service.SetSmsModemSettings(new SmsModemSettings { RecipientSlots = mask }),
                Throws.ArgumentException.With.Message.Contains(nameof(SmsModemSettings.RecipientSlots)));
            A.CallTo(() => soap.setSMSModemSettingsAsync(A<inputMessageName1>._)).MustNotHaveHappened();
        }
    }
}
