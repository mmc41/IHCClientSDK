using NUnit.Framework;
using System;
using System.Linq;
using System.IO;
using Ihc;
using System.Threading.Tasks;
using KellermanSoftware.CompareNetObjects;
using Ihc.Envelope;
using System.Text.RegularExpressions;

/// <summary>
/// Unit test of custom serialization.
/// </summary>
namespace Ihc.Tests
{
    [NonParallelizable]
    public class SerializeTest
    { 
         RequestEnvelope<Ihc.Soap.Authentication.inputMessageName2> authenticate1Object = new RequestEnvelope<Ihc.Soap.Authentication.inputMessageName2>(new Ihc.Soap.Authentication.inputMessageName2(new Ihc.Soap.Authentication.WSAuthenticationData()  { username = "a", password="b", application="c"} )); 

         string authenticate1Xml = """
         <soapenv:Envelope xmlns:utcs="utcs" xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
          <soapenv:Header />
          <soapenv:Body>
            <utcs:authenticate1>
              <utcs:password>b</utcs:password>
              <utcs:username>a</utcs:username>
              <utcs:application>c</utcs:application>
            </utcs:authenticate1>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

        ResponseEnvelope<Ihc.Soap.Openapi.outputMessageName11> fwObject = new ResponseEnvelope<Ihc.Soap.Openapi.outputMessageName11>(new Ihc.Soap.Openapi.outputMessageName11(new Ihc.Soap.Openapi.WSVersionInfo() { majorVersion = 3, minorVersion = 3, buildVersion = 7} ));

        string fwXml = """
        <SOAP-ENV:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
        <SOAP-ENV:Body>
            <ns1:getFWVersion1 xsi:type="ns1:WSVersionInfo" xmlns:ns1="utcs">
            <ns1:majorVersion xsi:type="xsd:int">3</ns1:majorVersion>
            <ns1:minorVersion xsi:type="xsd:int">3</ns1:minorVersion>
            <ns1:buildVersion xsi:type="xsd:int">7</ns1:buildVersion>
            </ns1:getFWVersion1>
        </SOAP-ENV:Body>
        </SOAP-ENV:Envelope>
        """;

        ResponseEnvelope<Ihc.Soap.Resourceinteraction.outputMessageName12> getAllDatalineInputsObject = new ResponseEnvelope<Ihc.Soap.Resourceinteraction.outputMessageName12>(new Ihc.Soap.Resourceinteraction.outputMessageName12(new [] {
                                          new Ihc.Soap.Resourceinteraction.WSDatalineResource() {resourceID=2, datalineNumber=1}, 
                                          new Ihc.Soap.Resourceinteraction.WSDatalineResource() {resourceID=4, datalineNumber=3}
                                        }));

        string getAllDatalineInputsXml ="""
        <SOAP-ENV:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                                        <SOAP-ENV:Body>
                                        <ns1:getAllDatalineInputs1 xmlns:ns1="utcs">
                                          <ns1:arrayItem xsi:type="ns1:WSDatalineResource">
                                          <ns1:datalineNumber xsi:type="xsd:int">1</ns1:datalineNumber>
                                          <ns1:resourceID xsi:type="xsd:int">2</ns1:resourceID>
                                          </ns1:arrayItem>
                                          <ns1:arrayItem xsi:type="ns1:WSDatalineResource">
                                          <ns1:datalineNumber xsi:type="xsd:int">3</ns1:datalineNumber>
                                          <ns1:resourceID xsi:type="xsd:int">4</ns1:resourceID>
                                          </ns1:arrayItem>
                                        </ns1:getAllDatalineInputs1>
                                        </SOAP-ENV:Body>
                                        </SOAP-ENV:Envelope>
        """;

        private string normalize(string str) {
          return Regex.Replace(str, @"\s", "");
        }

        #region High level tests
        [Test]
        public void SerializeRequestXml()
        {
          var o = Serialization.SerializeXml<RequestEnvelope<Ihc.Soap.Authentication.inputMessageName2>>(authenticate1Object);
          var normalizedO = normalize(o);
          var normalizedXml = normalize(authenticate1Xml);
          Assert.That(normalizedXml, Is.EqualTo(normalizedO));
        }

        [Test]
        public void DeserializeRequestTest()
        {
          var o = Serialization.DeserializeXml<RequestEnvelope<Ihc.Soap.Authentication.inputMessageName2>>(authenticate1Xml);
          CompareLogic compareLogic = new CompareLogic();
          ComparisonResult compare = compareLogic.Compare(o, authenticate1Object);
          Assert.True(compare.AreEqual);
        }

        
        [Test]
        public void DeserializeResponseXml()
        {
          var o = Serialization.DeserializeXml<ResponseEnvelope<Ihc.Soap.Openapi.outputMessageName11>>(fwXml);
          CompareLogic compareLogic = new CompareLogic();
          ComparisonResult compare = compareLogic.Compare(o, fwObject);
          Assert.True(compare.AreEqual);
        }

        
        [Test]
        public void DeserializeArrayResponseXml()
        {
          var o = Serialization.DeserializeXml<ResponseEnvelope<Ihc.Soap.Resourceinteraction.outputMessageName12>>(getAllDatalineInputsXml);
          CompareLogic compareLogic = new CompareLogic();
          ComparisonResult compare = compareLogic.Compare(o, getAllDatalineInputsObject);
          Assert.True(compare.AreEqual);
        }
        #endregion
        
        #region Low level tests
        [Test]
        public void GetOrCreateSerializer_ShouldReuseSerializer_ForSameType()
        {
            var type = typeof(RequestEnvelope<Ihc.Soap.Authentication.inputMessageName2>);
            var attrs = new System.Xml.Serialization.XmlAttributeOverrides();

            var serializer1 = GetOrCreateSerializerAccessor(type, attrs, null);
            var serializer2 = GetOrCreateSerializerAccessor(type, attrs, null);

            Assert.That(serializer2, Is.SameAs(serializer1), "Serializer should be reused for same type and attributes");
        }

        [Test]
        public void GetOrCreateSerializer_ShouldReuseSerializer_ForSameTypeWithoutAttributes()
        {
            var type = typeof(ResponseEnvelope<Ihc.Soap.Openapi.outputMessageName11>);

            var serializer1 = GetOrCreateSerializerAccessor(type, null, null);
            var serializer2 = GetOrCreateSerializerAccessor(type, null, null);

            Assert.That(serializer2, Is.SameAs(serializer1), "Serializer should be reused for same type without attributes");
        }

        [Test]
        public void GetOrCreateSerializer_ShouldReuseSerializer_ForSameTypeWithExtraTypes()
        {
            var type = typeof(ResponseEnvelope<Ihc.Soap.Resourceinteraction.outputMessageName12>);
            var attrs = new System.Xml.Serialization.XmlAttributeOverrides();
            var extraTypes = new Type[] { typeof(Ihc.Soap.Resourceinteraction.WSDatalineResource) };

            var serializer1 = GetOrCreateSerializerAccessor(type, attrs, extraTypes);
            var serializer2 = GetOrCreateSerializerAccessor(type, attrs, extraTypes);

            Assert.That(serializer2, Is.SameAs(serializer1), "Serializer should be reused for same type with extra types");
        }

        [Test]
        public void GetOrCreateSerializer_ShouldCreateDifferentSerializers_ForDifferentTypes()
        {
            var type1 = typeof(RequestEnvelope<Ihc.Soap.Authentication.inputMessageName2>);
            var type2 = typeof(ResponseEnvelope<Ihc.Soap.Openapi.outputMessageName11>);
            var attrs = new System.Xml.Serialization.XmlAttributeOverrides();

            var serializer1 = GetOrCreateSerializerAccessor(type1, attrs, null);
            var serializer2 = GetOrCreateSerializerAccessor(type2, attrs, null);

            Assert.That(serializer2, Is.Not.SameAs(serializer1), "Different types should have different serializers");
        }

        [Test]
        public void GetOrCreateSerializer_ShouldCreateDifferentSerializers_ForSameTypeWithDifferentAttributes()
        {
            var type = typeof(RequestEnvelope<Ihc.Soap.Authentication.inputMessageName2>);

            var serializer1 = GetOrCreateSerializerAccessor(type, null, null);
            var serializer2 = GetOrCreateSerializerAccessor(type, new System.Xml.Serialization.XmlAttributeOverrides(), null);

            Assert.That(serializer2, Is.Not.SameAs(serializer1), "Same type with different attribute configurations should have different serializers");
        }

        [Test]
        public void GetOrCreateSerializer_ShouldCreateDifferentSerializers_ForSameTypeWithDifferentExtraTypes()
        {
            var type = typeof(ResponseEnvelope<Ihc.Soap.Resourceinteraction.outputMessageName12>);
            var attrs = new System.Xml.Serialization.XmlAttributeOverrides();
            var extraTypes1 = new Type[] { typeof(Ihc.Soap.Resourceinteraction.WSDatalineResource) };
            var extraTypes2 = new Type[] { typeof(Ihc.Soap.Authentication.WSAuthenticationData) };

            var serializer1 = GetOrCreateSerializerAccessor(type, attrs, extraTypes1);
            var serializer2 = GetOrCreateSerializerAccessor(type, attrs, extraTypes2);

            Assert.That(serializer2, Is.Not.SameAs(serializer1), "Same type with different extra types should have different serializers");
        }

        [Test]
        public void GetOrCreateSerializer_ShouldHandleConcurrentAccess()
        {
            var type = typeof(RequestEnvelope<Ihc.Soap.Authentication.inputMessageName2>);
            var attrs = new System.Xml.Serialization.XmlAttributeOverrides();
            var serializers = new System.Xml.Serialization.XmlSerializer[10];

            Parallel.For(0, 10, i =>
            {
                serializers[i] = GetOrCreateSerializerAccessor(type, attrs, null);
            });

            var firstSerializer = serializers[0];
            for (int i = 1; i < 10; i++)
            {
                Assert.That(serializers[i], Is.SameAs(firstSerializer), $"All concurrent accesses should return the same serializer instance (index {i})");
            }
        }

        private System.Xml.Serialization.XmlSerializer GetOrCreateSerializerAccessor(Type type, System.Xml.Serialization.XmlAttributeOverrides attrs, Type[] extraTypes)
        {
            var method = typeof(Serialization).GetMethod("GetOrCreateSerializer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (System.Xml.Serialization.XmlSerializer)method.Invoke(null, new object[] { type, attrs, extraTypes });
        }
        #endregion
    }
}