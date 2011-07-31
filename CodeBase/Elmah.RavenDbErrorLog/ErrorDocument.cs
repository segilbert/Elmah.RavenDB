using System;

namespace Elmah.RavenDbErrorLog
{
    public class ErrorDocument
    {
        public string Id { get; set; }
        public Error Error { get; set; }
    }
}