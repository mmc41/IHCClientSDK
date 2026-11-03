using System;

namespace Ihc {
  /// <summary>
  /// IHC communication related exception with an HTTP/IHC/Communication error code.
  /// </summary>
  public sealed class ErrorWithCodeException : Exception
  {
    public readonly int ErrorCode;

    public ErrorWithCodeException()
    {
    }

    public ErrorWithCodeException(int errorCode, string message)
        : base(message)
    {
      this.ErrorCode = errorCode;
    }

    public ErrorWithCodeException(int errorCode, string message, Exception inner)
        : base(message, inner)
    {
         this.ErrorCode = errorCode;
    }

    public override string ToString() => ErrorCode + " : " + this.Message;
 };

 /// <summary>
 /// Standard IHC/HTTP/Communication error codes.
 /// </summary>
 public class Errors
 {
   public const int XML_FORMAT_ERROR = 1000;

   public const int XML_LOOKUP_ERROR = 1001;
   public const int XML_SERIALIZE_ERROR = 1002;

   public const int XML_DESERIALIZE_ERROR = 1003;

   public const int HTTP_CLIENT_SIDE_INTERNAL_ERROR = 1004;

   public const int HTTP_UNEXPECTED_CONTENT_ERROR = 1005;

   public const int LOGIN_FAILED_DUE_TO_CONNECTION_RESTRUCTIONS_ERROR = 1006;

   public const int LOGIN_FAILED_DUE_TO_INSUFFICIENT_USER_RIGHTS_ERROR = 1007;

   public const int LOGIN_FAILED_DUE_TO_ACCOUNT_INVALID_ERROR = 1008;

   public const int LOGIN_UNKNOWN_ERROR = 1009;

   public const int FEATURE_NOT_IMPLEMENTED= 1010;

   /// <summary>
   /// A call targets a controller-stub endpoint for which no stub is registered in this process. It is refused
   /// rather than sent, so a stub endpoint never reaches a controller.
   /// </summary>
   public const int CONTROLLER_STUB_NOT_REGISTERED = 1011;

   /// <summary>
   /// A call targets a real controller endpoint while a controller stub is registered in this process, which
   /// marks it as a stub process that must reach no controller.
   /// </summary>
   public const int CONTROLLER_SOCKET_REFUSED_WHILE_STUBBED = 1012;

   /// <summary>
   /// A controller stub threw or returned no response: a defect in the stub, never a controller's answer.
   /// </summary>
   public const int CONTROLLER_STUB_FAULTED = 1013;

   /// <summary>
   /// A stub endpoint or a <c>--controller-stub</c> value the rules refuse: a chosen registration key that is
   /// not <c>mock://</c>, or a loopback address that is not plain http to a loopback IP literal with an explicit
   /// port and a token.
   /// </summary>
   public const int CONTROLLER_STUB_ENDPOINT_INVALID = 1014;

   /// <summary>
   /// The process at a loopback stub address did not prove it is the stub it was registered as: no token echo
   /// when registered, or a response without the stub header.
   /// </summary>
   public const int CONTROLLER_STUB_IDENTITY_FAILED = 1015;

   /// <summary>
   /// A <c>mock://</c> key was registered a second time in one process. A key is single-use, live or disposed,
   /// so two fixtures can never swap each other's stub.
   /// </summary>
   public const int CONTROLLER_STUB_ENDPOINT_IN_USE = 1016;

   /// <summary>
   /// A controller reply carried a SOAP 1.1 fault, whatever its HTTP status; the message carries the fault's
   /// <c>faultcode</c> and <c>faultstring</c>. A <c>Logon Failed</c> fault has a code of its own,
   /// <see cref="CONTROLLER_LOGON_FAILED"/>.
   /// </summary>
   public const int CONTROLLER_SOAP_FAULT = 1017;

   /// <summary>
   /// A controller reply carried the SOAP fault <c>Logon Failed</c>, which a controller answers a call without a
   /// working session with; logging in again is the remedy. The message carries the fault's code and text.
   /// </summary>
   public const int CONTROLLER_LOGON_FAILED = 1018;

   /// <summary>
   /// A write was sent, but the transport failed before the controller answered, so whether it applied is unknown:
   /// the captured outcome of a wired-network save that changes the address, after which the controller restarts
   /// without replying. Reconnect, at the new address if one was set, and read the settings back before retrying.
   /// The transport's own error is the inner exception.
   /// </summary>
   public const int CONTROLLER_WRITE_UNCONFIRMED = 1019;

   public const int WEB_EXCEPTION_ERROR_BASE = 10000;
 } 
}