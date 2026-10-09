using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VetCare.API.Common
{
    /// <summary>
    /// Nascimento, óbito, aplicação e próxima dose de vacina e validade da receita são dias de
    /// calendário, não instantes: 14/03 é 14/03 em qualquer fuso. O banco os guarda como
    /// meia-noite UTC daquele dia, e serializá-los assim ("2018-03-14T00:00:00Z") fazia o portal
    /// e o aplicativo converterem para o fuso do aparelho e mostrarem 13/03 — e, na edição do
    /// paciente ou da vacina, devolverem o dia anterior para ser gravado. Marcadas com este
    /// atributo, essas datas saem da API como "AAAA-MM-DD", sem hora e sem fuso.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DataDeCalendarioAttribute : JsonConverterAttribute
    {
        private const string Formato = "yyyy-MM-dd";

        public override JsonConverter? CreateConverter(Type typeToConvert) =>
            typeToConvert == typeof(DateTime?) ? new ConversorOpcional() : new Conversor();

        private static DateTime Ler(ref Utf8JsonReader leitor)
        {
            var texto = leitor.GetString();

            // Aceita também o formato completo ("2018-03-14T00:00:00Z"): o dia é o que vem antes do "T".
            if (texto is { Length: >= 10 } &&
                DateTime.TryParseExact(texto[..10], Formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
            {
                return DateTime.SpecifyKind(dia, DateTimeKind.Utc);
            }

            throw new JsonException($"Data inválida: \"{texto}\". Use o formato AAAA-MM-DD.");
        }

        private static string Escrever(DateTime valor) => valor.ToString(Formato, CultureInfo.InvariantCulture);

        private sealed class Conversor : JsonConverter<DateTime>
        {
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                Ler(ref reader);

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
                writer.WriteStringValue(Escrever(value));
        }

        private sealed class ConversorOpcional : JsonConverter<DateTime?>
        {
            public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                reader.TokenType == JsonTokenType.Null ? null : Ler(ref reader);

            public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
            {
                if (value.HasValue)
                {
                    writer.WriteStringValue(Escrever(value.Value));
                }
                else
                {
                    writer.WriteNullValue();
                }
            }
        }
    }
}
