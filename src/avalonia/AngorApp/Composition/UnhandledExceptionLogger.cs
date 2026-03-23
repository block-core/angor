using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Serilog;

namespace AngorApp.Composition;

public static class UnhandledExceptionLogger
{
    private static int registrationState;

    public static void Register(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (Interlocked.CompareExchange(ref registrationState, 1, 0) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception;

            if (exception == null)
            {
                logger.Fatal(
                    "Unhandled exception object of type {ExceptionType}. IsTerminating: {IsTerminating}",
                    args.ExceptionObject?.GetType().FullName ?? "Unknown",
                    args.IsTerminating);
                return;
            }

            logger.Fatal(exception, "Unhandled exception. IsTerminating: {IsTerminating}", args.IsTerminating);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.Fatal(args.Exception, "Unobserved task exception.");
            args.SetObserved();
        };

        var dispatcher = Dispatcher.UIThread;
        if (dispatcher != null)
        {
            dispatcher.UnhandledException += (_, args) =>
            {
                logger.Fatal(args.Exception, "Unhandled UI thread exception.");
            };
        }
    }
}
