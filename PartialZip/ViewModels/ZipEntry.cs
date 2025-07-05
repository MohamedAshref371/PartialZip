using System;

namespace PartialZip
{
    public class ZipEntry
    {
        public string FileName { get; set; }

        public string CompressedSize { get; set; }

        public string UncompressedSize { get; set; }

        public string LastModified { get; set; }

        public static DateTime DosDateTimeToDateTime(ushort date, ushort time)
        {
            int year = ((date >> 9) & 0x7F) + 1980;
            int month = (date >> 5) & 0x0F;
            int day = date & 0x1F;

            int hour = (time >> 11) & 0x1F;
            int minute = (time >> 5) & 0x3F;
            int second = (time & 0x1F) * 2;

            try
            {
                return new DateTime(year, month, day, hour, minute, second);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        public static string FormatSize(uint bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

    }
}
