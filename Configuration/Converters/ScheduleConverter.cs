using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration.Converters;
public class ScheduleConverter : JsonConverter<DateTime[]?>
{
    [ThreadStatic]
    private static StringBuilder? _sb;
    public override DateTime[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        _sb ??= new StringBuilder(24);
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected token: Null or Start Array to read schedule.");
        List<DateTime> rtn = new List<DateTime>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;
            if (reader.TokenType != JsonTokenType.String || reader.GetString() is not { Length: > 0 } str)
                throw new JsonException("Expected token: String to read schedule element.");
            int yr = str.IndexOf('-');
            if (yr <= 0 || yr + 1 == str.Length)
                Throw();
            int month = str.IndexOf('-', yr < 0 ? 0 : yr + 1);
            if (month >= 0 && (month + 1 == str.Length || month - 1 == yr))
                Throw();
            int day = str.IndexOf(' ', month + 1);
            if (month < 0)
            {
                month = yr;
                yr = -1;
            }
            else if (day < 0 || day + 1 == str.Length || day - 1 == month)
                Throw();
            int hr = str.IndexOf(':', day + 1);
            if (hr < 0 || hr + 1 == str.Length || hr - 1 == day)
                Throw();
            int min = str.IndexOf(':', hr + 1);
            if (min >= 0 && (min + 1 == str.Length || min - 1 == hr))
                Throw();
            int yearVal = 1;
            int secondVal = 0;
            if (yr >= 0 && !int.TryParse(str.Substring(0, yr), NumberStyles.Number, CultureInfo.InvariantCulture, out yearVal) ||
                !int.TryParse(str.Substring(yr < 0 ? 0 : (yr + 1), yr < 0 ? month : (month - yr - 1)), NumberStyles.Number, CultureInfo.InvariantCulture, out int monthVal) ||
                !int.TryParse(str.Substring(month + 1, day - month - 1), NumberStyles.Number, CultureInfo.InvariantCulture, out int dayVal) ||
                !int.TryParse(str.Substring(day + 1, hr - day - 1), NumberStyles.Number, CultureInfo.InvariantCulture, out int hourVal) ||
                !int.TryParse(str.Substring(hr + 1, (min >= 0 ? min : str.Length) - hr - 1), NumberStyles.Number, CultureInfo.InvariantCulture, out int minuteVal) ||
                min >= 0 && !int.TryParse(str.Substring(min + 1), NumberStyles.Number, CultureInfo.InvariantCulture, out secondVal))
            {
                throw new JsonException("Invalid date string in schedule element, failed to parse a datetime component: \"" + str + "\".");
            }

            try
            {
                rtn.Add(new DateTime(yearVal, monthVal, dayVal, hourVal, minuteVal, secondVal));
            }
            catch (ArgumentOutOfRangeException ex)
            {
                string str2;
                try
                {
                    _sb.Append(yearVal.ToString("D4", CultureInfo.InvariantCulture)).Append('-');
                    _sb.Append(monthVal.ToString("D2", CultureInfo.InvariantCulture)).Append('-');
                    _sb.Append(dayVal.ToString("D2", CultureInfo.InvariantCulture)).Append(' ');
                    _sb.Append(hourVal.ToString("D2", CultureInfo.InvariantCulture)).Append(':');
                    _sb.Append(minuteVal.ToString("D2", CultureInfo.InvariantCulture)).Append(':');
                    _sb.Append(secondVal.ToString("D2", CultureInfo.InvariantCulture));
                    str2 = _sb.ToString();
                }
                finally
                {
                    _sb.Clear();
                }
                throw new JsonException("Invalid date string in schedule element, datetime component out of range: '" + str2 + "' (\"" + str + "\").", ex);
            }
        }

        void Throw()
        {
            DateTime now = DateTime.Now;
            string str;
            try
            {
                _sb!.Append(now.Year.ToString("D4", CultureInfo.InvariantCulture)).Append('-');
                _sb.Append(now.Month.ToString("D2", CultureInfo.InvariantCulture)).Append('-');
                _sb.Append(now.Day.ToString("D2", CultureInfo.InvariantCulture)).Append(' ');
                _sb.Append(now.Hour.ToString("D2", CultureInfo.InvariantCulture)).Append(':');
                _sb.Append(now.Minute.ToString("D2", CultureInfo.InvariantCulture)).Append("[:");
                _sb.Append(now.Second.ToString("D2", CultureInfo.InvariantCulture)).Append(']');
                str = _sb.ToString();
            }
            finally
            {
                _sb?.Clear();
            }

            throw new JsonException("Invalid date string in schedule element, expected '" + str + "' (YYYY-MM-DD hh:mm:ss).");
        }

        return rtn.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, DateTime[]? value, JsonSerializerOptions options)
    {
        _sb ??= new StringBuilder(24);
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        int minYear = DateTime.Now.Subtract(TimeSpan.FromHours(14d)).Year;
        for (int i = 0; i < value.Length; ++i)
        {
            try
            {
                DateTime current = value[i];
                _sb.Append((current.Year < minYear ? minYear : current.Year).ToString("D4", CultureInfo.InvariantCulture)).Append('-');
                _sb.Append(current.Month.ToString("D2", CultureInfo.InvariantCulture)).Append('-');
                _sb.Append(current.Day.ToString("D2", CultureInfo.InvariantCulture)).Append(' ');
                _sb.Append(current.Hour.ToString("D2", CultureInfo.InvariantCulture)).Append(':');
                _sb.Append(current.Minute.ToString("D2", CultureInfo.InvariantCulture));
                if (current.Second > 0)
                    _sb.Append(':').Append(current.Second.ToString("D2", CultureInfo.InvariantCulture));
                writer.WriteStringValue(_sb.ToString());
            }
            finally
            {
                _sb.Clear();
            }
        }
        writer.WriteEndArray();
    }
}