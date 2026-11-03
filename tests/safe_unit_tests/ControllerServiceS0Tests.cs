using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Soap.Controller;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The S0 meter's writes against a faked SOAP layer. <see cref="ControllerService.SetS0FiscalYearStart"/>: the
    /// operation's first part is the day of the month and its second the month, as IHC ServiceView sends them; the
    /// descrambled vendor signature <c>(month, day)</c> had the names reversed.
    /// <see cref="ControllerService.ResetS0Values"/>: five period flags, which ServiceView sends in wire order.
    /// </summary>
    [TestFixture]
    public class ControllerServiceS0Tests
    {
        [Test]
        public async Task SetS0FiscalYearStart_SendsTheDayFirst_AndTheMonthSecond()
        {
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            inputMessageName20? sent = null;
            A.CallTo(() => soap.setS0FiscalYearStartAsync(A<inputMessageName20>._))
                .Invokes((inputMessageName20 request) => sent = request)
                .Returns(Task.FromResult(new outputMessageName20()));

            await new ControllerService(FakeSession.Over(), soap).SetS0FiscalYearStart(month: 3, day: 1);

            Assert.That((sent?.setS0FiscalYearStart1, sent?.setS0FiscalYearStart2), Is.EqualTo(((sbyte?)1, (sbyte?)3)));
            Assert.That(SoapRequestText.Normalized(SoapRequestText.Of(sent!)),
                Does.Contain("<utcs:setS0FiscalYearStart1>1</utcs:setS0FiscalYearStart1><utcs:setS0FiscalYearStart2>3</utcs:setS0FiscalYearStart2>"));
        }

        /// <summary>
        /// The reset takes one flag per period, in the order IHC ServiceView labels them: hour, day, week, month and
        /// year. Each flag goes out as given, none nil; a pattern with no two neighbours equal shows the order.
        /// </summary>
        [Test]
        public async Task ResetS0Values_SendsTheFivePeriodFlags_InWireOrder()
        {
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            inputMessageName10? sent = null;
            A.CallTo(() => soap.resetS0ValuesAsync(A<inputMessageName10>._))
                .Invokes((inputMessageName10 request) => sent = request)
                .Returns(Task.FromResult(new outputMessageName10()));

            await new ControllerService(FakeSession.Over(), soap).ResetS0Values(hour: true, day: false, week: true, month: false, year: true);

            Assert.That((sent?.resetS0Values1, sent?.resetS0Values2, sent?.resetS0Values3, sent?.resetS0Values4, sent?.resetS0Values5),
                Is.EqualTo(((bool?)true, (bool?)false, (bool?)true, (bool?)false, (bool?)true)));
            Assert.That(SoapRequestText.Normalized(SoapRequestText.Of(sent!)), Does.Contain(
                "<utcs:resetS0Values1>true</utcs:resetS0Values1><utcs:resetS0Values2>false</utcs:resetS0Values2>" +
                "<utcs:resetS0Values3>true</utcs:resetS0Values3><utcs:resetS0Values4>false</utcs:resetS0Values4>" +
                "<utcs:resetS0Values5>true</utcs:resetS0Values5>"));
        }
    }
}
