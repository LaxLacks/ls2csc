using System;
namespace System
{
    public class Exception
    {
        public Exception()
        {

        }

        public Exception(string message)
        {
            Message = message;
        }

        public Exception(string message, Exception innerException)
        {
            Message = message;
            InnerException = innerException;
        }

        public virtual string Message { get; set; }
        public Exception InnerException { get; private set; }
        public virtual string Source { get; set; }

        /*
        public virtual IDictionary Data { get; }
        public virtual string HelpLink { get; set; }
        public int HResult { get; protected set; }
        public virtual string StackTrace { get; }
        public MethodBase TargetSite { get; }
        /**/
    }

    public class EventArgs
    {
        public EventArgs()
        {
            
        }

        public static EventArgs Empty = new EventArgs();
    }

    namespace Windows
    {
        public enum RoutingStrategy
        {
            Bubble,
            Direct,
            Tunnel,
        }

        public sealed class RoutedEvent
        {
            public LavishScript2.Type HandlerType { get; private set; }
            public string Name { get; private set; }
            public LavishScript2.Type OwnerType { get; private set; }
            public RoutingStrategy RoutingStrategy { get; private set; }
        }

        public class RoutedEventArgs : System.EventArgs
        {
            public RoutedEventArgs()
            {

            }
            public RoutedEventArgs(RoutedEvent routedEvent)
            {
                this.RoutedEvent = routedEvent;
            }
            public RoutedEventArgs(RoutedEvent routedEvent, object source)
            {
                this.RoutedEvent = routedEvent;
                this.Source = source;
                _OriginalSource = source;
            }

            public bool Handled { get; set; }

            object _OriginalSource;
            public object OriginalSource { get { return _OriginalSource; } }

            public RoutedEvent RoutedEvent { get; set; }
            public object Source { get; set; }
        }
    }
}
namespace LavishScript2
{
    public class ByteCodeException : System.Exception
    {
        public ByteCodeException()
        {

        }
    }
}
