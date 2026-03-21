namespace IRIS.UI.Helpers
{
    public static class DateTimeDisplayHelper
    {
        private static readonly TimeZoneInfo ManilaTimeZone = ResolveManilaTimeZone();

        public static DateTime ToManilaFromUtc(DateTime utcDateTime)
        {
            var value = utcDateTime.Kind == DateTimeKind.Utc
                ? utcDateTime
                : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(value, ManilaTimeZone);
        }

        public static DateTime? ToManilaFromUtc(DateTime? utcDateTime)
        {
            return utcDateTime.HasValue ? ToManilaFromUtc(utcDateTime.Value) : null;
        }

        private static TimeZoneInfo ResolveManilaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            }
            catch
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                }
                catch
                {
                    return TimeZoneInfo.Local;
                }
            }
        }
    }
}
