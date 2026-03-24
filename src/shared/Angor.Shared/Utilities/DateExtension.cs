namespace Angor.Shared.Utilities;

public static class DateExtension
{
    public static string FormatDate(this DateTime dateTime)
    {
        return dateTime.ToString("dd MMMM yyyy");
    }

    public static string FormatDateTime(this DateTime dateTime)
    {
        return dateTime.ToString("dd MMMM yyyy HH:mm");
    }
}
