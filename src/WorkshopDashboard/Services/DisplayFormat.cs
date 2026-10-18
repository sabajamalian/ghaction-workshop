using System.Globalization;

namespace WorkshopDashboard.Services;

public static class DisplayFormat
{
    // 15 becomes "15 minutes", 60 becomes "1 hour", and 135 becomes "2 hours 15 minutes".
    public static string Duration(int minutes)
    {
        if (minutes < 60)
        {
            return minutes == 1 ? "1 minute" : string.Create(CultureInfo.InvariantCulture, $"{minutes} minutes");
        }

        int hours = minutes / 60;
        int remainder = minutes % 60;
        string hoursText = hours == 1 ? "1 hour" : string.Create(CultureInfo.InvariantCulture, $"{hours} hours");
        return remainder == 0 ? hoursText : $"{hoursText} {Duration(remainder)}";
    }

    public static string LabCount(int count)
    {
        return count == 1 ? "1 lab" : string.Create(CultureInfo.InvariantCulture, $"{count} labs");
    }
}
