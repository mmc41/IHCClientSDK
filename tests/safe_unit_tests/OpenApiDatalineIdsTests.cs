using System.Collections.Generic;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Soap.Openapi;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <see cref="OpenAPIService.GetDatalineInputIDs"/> and <see cref="OpenAPIService.GetDatalineOutputIDs"/> against a
    /// faked SOAP layer. A 3.3.44 controller answers an empty list as an array holding one nil item; the resource
    /// service's dataline lists, which these mirror, already read that form as empty.
    /// </summary>
    [TestFixture]
    public class OpenApiDatalineIdsTests
    {
        private static Task<IReadOnlyList<int>> IdsAnswering(bool outputs, WSDatalineResource?[] reply)
        {
            var soap = A.Fake<Ihc.Soap.Openapi.OpenAPIService>();
            A.CallTo(() => soap.getDatalineInputIDsAsync(A<inputMessageName3>._))
                .Returns(Task.FromResult(new outputMessageName3(reply!)));
            A.CallTo(() => soap.getDatalineOutputIDsAsync(A<inputMessageName4>._))
                .Returns(Task.FromResult(new outputMessageName4(reply!)));
            var service = new OpenAPIService(FakeSession.Over(), soap);
            return outputs ? service.GetDatalineOutputIDs() : service.GetDatalineInputIDs();
        }

        [TestCase(false, TestName = "GetDatalineInputIDs_WithTheLoneNilItem_IsEmpty")]
        [TestCase(true, TestName = "GetDatalineOutputIDs_WithTheLoneNilItem_IsEmpty")]
        public async Task TheLoneNilItem_ReadsAsAnEmptyList(bool outputs)
        {
            Assert.That(await IdsAnswering(outputs, [null]), Is.Empty);
        }

        [TestCase(false, TestName = "GetDatalineInputIDs_ReturnsTheIdsInWireOrder")]
        [TestCase(true, TestName = "GetDatalineOutputIDs_ReturnsTheIdsInWireOrder")]
        public async Task TheIds_AreReturnedInWireOrder(bool outputs)
        {
            IReadOnlyList<int> ids = await IdsAnswering(outputs,
                [new WSDatalineResource { resourceID = 23386 }, new WSDatalineResource { resourceID = 23898 }]);

            Assert.That(ids, Is.EqualTo(new[] { 23386, 23898 }));
        }
    }
}
