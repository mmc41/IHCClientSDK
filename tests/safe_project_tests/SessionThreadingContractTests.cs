using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// crudarch D04: the <see cref="ProjectDocumentSession"/> threading contract after the switch from
    /// thread-affinity to lock-serialization. These are the two cross-thread tests the redesign explicitly
    /// requires (its deliverable, not incidental multithreading coverage): (1) the off-thread-reader shape — a
    /// worker thread reads <c>Current</c>/state while the owner thread edits, with no throw and no torn read; (2) the
    /// D04(a) event contract — <c>Changed</c>/<c>StateChanged</c> are raised synchronously on the thread that
    /// performed the state change, never marshalled or deferred.
    /// </summary>
    public class SessionThreadingContractTests
    {
        private static readonly TimeSpan WorkerTimeout = TimeSpan.FromSeconds(5);

        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        [Test]
        public async Task WorkerThreadReads_WhileOwnerThreadEdits_NoThrowNoTornRead()
        {
            Project project = await Load("project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            int baseCount = session.Current!.Groups.Count;

            // The off-thread reader shape: a worker samples Current + state while the owner thread applies
            // edits. Every sample must be a committed snapshot — group counts only grow here, so a decreasing
            // observation would be a torn read.
            using var stop = new CancellationTokenSource();
            using var workerReady = new ManualResetEventSlim();
            using var startSampling = new ManualResetEventSlim();
            using var observationsDuringEdits = new CountdownEvent(2);
            var observedCounts = new ConcurrentQueue<int>();
            int editingPhase = 0;
            Exception? workerFailure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    workerReady.Set();
                    if (!startSampling.Wait(WorkerTimeout))
                        throw new TimeoutException("owner did not release the sampling barrier");
                    while (!stop.IsCancellationRequested)
                    {
                        if (session.Current is { } snapshot)
                        {
                            observedCounts.Enqueue(snapshot.Groups.Count);
                            if (Volatile.Read(ref editingPhase) == 1
                                && observationsDuringEdits.CurrentCount > 0)
                                observationsDuringEdits.Signal();
                        }
                        bool _ = session.IsDirty && session.CanUndo && session.UndoLabel is not null;
                    }
                }
                catch (Exception ex)
                {
                    workerFailure = ex;
                }
            }) { IsBackground = true };
            worker.Start();
            Assert.That(workerReady.Wait(WorkerTimeout), Is.True, "the sampling worker reached its start barrier");
            startSampling.Set();
            const int edits = 25;
            session.Apply(new AddLocality("Room 0"));
            Volatile.Write(ref editingPhase, 1);
            for (int i = 1; i < edits; i++)
            {
                session.Apply(new AddLocality("Room " + i));
            }
            Assert.That(observationsDuringEdits.Wait(WorkerTimeout), Is.True,
                "the worker must complete concurrent observations during the edit phase");
            Volatile.Write(ref editingPhase, 2);
            stop.Cancel();
            bool joined = worker.Join(WorkerTimeout);
            int[] observations = observedCounts.ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(joined, Is.True, "the sampling worker must stop within the bounded join");
                Assert.That(workerFailure, Is.Null,
                    "a worker-thread read must not throw under lock-serialization (D04)");
                Assert.That(observations, Has.Length.GreaterThanOrEqualTo(2),
                    "the contract requires real worker observations; an empty sample must not pass vacuously");
                Assert.That(observations.Zip(observations.Skip(1)).All(p => p.Second >= p.First), Is.True,
                    "observed group counts never decrease — each sample is a committed snapshot, never torn");
                Assert.That(session.Current!.Groups.Count, Is.EqualTo(baseCount + edits),
                    "all owner-thread edits committed");
            });
        }

        [Test]
        public async Task Events_AreRaisedSynchronouslyOnTheMutatingThread()
        {
            Project project = await Load("project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            int changedOn = 0;
            int stateChangedOn = 0;
            int stateChangedCount = 0;
            session.Changed += (_, _) => Volatile.Write(ref changedOn, Environment.CurrentManagedThreadId);
            session.StateChanged += (_, _) =>
            {
                Volatile.Write(ref stateChangedOn, Environment.CurrentManagedThreadId);
                Interlocked.Increment(ref stateChangedCount);
            };

            session.Apply(new AddLocality("Owner room"));
            Assert.That(changedOn, Is.EqualTo(Environment.CurrentManagedThreadId),
                "an owner-thread mutation raises Changed synchronously on the owner thread");

            // D04(a): a worker-thread mutation raises the events ON the worker, before the mutating call
            // returns — not marshalled to another thread, not deferred.
            int workerId = 0;
            int changedAtApplyReturn = 0;
            int stateChangedAtMarkSavedReturn = 0;
            int stateChangedRaisedByMarkSaved = 0;
            Exception? workerFailure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    workerId = Environment.CurrentManagedThreadId;
                    session.Apply(new AddLocality("Worker room"));
                    changedAtApplyReturn = Volatile.Read(ref changedOn);
                    int stateChangesBeforeMarkSaved = Volatile.Read(ref stateChangedCount);
                    Volatile.Write(ref stateChangedOn, 0);
                    session.MarkSaved(session.Current!);
                    stateChangedAtMarkSavedReturn = Volatile.Read(ref stateChangedOn);
                    stateChangedRaisedByMarkSaved = Volatile.Read(ref stateChangedCount) - stateChangesBeforeMarkSaved;
                }
                catch (Exception ex)
                {
                    workerFailure = ex;
                }
            }) { IsBackground = true };
            worker.Start();
            bool joined = worker.Join(WorkerTimeout);

            Assert.Multiple(() =>
            {
                Assert.That(joined, Is.True, "the mutation worker must stop within the bounded join");
                Assert.That(workerFailure, Is.Null,
                    "a worker-thread mutation must not throw under lock-serialization (D04)");
                Assert.That(changedAtApplyReturn, Is.EqualTo(workerId),
                    "Changed had already run on the worker when its Apply returned (synchronous, on the mutating thread)");
                Assert.That(stateChangedAtMarkSavedReturn, Is.EqualTo(workerId),
                    "StateChanged had already run on the worker when its MarkSaved returned");
                Assert.That(stateChangedRaisedByMarkSaved, Is.GreaterThanOrEqualTo(1),
                    "MarkSaved itself must synchronously raise StateChanged; the preceding Apply cannot satisfy this assertion");
            });
        }
    }
}
