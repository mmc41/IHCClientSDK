using System;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The conversion every controller date passes through.
    ///
    /// <see cref="DateHelper.CreateDateTimeOffset"/> sits under all nine generated <c>WSDate</c> wrappers, so
    /// login dates, module and project timestamps and the controller clock all read through it. Its own
    /// doc-comment states one contract - "Returns MinValue if invalid date" - and the year, month and day
    /// guards keep it. The TIME guards did not: they zeroed the offending component and kept the date, so a
    /// corrupt wire time arrived as an ordinary-looking midnight instant that no caller could tell from a
    /// real one. A sentinel a caller can test for is the whole point of having one.
    /// </summary>
    [TestFixture]
    public class DateHelperTests
    {
        private static readonly TimeSpan Offset = DateHelper.GetWSTimeOffset();

        /// <summary>Each date component out of range - the contract that was already kept, pinned so the
        /// time half below is proven to have joined it rather than replaced it.</summary>
        [TestCase(0, 6, 15, TestName = "{m}(year 0)")]
        [TestCase(10000, 6, 15, TestName = "{m}(year 10000)")]
        [TestCase(2024, 0, 15, TestName = "{m}(month 0)")]
        [TestCase(2024, 13, 15, TestName = "{m}(month 13)")]
        [TestCase(2024, 6, 0, TestName = "{m}(day 0)")]
        [TestCase(2024, 6, 32, TestName = "{m}(day 32)")]
        public void AnOutOfRangeDateComponent_YieldsTheMinValueSentinel(int year, int month, int day)
        {
            Assert.That(DateHelper.CreateDateTimeOffset(year, month, day, 12, 30, 45, Offset),
                Is.EqualTo(DateTimeOffset.MinValue));
        }

        /// <summary>
        /// The defect: these six used to yield <c>2024-06-15T00:30:45</c> and friends - a date that reads as
        /// real. The date is now discarded with the time, because a record whose time could not be read is
        /// not a record whose date can be trusted either.
        /// </summary>
        [TestCase(24, 30, 45, TestName = "{m}(hours 24)")]
        [TestCase(-1, 30, 45, TestName = "{m}(hours -1)")]
        [TestCase(12, 60, 45, TestName = "{m}(minutes 60)")]
        [TestCase(12, -1, 45, TestName = "{m}(minutes -1)")]
        [TestCase(12, 30, 60, TestName = "{m}(seconds 60)")]
        [TestCase(12, 30, -1, TestName = "{m}(seconds -1)")]
        public void AnOutOfRangeTimeComponent_YieldsTheMinValueSentinelRatherThanAnOrdinaryLookingInstant(
            int hours, int minutes, int seconds)
        {
            Assert.That(DateHelper.CreateDateTimeOffset(2024, 6, 15, hours, minutes, seconds, Offset),
                Is.EqualTo(DateTimeOffset.MinValue));
        }

        /// <summary>The legitimate range is untouched, offset included - so the guard narrowed nothing.</summary>
        [Test]
        public void AnInRangeDate_ConvertsToTheExactInstantAndOffset()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DateHelper.CreateDateTimeOffset(2024, 6, 15, 12, 30, 45, Offset),
                    Is.EqualTo(new DateTimeOffset(2024, 6, 15, 12, 30, 45, Offset)));
                Assert.That(DateHelper.CreateDateTimeOffset(2024, 6, 15, 0, 0, 0, Offset),
                    Is.EqualTo(new DateTimeOffset(2024, 6, 15, 0, 0, 0, Offset)));
                Assert.That(DateHelper.CreateDateTimeOffset(2024, 6, 15, 23, 59, 59, Offset),
                    Is.EqualTo(new DateTimeOffset(2024, 6, 15, 23, 59, 59, Offset)));
            });
        }

        /// <summary>
        /// The generated <c>WSDate</c> fields are non-nullable <c>int</c>, so an element the controller left
        /// out arrives as zero - which the year guard already turns into the sentinel. Pinned because the
        /// masking-site report claimed the opposite ("reads as an ordinary date"), and the claim decides
        /// whether an absent wire date needs a guard of its own.
        /// </summary>
        [Test]
        public void AnAbsentWireDate_ReadsAsTheSentinelThroughTheGeneratedWrapper()
        {
            Assert.That(new Ihc.Soap.Authentication.WSDate().ToDateTimeOffset(),
                Is.EqualTo(DateTimeOffset.MinValue));
        }

        /// <summary>
        /// One case through a generated wrapper, so the nine hand-repeated copies of the same call are
        /// pinned to the helper's contract rather than only to each other.
        /// </summary>
        [Test]
        public void TheGeneratedWrapper_CarriesTheSameTimeContractAsTheHelper()
        {
            var corruptTime = new Ihc.Soap.Authentication.WSDate
            {
                year = 2024,
                monthWithJanuaryAsOne = 6,
                day = 15,
                hours = 24,
                minutes = 30,
                seconds = 45,
            };
            var realTime = new Ihc.Soap.Authentication.WSDate
            {
                year = 2024,
                monthWithJanuaryAsOne = 6,
                day = 15,
                hours = 12,
                minutes = 30,
                seconds = 45,
            };

            Assert.Multiple(() =>
            {
                Assert.That(corruptTime.ToDateTimeOffset(), Is.EqualTo(DateTimeOffset.MinValue));
                Assert.That(realTime.ToDateTimeOffset(),
                    Is.EqualTo(new DateTimeOffset(2024, 6, 15, 12, 30, 45, Offset)));
            });
        }
    }
}
