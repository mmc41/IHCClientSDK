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

   public const int WEB_EXCEPTION_ERROR_BASE = 10000;
 } 
}