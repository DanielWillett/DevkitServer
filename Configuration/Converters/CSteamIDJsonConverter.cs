using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration.Converters;
public sealed class CSteamIDJsonConverter : JsonConverter<CSteamID>
{
    public override CSteamID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return CSteamID.Nil;

            case JsonTokenType.String:
                string str = reader.GetString()!;
                if (!DevkitServerUtility.TryParseSteamId(str, out CSteamID steamId))
                    throw new JsonException("Unknown string notation for CSteamID. Valid formats: Steam64 ID, Steam2 ID, Steam3 ID, common strings.");
                return steamId;

            case JsonTokenType.Number:
                if (reader.TryGetUInt64(out ulong steam64))
                    return new CSteamID(steam64);
                throw new JsonException("Unknown numeric (64-bit) notation for CSteamID. Valid formats: Steam64 ID, Steam2 ID, Steam3 ID, preset strings.");

            case JsonTokenType.StartObject:
                ulong? csteamid = null;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string? prop = reader.GetString();
                        if (reader.Read() && prop != null && "m_SteamID".Equals(prop, StringComparison.InvariantCultureIgnoreCase) || "id".Equals(prop, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                str = reader.GetString()!;
                                if (!DevkitServerUtility.TryParseSteamId(str, out steamId))
                                {
                                    csteamid = steamId.m_SteamID;
                                    continue;
                                }
                            }
                            else if (reader.TokenType == JsonTokenType.Number)
                            {
                                if (reader.TryGetUInt64(out ulong id))
                                {
                                    csteamid = id;
                                    continue;
                                }
                            }
                            else if (reader.TokenType == JsonTokenType.Null)
                            {
                                csteamid = 0;
                                continue;
                            }
                            throw new JsonException("Failed to read a Guid-object formatted asset reference.");
                        }
                    }
                }

                if (csteamid.HasValue)
                    return new CSteamID(csteamid.Value);

                throw new JsonException("Unknown object-numeric (64-bit) notation for CSteamID. Valid formats: Steam64 ID, Steam2 ID, Steam3 ID, preset strings.");

            default:
                throw new JsonException("Unknown CSteamID token: " + reader.TokenType + ".");
        }
    }
    public override void Write(Utf8JsonWriter writer, CSteamID value, JsonSerializerOptions options)
    {
        if (value.m_SteamID == default)
            writer.WriteNullValue();
        else if (value.m_SteamID == CSteamID.OutofDateGS.m_SteamID)
            writer.WriteStringValue("out-of-date-gs");
        else if (value.m_SteamID == CSteamID.LanModeGS.m_SteamID)
            writer.WriteStringValue("lan-mode-gs");
        else if (value.m_SteamID == CSteamID.NotInitYetGS.m_SteamID)
            writer.WriteStringValue("not-init-yet-gs");
        else if (value.m_SteamID == CSteamID.NonSteamGS.m_SteamID)
            writer.WriteStringValue("non-steam-gs");
        else
            writer.WriteNumberValue(value.m_SteamID);
    }
}