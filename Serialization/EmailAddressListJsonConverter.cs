using System.Text.Json;
using System.Text.Json.Serialization;

namespace TopMail.Rest.Serialization;

public class EmailAddressListJsonConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return new List<string>();

        if (reader.TokenType == JsonTokenType.String)
            return SplitAddresses(reader.GetString());

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var values = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (reader.TokenType != JsonTokenType.String)
                    throw new JsonException("Gli indirizzi devono essere stringhe.");

                values.AddRange(SplitAddresses(reader.GetString()));
            }

            return values;
        }

        throw new JsonException("Formato indirizzi non valido: usare stringa o array di stringhe.");
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }
        writer.WriteEndArray();
    }

    private static List<string> SplitAddresses(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return new List<string>();

        return rawValue
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .ToList();
    }
}
