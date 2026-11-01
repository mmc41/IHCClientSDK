using System;
using System.Linq;
using System.Reflection;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The stub's hand-written answers agree with the operation table. The dispatcher finds an answer by the
    /// generated operation's name and signature, so a typo would quietly leave the operation answered from its
    /// shape; this is what makes that a failure instead.
    /// </summary>
    [TestFixture]
    public class StubAnswerSelfTests
    {
        private static readonly Type[] AnswerClasses = typeof(ControllerStub).Assembly.GetTypes()
            .Where(type => type.IsClass && typeof(IWireAnswers).IsAssignableFrom(type))
            .ToArray();

        [Test]
        public void EveryAnswerMethod_MatchesAGeneratedOperationByNameAndSignature()
        {
            Assert.That(AnswerClasses, Is.Not.Empty, "the scan must find the answer classes it judges");

            var unmatched = AnswerClasses
                .SelectMany(type => AnswerMethods(type).Select(method => (Service: ServiceOf(type), Method: method)))
                .Where(answer => SoapDispatcher.AnsweredOperation(answer.Service, answer.Method) is null)
                .Select(answer => $"{answer.Method.DeclaringType!.Name}.{answer.Method.Name}")
                .ToList();

            Assert.That(unmatched, Is.Empty, "an answer method no generated operation has, by name and signature");
        }

        /// <summary>The operations answered by hand are exactly the rows the table declares typed or request-aware:
        /// one answer per such row, and none for a row answered from its shape.</summary>
        [Test]
        public void TheAnsweredOperations_AreExactlyTheRowsDeclaredTyped()
        {
            var answered = AnswerClasses
                .SelectMany(type => AnswerMethods(type).Select(method => SoapDispatcher.AnsweredOperation(ServiceOf(type), method)))
                .OfType<SdkOperation>()
                .Select(operation => $"{operation.Service}.{operation.WireAction}")
                .ToList();
            var declared = SdkOperations.Rows.Where(row => row.AnswerKind != StubAnswerKind.Shape)
                .Select(row => $"{row.Service}.{row.WireAction}");

            Assert.That(answered, Is.Unique);
            Assert.That(answered, Is.EquivalentTo(declared));
        }

        /// <summary>Positive control: a misspelt answer and one with the wrong signature each match no operation,
        /// so the check above can fail.</summary>
        [Test]
        public void AMisspeltOrMistypedAnswer_MatchesNoOperation()
        {
            var matches = AnswerMethods(typeof(SeededMistakenAnswers))
                .ToDictionary(method => $"{method.Name}({method.GetParameters()[0].ParameterType.Name})",
                    method => SoapDispatcher.AnsweredOperation("ControllerService", method));

            Assert.That(matches, Has.Count.EqualTo(3));
            Assert.That(matches.Where(match => match.Value is not null).Select(match => match.Key),
                Is.EqualTo(new[] { "getStateAsync(inputMessageName1)" }), "only the correctly written answer matches");
        }

        private static MethodInfo[] AnswerMethods(Type answerClass) => [.. SoapDispatcher.AnswerMethodsOf(answerClass)];

        /// <summary>The service an answer class declares, read without constructing its scenario.</summary>
        private static string ServiceOf(Type answerClass) =>
            (string)answerClass.GetProperty(nameof(IWireAnswers.Service))!.GetValue(
                System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(answerClass))!;
    }

    /// <summary>One correct answer beside a misspelt one and one taking the wrong request type.</summary>
    internal static class SeededMistakenAnswers
    {
        internal static System.Threading.Tasks.Task<Ihc.Soap.Controller.outputMessageName1> getStateAsync(Ihc.Soap.Controller.inputMessageName1 request) =>
            throw new NotSupportedException();

        internal static System.Threading.Tasks.Task<Ihc.Soap.Controller.outputMessageName1> getStatAsync(Ihc.Soap.Controller.inputMessageName1 request) =>
            throw new NotSupportedException();

        internal static System.Threading.Tasks.Task<Ihc.Soap.Controller.outputMessageName1> getStateAsync(string request) =>
            throw new NotSupportedException();
    }
}
