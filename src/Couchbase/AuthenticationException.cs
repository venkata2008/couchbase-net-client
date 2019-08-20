using System;

namespace Couchbase
{
    public class AuthenticationException : CouchbaseException
    {
        public AuthenticationException()
        {
            var dd = "ss";
        }

        public AuthenticationException(string message)
            : base(message)
        {
        }

        public AuthenticationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
