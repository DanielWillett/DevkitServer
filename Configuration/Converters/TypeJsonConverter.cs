using DanielWillett.ReflectionTools;
using DevkitServer.API;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration.Converters;
public sealed class TypeJsonConverter : JsonConverter<Type?>
{
    public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                string? str = reader.GetString();
                if (string.IsNullOrEmpty(str) || str!.Equals("null", StringComparison.InvariantCultureIgnoreCase) || str.Equals("<null>", StringComparison.InvariantCultureIgnoreCase))
                    return null;

                Type? type = Type.GetType(str, false) ?? Type.GetType(str, false, true);
                if (type != null)
                    return type;

                type = AccessorExtensions.DevkitServer.GetType(str, false, true);
                if (type != null)
                    return type;

                type = AccessorExtensions.AssemblyCSharp.GetType(str, false, true);
                if (type != null)
                    return type;

                type = Accessor.MSCoreLib.GetType(str, false, true);
                if (type != null)
                    return type;

                throw new JsonException("Unknown type: \"" + str + "\". Try using the type's fully qualified name. Example: \"SDG.NetTransport.ITransportConnection, SDG.NetTransport\".");

            default:
                throw new JsonException("Unexpected token " + reader.TokenType + " in JSON type converter.");
        }
    }

    public override void Write(Utf8JsonWriter writer, Type? value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
        {
            Assembly asm = value.Assembly;
            if (asm == AccessorExtensions.DevkitServer || asm == AccessorExtensions.AssemblyCSharp || asm == Accessor.MSCoreLib)
                writer.WriteStringValue(value.FullName);
            else
                writer.WriteStringValue(value.AssemblyQualifiedName);
        }
    }
}