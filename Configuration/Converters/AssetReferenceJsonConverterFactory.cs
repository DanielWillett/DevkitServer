using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration.Converters;
public sealed class AssetReferenceJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(AssetReference<>);
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return (JsonConverter)Activator.CreateInstance(typeof(AssetReferenceJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]));
    }
    public class AssetReferenceJsonConverterGuidPreferred<TAsset> : AssetReferenceJsonConverter<TAsset> where TAsset : Asset
    {
        public AssetReferenceJsonConverterGuidPreferred() => WriteGuid = true;
    }
    public class AssetReferenceJsonConverter<TAsset> : JsonConverter<AssetReference<TAsset>> where TAsset : Asset
    {
        public bool WriteGuid { get; protected set; }
        public override AssetReference<TAsset> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return AssetReference<TAsset>.invalid;
                case JsonTokenType.Number:
                    if (reader.TryGetUInt16(out ushort value))
                    {
                        if (value == 0)
                            return AssetReference<TAsset>.invalid;

                        EAssetType type = AssetUtil.GetAssetCategory<TAsset>();
                        if (type != EAssetType.NONE && Assets.hasLoadedUgc && Assets.find(type, value) is TAsset asset)
                            return new AssetReference<TAsset>(asset.GUID);
                    }

                    throw new JsonException("Failed to parse number as a " + typeof(TAsset).Name + $" reference (UInt16 format), unable to find asset: {value}.");
                case JsonTokenType.String:
                    string str = reader.GetString()!;
                    if (Guid.TryParse(str, out Guid guid))
                        return new AssetReference<TAsset>(guid);

                    throw new JsonException("Failed to parse string as a " + typeof(TAsset).Name + " reference (GUID format).");

                case JsonTokenType.StartObject:
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject)
                            break;
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            string prop = reader.GetString()!;
                            if (prop.Equals("GUID", StringComparison.InvariantCultureIgnoreCase) || prop.Equals("_guid", StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (reader.Read() && reader.TokenType == JsonTokenType.String && Guid.TryParse(reader.GetString()!, out guid))
                                    return new AssetReference<TAsset>(guid);
                            }
                        }
                    }

                    throw new JsonException("Failed to parse object as a " + typeof(TAsset).Name + " reference (Object-GUID format).");
                default:
                    throw new JsonException("Failed to parse a " + typeof(TAsset).Name + $" reference, unknown token: {reader.TokenType}.");
            }
        }

        public override void Write(Utf8JsonWriter writer, AssetReference<TAsset> value, JsonSerializerOptions options)
        {
            if (WriteGuid)
            {
                writer.WriteStringValue(value.GUID.ToString("N"));
            }
            else
            {
                writer.WriteStartObject();
                writer.WritePropertyName("GUID");
                writer.WriteStringValue(value.GUID.ToString("N"));
                writer.WriteEndObject();
            }
        }
    }
}