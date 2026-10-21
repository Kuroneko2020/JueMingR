using System;
using System.Reflection;

namespace JueMingR.Testing
{
    // Only explicit assertion exits may be isolated by the workload runner.
    // Loading, initialization and arbitrary InvalidOperationException stay fatal.
    internal sealed class BusinessAssertionException : Exception
    {
        internal BusinessAssertionException(string message) : base(message) { }
    }
    internal static class BusinessAssertionProtocol
    {
        internal static bool IsAssertion(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null)
                exception = exception.InnerException;
            return exception is BusinessAssertionException;
        }
        internal static int Fail()
        {
            string context = Environment.GetEnvironmentVariable("JUEMINGR_WORKLOAD_ASSERTION_CONTEXT");
            if (!String.IsNullOrEmpty(context)) Console.WriteLine("JMR-ASSERTION-V1|" + context);
            return 3;
        }
        internal static int Fault(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null)
                exception = exception.InnerException;
            string context = Environment.GetEnvironmentVariable("JUEMINGR_WORKLOAD_ASSERTION_CONTEXT");
            if (!String.IsNullOrEmpty(context)) Console.WriteLine("JMR-FAULT-V1|" + context + "|" + exception.GetType().FullName);
            return 1;
        }
    }
}
