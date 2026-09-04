using Ihc.UiAutomation;
using NUnit.Framework;
using System;
using System.Runtime.Versioning;

namespace Ihc.Tests
{
    /// <summary>
    /// The UI-Automation toolkit's bounded wait: that it stops as soon as the condition holds, that it gives
    /// up when looking again is pointless, and that a failure says enough to classify itself.
    /// </summary>
    /// <remarks>
    /// <para>This is the ONLY part of <c>shared/ihc_uiautomation_windows</c> with a test. Every other type there is a
    /// call into the live UI-Automation client, which a test could face only with a fake of the very API under
    /// test; <see cref="UiaWait"/> calls nothing and polls a delegate the caller supplies. Without these, a
    /// defect in the poll loop would surface as an unexplained desktop-mode failure — the "unclassified" row of
    /// TESTSTRATEGY.md's flakiness table, which is the row this primitive exists to shrink.</para>
    ///
    /// <para>The assertions are driven by a COUNTER wherever they can be. A test that asserts "satisfied after
    /// 300 ms" fails on a loaded build agent for a reason that has nothing to do with the code — which would
    /// make a fixture about removing timing guesses into one built on a timing guess.</para>
    ///
    /// <para>Windows-gated because the assembly declares <c>SupportedOSPlatform("windows6.1")</c> for the sake
    /// of every other type in it. This one would run anywhere; the contract is the assembly's, and weakening
    /// it per type would trade a compiler-held guarantee for nothing.</para>
    /// </remarks>
    [SupportedOSPlatform("windows6.1")]
    public class UiaWaitTests
    {
        /// <summary>Long enough that a counter, not the clock, decides when a wait ends.</summary>
        private static readonly TimeSpan Generous = TimeSpan.FromSeconds(30);

        /// <summary>Short enough that a test asserting a timeout does not pay for it.</summary>
        private static readonly TimeSpan Brief = TimeSpan.FromMilliseconds(20);

        /// <summary>Fast enough that the poll interval never dominates what a test is measuring.</summary>
        private static readonly TimeSpan Rapid = TimeSpan.FromMilliseconds(1);

        [SetUp]
        public void SetUp()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Ignore("The UI-Automation toolkit is declared Windows-only at the assembly.");
            }
        }

        [Test]
        public void Until_StopsLookingAsSoonAsTheProbeYieldsAValue()
        {
            int looks = 0;

            UiaWaitResult<string> result = UiaWait.Until(
                probe: () => ++looks >= 3 ? "ready" : null,
                satisfied: _ => true,
                timeout: Generous,
                poll: Rapid);

            AssertSettled(result, on: "ready", afterPolls: 3, "the wait looked again after the condition held");
        }

        [Test]
        public void Until_KeepsLookingWhileTheProbedValueIsNotTheOneBeingWaitedFor()
        {
            // The level-triggered shape the drivers wait in: a value is always readable, and what decides the
            // wait is whether the value read is the one that was asked for.
            string[] states = ["stale", "stale", "current"];
            int looks = 0;

            UiaWaitResult<string> result = UiaWait.Until(
                probe: () => states[Math.Min(looks++, states.Length - 1)],
                satisfied: state => state == "current",
                timeout: Generous,
                poll: Rapid);

            AssertSettled(result, on: "current", afterPolls: 3, "a value the predicate rejected ended the wait anyway");
        }

        /// <summary>
        /// A wait that ended satisfied, on the value expected and no later than the look expected. The poll
        /// count is half of it: a wait that returns the right value after looking too many times has stopped
        /// being level-triggered without failing anything.
        /// </summary>
        private static void AssertSettled(UiaWaitResult<string> result, string on, int afterPolls, string whenLate)
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.Satisfied, Is.True, result.ToString());
                Assert.That(result.Value, Is.EqualTo(on));
                Assert.That(result.Polls, Is.EqualTo(afterPolls), whenLate);
            });
        }

        [Test]
        public void Until_TimesOutSayingHowOftenItLookedAndWhatItLastSaw()
        {
            const string snapshot = "gen=3|ver=18|val=3.17|dirty=1";

            UiaWaitResult<string> result = UiaWait.Until(
                probe: () => snapshot,
                satisfied: _ => false,
                timeout: Brief,
                poll: Rapid,
                describe: seen => seen ?? "nothing");

            Assert.Multiple(() =>
            {
                Assert.That(result.Satisfied, Is.False);
                Assert.That(result.Value, Is.EqualTo(snapshot), "a timeout dropped the last value it read");
                Assert.That(result.LastSeen, Is.EqualTo(snapshot));
                Assert.That(result.Polls, Is.GreaterThan(1), "the wait gave up after a single look");
                Assert.That(result.ToString(), Does.Contain("last saw " + snapshot),
                    "the diagnostic is half the reason this primitive exists");
            });
        }

        [Test]
        public void Until_LooksOnceEvenWhenTheTimeoutLeavesNoRoomForASecondLook()
        {
            int looks = 0;

            UiaWaitResult<string> result = UiaWait.Until<string>(
                probe: () => { looks++; return null; },
                satisfied: _ => true,
                timeout: TimeSpan.Zero,
                poll: Rapid);

            Assert.Multiple(() =>
            {
                Assert.That(looks, Is.EqualTo(1), "a zero timeout must mean 'look now', not 'do not look'");
                Assert.That(result.Satisfied, Is.False);
                Assert.That(result.LastSeen, Is.EqualTo("nothing"), "a value that was never there rendered as something");
            });
        }

        [Test]
        public void Until_TakesItsLastLookAtTheDeadlineRatherThanAPollIntervalPastIt()
        {
            // A poll interval a thousand times the timeout. Unclamped, the wait slept the whole interval and
            // only then looked again — so it returned late by the interval, and would have reported as
            // satisfied anything that first came to hold during that sleep.
            UiaWaitResult<string> result = UiaWait.Until<string>(
                probe: () => null,
                satisfied: _ => true,
                timeout: Brief,
                poll: Generous);

            Assert.Multiple(() =>
            {
                Assert.That(result.Satisfied, Is.False);
                Assert.That(result.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)),
                    "the wait served out its poll interval instead of stopping at its deadline");
                Assert.That(result.Polls, Is.InRange(2, 3),
                    "one look at the start and one AT the deadline — a third only when the sleep wakes a timer "
                    + "tick short of it. One means the deadline was never looked at; more means the wait kept "
                    + "looking past it");
            });
        }

        [Test]
        public void Until_AbandonsTheWaitWithItsReasonRatherThanServingOutTheTimeout()
        {
            const string reason = "process 1234 exited with code 1";
            int looks = 0;

            UiaWaitResult<string> result = UiaWait.Until<string>(
                probe: () => { looks++; return null; },
                satisfied: _ => true,
                timeout: Generous,
                poll: Rapid,
                giveUp: () => looks >= 2 ? reason : null);

            Assert.Multiple(() =>
            {
                Assert.That(result.Satisfied, Is.False);
                Assert.That(result.LastSeen, Is.EqualTo(reason), "the reason for giving up did not reach the result");
                Assert.That(result.Elapsed, Is.LessThan(Generous), "the wait served out its timeout instead of giving up");
            });
        }

        [Test]
        public void Until_LetsAProbeFaultOutRatherThanReportingItAsATimeout()
        {
            Assert.That(
                () => UiaWait.Until<string>(
                    probe: () => throw new InvalidOperationException("the element went away"),
                    satisfied: _ => true,
                    timeout: Brief,
                    poll: Rapid),
                Throws.InstanceOf<InvalidOperationException>(),
                "swallowing a probe fault turns a defect into a timeout whose diagnostic names the wrong cause");
        }

        [Test]
        public void Until_TheConditionOverloadReportsTheConditionItSettledOn()
        {
            int looks = 0;

            UiaWaitResult<bool> result = UiaWait.Until(() => ++looks >= 2, Generous, Rapid);

            Assert.Multiple(() =>
            {
                Assert.That(result.Satisfied, Is.True, result.ToString());
                Assert.That(result.Value, Is.True);
                Assert.That(result.Polls, Is.EqualTo(2));
            });
        }

        [Test]
        public void Until_TheConditionOverloadTimesOutSayingFalseRatherThanNothing()
        {
            UiaWaitResult<bool> result = UiaWait.Until(() => false, Brief, Rapid);

            Assert.Multiple(() =>
            {
                Assert.That(result.Satisfied, Is.False);
                Assert.That(result.LastSeen, Is.EqualTo("false"),
                    "a condition that never held has something to report, unlike an absent value");
            });
        }
    }
}
