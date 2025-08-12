namespace AngorApp.Core;

public class AppHelper
{
#if DEBUG
    public static bool IsDebug => true;
#else
    public static bool IsDebug => false;
#endif
}