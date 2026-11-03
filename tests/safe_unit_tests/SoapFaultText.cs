namespace Ihc.Tests
{
    /// <summary>
    /// SOAP 1.1 fault envelopes as a controller's SOAP stack writes them: a <c>Fault</c> in the body carrying an
    /// unqualified <c>faultcode</c> and <c>faultstring</c>. The texts are synthetic except <c>Logon Failed</c>, the one
    /// fault text on record from a controller, answered to a call without a working session.
    /// </summary>
    internal static class SoapFaultText
    {
        internal static string Of(string faultCode, string faultString) =>
            "<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
            "<SOAP-ENV:Body><SOAP-ENV:Fault>" +
            $"<faultcode>{faultCode}</faultcode><faultstring>{faultString}</faultstring>" +
            "</SOAP-ENV:Fault></SOAP-ENV:Body></SOAP-ENV:Envelope>";

        internal static readonly string LogonFailed = Of("SOAP-ENV:Server", "Logon Failed");
    }
}
