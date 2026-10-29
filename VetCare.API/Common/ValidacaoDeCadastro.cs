using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VetCare.API.Common
{
    /// <summary>
    /// Regras dos dados de cadastro de pessoas (usuários da equipe e tutores), as mesmas no
    /// cadastro e na edição. Cada método devolve a mensagem do problema, ou nulo quando o valor
    /// é aceito; os que padronizam o formato (CRMV, telefone, CPF) devolvem também o valor
    /// normalizado, que é o que vai para o banco. O portal web aplica as mesmas regras antes
    /// de enviar (utils/formato.ts).
    /// </summary>
    public static partial class ValidacaoDeCadastro
    {
        public const int NomeMinimo = 3;
        public const int NomeMaximo = 120;
        public const int EmailMaximo = 180;

        /// <summary>As 27 unidades da federação, que aparecem no registro do CRMV.</summary>
        public static readonly IReadOnlySet<string> Ufs = new HashSet<string>
        {
            "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT", "PA",
            "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO"
        };

        /// <summary>Espaços repetidos viram um só, e as pontas são cortadas.</summary>
        public static string Compactar(string? texto) =>
            string.Join(' ', (texto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();

        /// <summary>
        /// Nome completo de uma pessoa: de 3 a 120 caracteres, só letras (com acento), espaços,
        /// apóstrofos, hífens e pontos, e pelo menos nome e sobrenome.
        /// </summary>
        public static string? Nome(string? nome)
        {
            var texto = Compactar(nome);

            if (texto.Length < NomeMinimo || texto.Length > NomeMaximo)
                return $"O nome deve ter entre {NomeMinimo} e {NomeMaximo} caracteres.";

            if (!CaracteresDeNome().IsMatch(texto))
                return "O nome deve ter apenas letras, espaços, apóstrofos, hífens e pontos (sem números).";

            // "Dra. Ana" conta como nome e sobrenome? Não: a abreviação de título não é sobrenome.
            var palavras = texto.Split(' ').Count(p => p.TrimEnd('.').Count(char.IsLetter) >= 2 && !p.EndsWith('.'));

            return palavras < 2 ? "Informe o nome completo, com nome e sobrenome." : null;
        }

        /// <summary>E-mail com usuário, @ e domínio com extensão (ex.: nome@clinica.com.br).</summary>
        public static string? Email(string? email)
        {
            var texto = (email ?? string.Empty).Trim();

            if (texto.Length == 0)
                return "Informe o e-mail.";

            if (texto.Length > EmailMaximo)
                return $"O e-mail deve ter até {EmailMaximo} caracteres.";

            if (!FormatoDeEmail().IsMatch(texto) || texto.Contains(".."))
                return "Informe um e-mail válido, como nome@clinica.com.br.";

            return null;
        }

        /// <summary>
        /// CRMV com a UF do conselho e o número da inscrição. Aceita "CRMV-DF 12345",
        /// "CRMV DF 12345", "DF-12345" ou "12345/DF" e grava sempre como "CRMV-DF 12345".
        /// </summary>
        public static (string? Valor, string? Erro) Crmv(string? crmv)
        {
            var texto = (crmv ?? string.Empty).Trim().ToUpperInvariant();

            if (texto.Length == 0)
                return (null, "Informe o CRMV do veterinário.");

            var formato = CrmvUfAntes().Match(texto);
            string uf, numero;

            if (formato.Success)
            {
                uf = formato.Groups["uf"].Value;
                numero = formato.Groups["numero"].Value;
            }
            else
            {
                formato = CrmvUfDepois().Match(texto);

                if (!formato.Success)
                    return (null, "Informe o CRMV no formato CRMV-UF 12345, por exemplo CRMV-DF 12345.");

                uf = formato.Groups["uf"].Value;
                numero = formato.Groups["numero"].Value;
            }

            if (!Ufs.Contains(uf))
                return (null, $"\"{uf}\" não é uma UF válida para o CRMV. Use a sigla do estado, como DF ou SP.");

            return ($"CRMV-{uf} {numero}", null);
        }

        /// <summary>
        /// Telefone brasileiro com DDD: fixo (10 dígitos) ou celular (11 dígitos, começando
        /// por 9). O código do país (+55) é aceito e descartado. Grava como "(61) 99999-0000".
        /// </summary>
        public static (string? Valor, string? Erro) Telefone(string? telefone, bool obrigatorio, string rotulo = "o telefone")
        {
            var digitos = SoDigitos(telefone);

            if (digitos.Length == 0)
                return obrigatorio ? (null, $"Informe {rotulo} com DDD.") : (string.Empty, null);

            if (digitos.Length is 12 or 13 && digitos.StartsWith("55"))
                digitos = digitos[2..];

            const string exemplo = "Informe um telefone válido com DDD, como (61) 99999-0000 ou (61) 3333-4444.";

            if (digitos.Length is not (10 or 11) || digitos[0] == '0' || digitos[1] == '0')
                return (null, exemplo);

            if (digitos.Length == 11 && digitos[2] != '9')
                return (null, "Celular com 11 dígitos começa com 9 depois do DDD, como (61) 99999-0000.");

            if (digitos.Length == 10 && digitos[2] is '0' or '1')
                return (null, exemplo);

            var ddd = digitos[..2];
            var numero = digitos[2..];
            var corte = numero.Length - 4;

            return ($"({ddd}) {numero[..corte]}-{numero[corte..]}", null);
        }

        /// <summary>CPF com os dígitos verificadores corretos. Opcional; grava como "000.000.000-00".</summary>
        public static (string? Valor, string? Erro) Cpf(string? cpf)
        {
            var digitos = SoDigitos(cpf);

            if (digitos.Length == 0)
                return (string.Empty, null);

            if (digitos.Length != 11 || digitos.Distinct().Count() == 1 || !DigitosDoCpfConferem(digitos))
                return (null, "CPF inválido. Confira os 11 números.");

            return ($"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..]}", null);
        }

        /// <summary>Endereço opcional; quando informado, precisa ter letras e entre 5 e 250 caracteres.</summary>
        public static string? Endereco(string? endereco)
        {
            var texto = Compactar(endereco);

            if (texto.Length == 0)
                return null;

            if (texto.Length < 5 || texto.Length > 250 || !texto.Any(char.IsLetter))
                return "Informe o endereço completo (rua, número e bairro), com até 250 caracteres.";

            return null;
        }

        /// <summary>Texto descritivo opcional (especialidade, setor): letras, entre o mínimo e o máximo.</summary>
        public static string? TextoOpcional(string? texto, string rotulo, int minimo, int maximo)
        {
            var valor = Compactar(texto);

            if (valor.Length == 0)
                return null;

            if (valor.Length < minimo || valor.Length > maximo)
                return $"{rotulo} deve ter entre {minimo} e {maximo} caracteres.";

            if (valor.Count(char.IsLetter) < 2)
                return $"{rotulo} deve ser escrito com letras.";

            return null;
        }

        public static string SoDigitos(string? texto) =>
            new((texto ?? string.Empty).Where(char.IsDigit).ToArray());

        /// <summary>Compara textos sem diferenciar maiúsculas nem acentos ("Cão" e "cao").</summary>
        public static string SemAcento(string? texto)
        {
            var decomposto = (texto ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var resultado = new StringBuilder(decomposto.Length);

            foreach (var caractere in decomposto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
                    resultado.Append(caractere);
            }

            return resultado.ToString().Normalize(NormalizationForm.FormC);
        }

        private static bool DigitosDoCpfConferem(string digitos)
        {
            int Digito(int quantidade)
            {
                var soma = 0;

                for (var i = 0; i < quantidade; i++)
                    soma += (digitos[i] - '0') * (quantidade + 1 - i);

                var resto = soma % 11;
                return resto < 2 ? 0 : 11 - resto;
            }

            return Digito(9) == digitos[9] - '0' && Digito(10) == digitos[10] - '0';
        }

        [GeneratedRegex(@"^\p{L}[\p{L}\s'.\-]*$")]
        private static partial Regex CaracteresDeNome();

        [GeneratedRegex(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$")]
        private static partial Regex FormatoDeEmail();

        [GeneratedRegex(@"^(?:CRMV)?[\s\-/]*(?<uf>[A-Z]{2})[\s\-/]*(?<numero>\d{1,6})$")]
        private static partial Regex CrmvUfAntes();

        [GeneratedRegex(@"^(?:CRMV)?[\s\-/]*(?<numero>\d{1,6})[\s\-/]*(?<uf>[A-Z]{2})$")]
        private static partial Regex CrmvUfDepois();
    }
}
