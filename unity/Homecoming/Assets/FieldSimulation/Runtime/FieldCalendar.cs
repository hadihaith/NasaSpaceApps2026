using System;

namespace Homecoming.Field
{
    public static class FieldCalendar
    {
        // January 2026 is a synthetic campaign date. Weather repeats the pinned 1991–2020 normals.
        public const int StartMonth = (2026 - 1) * 12;
        public const int MonthsInPreview = 12;
        public static int Index(DateTime date) => (date.Year - 1) * 12 + date.Month - 1;
        public static DateTime Date(int monthIndex) => new DateTime(1, 1, 1).AddMonths(monthIndex);
    }
}
