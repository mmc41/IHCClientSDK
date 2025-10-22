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
    [TestFixture]
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

    string getAllDatalineInputsXml = """
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
        

        string getUsersResponseXml = """
          <SOAP-ENV:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
            <SOAP-ENV:Body> 
             <ns1:getUsers1 xmlns:ns1="utcs"> 
               <ns1:arrayItem xsi:type="ns1:WSUser">
                <ns1:createdDate xsi:type="ns1:WSDate">
                  <ns1:day xsi:type="xsd:int">1</ns1:day>
                  <ns1:hours xsi:type="xsd:int">20</ns1:hours>
                  <ns1:minutes xsi:type="xsd:int">54</ns1:minutes>
                  <ns1:seconds xsi:type="xsd:int">24</ns1:seconds>
                  <ns1:monthWithJanuaryAsOne xsi:type="xsd:int">10</ns1:monthWithJanuaryAsOne>
                  <ns1:year xsi:type="xsd:int">2019</ns1:year>
                </ns1:createdDate>
                <ns1:loginDate xsi:type="ns1:WSDate">
                  <ns1:day xsi:type="xsd:int">17</ns1:day>
                  <ns1:hours xsi:type="xsd:int">18</ns1:hours>
                  <ns1:minutes xsi:type="xsd:int">31</ns1:minutes>
                  <ns1:seconds xsi:type="xsd:int">54</ns1:seconds> 
                  <ns1:monthWithJanuaryAsOne xsi:type="xsd:int">10</ns1:monthWithJanuaryAsOne>
                  <ns1:year xsi:type="xsd:int">2025</ns1:year> 
                </ns1:loginDate>
                <ns1:username xsi:type="xsd:string">testusername</ns1:username>
                <ns1:password xsi:type="xsd:string">testpassword</ns1:password>
                <ns1:email xsi:type="xsd:string" xsi:nil="true"></ns1:email>
                <ns1:firstname xsi:type="xsd:string">test forname</ns1:firstname> 
                <ns1:lastname xsi:type="xsd:string">test surname</ns1:lastname>
                <ns1:phone xsi:type="xsd:string" xsi:nil="true"></ns1:phone>
                <ns1:group xsi:type="ns1:WSUserGroup">
                  <ns1:type xsi:type="xsd:string">text.usermanager.group_administrators</ns1:type>
                </ns1:group>
                <ns1:project xsi:type="xsd:string" xsi:nil="true">
                </ns1:project>
              </ns1:arrayItem>
             </ns1:getUsers1>
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
          Assert.That(compare.AreEqual, Is.True);
        }

        
        [Test]
        public void DeserializeResponseXml()
        {
          var o = Serialization.DeserializeXml<ResponseEnvelope<Ihc.Soap.Openapi.outputMessageName11>>(fwXml);
          CompareLogic compareLogic = new CompareLogic();
          ComparisonResult compare = compareLogic.Compare(o, fwObject);
          Assert.That(compare.AreEqual, Is.True);
        }

        
        [Test]
        public void DeserializeArrayResponseXml()
        {
          var o = Serialization.DeserializeXml<ResponseEnvelope<Ihc.Soap.Resourceinteraction.outputMessageName12>>(getAllDatalineInputsXml);
          CompareLogic compareLogic = new CompareLogic();
          ComparisonResult compare = compareLogic.Compare(o, getAllDatalineInputsObject);
          Assert.That(compare.AreEqual, Is.True);
        }
        #endregion
        
        #region Low level tests
        [Test]
        public void GetOrCreateSerializer_ShouldReuseSerializer_ForSameType()
        {
            var type = typeof(RequestEnvelope<Ihc.Soap.Authentication.inputMessageName2>);
            var attrs = new System.Xml.Serialization.XmlAttributeOverrides();

            var serializer1 = GetOrCreateSerializerAccessor(type, attrs, null!);
            var serializer2 = GetOrCreateSerializerAccessor(type, attrs, null!);

            Assert.That(serializer2, Is.SameAs(serializer1), "Serializer should be reused for same type and attributes");
        }

        [Test]
        public void GetOrCreateSerializer_ShouldReuseSerializer_ForSameTypeWithoutAttributes()
        {
            var type = typeof(ResponseEnvelope<Ihc.Soap.Openapi.outputMessageName11>);

            var serializer1 = GetOrCreateSerializerAccessor(type, null!, null!);
            var serializer2 = GetOrCreateSerializerAccessor(type, null!, null!);

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

            var serializer1 = GetOrCreateSerializerAccessor(type1, attrs, null!);
            var serializer2 = GetOrCreateSerializerAccessor(type2, attrs, null!);

            Assert.That(serializer2, Is.Not.SameAs(serializer1), "Different types should have different serializers");
        }

        [Test]
        public void GetOrCreateSerializer_ShouldCreateDifferentSerializers_ForSameTypeWithDifferentAttributes()
        {
            var type = typeof(RequestEnvelope<Ihc.Soap.Authentication.inputMessageName2>);

            var serializer1 = GetOrCreateSerializerAccessor(type, null!, null!);
            var serializer2 = GetOrCreateSerializerAccessor(type, new System.Xml.Serialization.XmlAttributeOverrides(), null!);

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
                serializers[i] = GetOrCreateSerializerAccessor(type, attrs, null!);
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
      var result = method!.Invoke(null, new object?[] { type, attrs, extraTypes });
      return (System.Xml.Serialization.XmlSerializer)result!;
    }
        #endregion
    
        #region Bug tests
        /// <summary>
        /// This bug is due to the controller provided WSDL being inconsistent with the XML returned by the controller.
        /// The WSDL defines the internal WSDate as a sequence in an order which is different from what the controller provides (the response xml in the test).
        /// Thus the generate soap class for WSDate has order set [System.Xml.Serialization.XmlElementAttribute(Order=xxx)] which will cause deserialization to set 
        /// fields out of order to 0. 
        /// 
        /// Fix: The WSDL or the generated soap classes must be fixed by replacing <xsd:sequence> with <xsd:all> and regenerating or by removing "Order=xxx" from XmlElementAttribute.
        /// </summary>
        [Test]
        public void DeserializeUserXmlGotWrongDate()
        {
          var o = Serialization.DeserializeXml<RequestEnvelope<Ihc.Soap.Usermanager.outputMessageName2>>(getUsersResponseXml);

          Assert.That(o.Body.getUsers1.Length, Is.EqualTo(1));

          // This worked always but included to be sure of no regressions
          Assert.That(o.Body.getUsers1[0].username, Is.EqualTo("testusername"));
          Assert.That(o.Body.getUsers1[0].password, Is.EqualTo("testpassword"));
          
          // Bug check: Year and month are correctly deserialized
          Assert.That(o.Body.getUsers1[0].createdDate.monthWithJanuaryAsOne, Is.EqualTo(10));
          Assert.That(o.Body.getUsers1[0].createdDate.day, Is.EqualTo(1));
          Assert.That(o.Body.getUsers1[0].createdDate.year, Is.EqualTo(2019));
          Assert.That(o.Body.getUsers1[0].loginDate.monthWithJanuaryAsOne, Is.EqualTo(10));
          Assert.That(o.Body.getUsers1[0].loginDate.day, Is.EqualTo(17));
          Assert.That(o.Body.getUsers1[0].loginDate.year, Is.EqualTo(2025));
        }

        #endregion
    
    }
}