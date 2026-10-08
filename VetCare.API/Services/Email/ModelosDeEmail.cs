using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using VetCare.API.Common;
using VetCare.API.Models;

namespace VetCare.API.Services.Email
{
    /// <summary>
    /// Todos os e-mails que o sistema envia, num único lugar e com a mesma identidade
    /// visual. Cada método devolve a mensagem pronta, em HTML e em texto simples. Os
    /// valores vindos do usuário passam por codificação HTML: um nome com "&lt;" não pode
    /// virar marcação dentro do e-mail de outra pessoa.
    /// </summary>
    public class ModelosDeEmail
    {
        private const string CorMarca = "#0284c7";
        private const string CorMarcaEscura = "#0369a1";
        private const string CorTexto = "#0f172a";
        private const string CorTextoSecundario = "#64748b";

        private readonly OpcoesDaAplicacao _aplicacao;

        public ModelosDeEmail(IOptions<OpcoesDaAplicacao> aplicacao)
        {
            _aplicacao = aplicacao.Value;
        }

        public MensagemDeEmail RecuperacaoDeSenha(Usuario usuario, string token, string codigo, TimeSpan validade)
        {
            return Montar(
                usuario,
                assunto: "Redefinição de senha",
                titulo: "Vamos redefinir sua senha",
                paragrafos: new[]
                {
                    $"Recebemos um pedido para redefinir a senha da sua conta no {_aplicacao.NomeDoSistema}.",
                    "Pelo computador, clique no botão abaixo. Pelo aplicativo, digite o código a seguir na tela \"Esqueci minha senha\".",
                },
                botao: new Botao("Redefinir minha senha", LinkDeRedefinicao("/recuperar-senha", token)),
                codigo: codigo,
                observacao:
                    $"O link e o código valem por {DescreverValidade(validade)} e só podem ser usados uma vez. " +
                    "Se você não pediu a redefinição, ignore este e-mail: sua senha continua a mesma.");
        }

        public MensagemDeEmail PrimeiroAcesso(Usuario usuario, string token, string codigo, TimeSpan validade)
        {
            return Montar(
                usuario,
                assunto: $"Bem-vindo(a) ao {_aplicacao.NomeDoSistema}: crie sua senha",
                titulo: $"Sua conta na {_aplicacao.NomeDaClinica} está pronta",
                paragrafos: new[]
                {
                    $"A {_aplicacao.NomeDaClinica} criou o seu acesso ao {_aplicacao.NomeDoSistema}, com o e-mail {usuario.Email}.",
                    "Para começar, defina a sua senha. Pelo computador, clique no botão abaixo. Pelo aplicativo, " +
                    "toque em \"Esqueci minha senha\" na tela de entrada e digite o código a seguir.",
                },
                botao: new Botao("Criar minha senha", LinkDeRedefinicao("/primeiro-acesso", token)),
                codigo: codigo,
                observacao:
                    $"O link e o código valem por {DescreverValidade(validade)}. Se o prazo passar, use " +
                    "\"Esqueci minha senha\" para receber novos.");
        }

        public MensagemDeEmail BoasVindas(Usuario usuario)
        {
            return Montar(
                usuario,
                assunto: $"Bem-vindo(a) ao {_aplicacao.NomeDoSistema}",
                titulo: $"Sua conta na {_aplicacao.NomeDaClinica} está pronta",
                paragrafos: new[]
                {
                    $"A {_aplicacao.NomeDaClinica} criou o seu acesso ao {_aplicacao.NomeDoSistema}, com o e-mail {usuario.Email}.",
                    "A senha inicial foi informada pela equipe da clínica. Recomendamos trocá-la no primeiro acesso, " +
                    "em \"Minha conta\".",
                },
                botao: new Botao($"Entrar no {_aplicacao.NomeDoSistema}", _aplicacao.LinkDoPortal("/login")),
                codigo: null,
                observacao: "Se você não esperava este e-mail, entre em contato com a clínica.");
        }

        public MensagemDeEmail SenhaProvisoria(Usuario usuario, string senhaProvisoria)
        {
            return Montar(
                usuario,
                assunto: "Sua senha foi redefinida pela clínica",
                titulo: "Senha provisória",
                paragrafos: new[]
                {
                    $"A administração da {_aplicacao.NomeDaClinica} redefiniu a senha da sua conta no {_aplicacao.NomeDoSistema}.",
                    "Use a senha provisória abaixo para entrar e troque-a em seguida, em \"Minha conta\".",
                },
                botao: new Botao($"Entrar no {_aplicacao.NomeDoSistema}", _aplicacao.LinkDoPortal("/login")),
                codigo: senhaProvisoria,
                observacao: "Se você não pediu essa redefinição, fale com a clínica.");
        }

        public MensagemDeEmail SenhaAlterada(Usuario usuario)
        {
            return Montar(
                usuario,
                assunto: "Sua senha foi alterada",
                titulo: "Senha alterada",
                paragrafos: new[]
                {
                    $"A senha da sua conta no {_aplicacao.NomeDoSistema} acabou de ser alterada, em {RelogioDaClinica.Padrao.Formatar(DateTime.UtcNow)}.",
                    "Se foi você, não precisa fazer nada.",
                },
                botao: null,
                codigo: null,
                observacao:
                    "Se você não reconhece essa alteração, use \"Esqueci minha senha\" para definir uma nova " +
                    "imediatamente e avise a clínica.");
        }

        /// <summary>HU-015: cada notificação do tratamento chega também por e-mail, com atalho para o portal.</summary>
        public MensagemDeEmail Notificacao(Usuario usuario, Notificacao notificacao)
        {
            return Montar(
                usuario,
                assunto: notificacao.Titulo,
                titulo: notificacao.Titulo,
                paragrafos: new[] { notificacao.Conteudo },
                botao: new Botao($"Abrir no {_aplicacao.NomeDoSistema}", _aplicacao.LinkDoPortal(notificacao.LinkRelacionado)),
                codigo: null,
                observacao:
                    "Você recebe este aviso porque ativou as notificações por e-mail. " +
                    "Para parar de recebê-las, desative a opção em \"Minha conta\".");
        }

        private string LinkDeRedefinicao(string caminho, string token) =>
            _aplicacao.LinkDoPortal($"{caminho}?token={Uri.EscapeDataString(token)}");

        private static string DescreverValidade(TimeSpan validade)
        {
            if (validade < TimeSpan.FromHours(1))
            {
                return $"{(int)validade.TotalMinutes} minutos";
            }

            if (validade < TimeSpan.FromHours(48))
            {
                return $"{(int)validade.TotalHours} horas";
            }

            return $"{(int)validade.TotalDays} dias";
        }

        private sealed record Botao(string Texto, string Url);

        private MensagemDeEmail Montar(
            Usuario usuario,
            string assunto,
            string titulo,
            IReadOnlyList<string> paragrafos,
            Botao? botao,
            string? codigo,
            string? observacao)
        {
            var primeiroNome = PrimeiroNome(usuario.Nome);
            var assuntoCompleto = $"{_aplicacao.NomeDoSistema} — {assunto}";

            return new MensagemDeEmail(
                usuario.Email,
                usuario.Nome,
                assuntoCompleto,
                MontarHtml(primeiroNome, titulo, paragrafos, botao, codigo, observacao),
                MontarTexto(primeiroNome, titulo, paragrafos, botao, codigo, observacao));
        }

        private string MontarHtml(
            string primeiroNome,
            string titulo,
            IReadOnlyList<string> paragrafos,
            Botao? botao,
            string? codigo,
            string? observacao)
        {
            var html = new StringBuilder();

            html.Append("<!DOCTYPE html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
                .Append($"<title>{E(titulo)}</title></head>")
                .Append($"<body style=\"margin:0;padding:0;background:#f1f5f9;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:{CorTexto};\">")
                .Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#f1f5f9;padding:32px 12px;\"><tr><td align=\"center\">")
                .Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:560px;background:#ffffff;border-radius:16px;overflow:hidden;border:1px solid #e2e8f0;\">")
                .Append($"<tr><td style=\"background:{CorMarca};padding:22px 32px;\">")
                .Append($"<span style=\"font-size:22px;font-weight:700;color:#ffffff;letter-spacing:-0.01em;\">{E(_aplicacao.NomeDoSistema)}</span>")
                .Append($"<span style=\"display:block;font-size:13px;color:#e0f2fe;margin-top:2px;\">{E(_aplicacao.NomeDaClinica)}</span>")
                .Append("</td></tr>")
                .Append("<tr><td style=\"padding:32px;\">")
                .Append($"<h1 style=\"margin:0 0 16px;font-size:22px;font-weight:700;color:{CorTexto};\">{E(titulo)}</h1>")
                .Append($"<p style=\"margin:0 0 12px;font-size:15px;line-height:1.6;\">Olá, {E(primeiroNome)}.</p>");

            foreach (var paragrafo in paragrafos)
            {
                html.Append($"<p style=\"margin:0 0 12px;font-size:15px;line-height:1.6;\">{E(paragrafo)}</p>");
            }

            if (botao != null)
            {
                html.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin:24px 0;\"><tr>")
                    .Append($"<td style=\"background:{CorMarca};border-radius:12px;\">")
                    .Append($"<a href=\"{E(botao.Url)}\" style=\"display:inline-block;padding:13px 26px;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;\">{E(botao.Texto)}</a>")
                    .Append("</td></tr></table>")
                    .Append($"<p style=\"margin:0 0 12px;font-size:12px;line-height:1.5;color:{CorTextoSecundario};\">Se o botão não funcionar, copie este endereço no navegador:<br>")
                    .Append($"<a href=\"{E(botao.Url)}\" style=\"color:{CorMarcaEscura};word-break:break-all;\">{E(botao.Url)}</a></p>");
            }

            if (!string.IsNullOrWhiteSpace(codigo))
            {
                html.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin:20px 0;\"><tr>")
                    .Append("<td align=\"center\" style=\"background:#f0f9ff;border:1px dashed #7dd3fc;border-radius:12px;padding:18px;\">")
                    .Append($"<span style=\"display:block;font-size:12px;font-weight:700;letter-spacing:0.08em;text-transform:uppercase;color:{CorTextoSecundario};\">Código</span>")
                    .Append($"<span style=\"display:block;margin-top:6px;font-family:Consolas,Menlo,monospace;font-size:30px;font-weight:700;letter-spacing:0.25em;color:{CorMarcaEscura};\">{E(codigo)}</span>")
                    .Append("</td></tr></table>");
            }

            if (!string.IsNullOrWhiteSpace(observacao))
            {
                html.Append($"<p style=\"margin:20px 0 0;font-size:13px;line-height:1.6;color:{CorTextoSecundario};\">{E(observacao)}</p>");
            }

            html.Append("</td></tr>")
                .Append($"<tr><td style=\"padding:18px 32px;background:#f8fafc;border-top:1px solid #e2e8f0;font-size:12px;line-height:1.5;color:{CorTextoSecundario};\">")
                .Append($"{E(_aplicacao.NomeDoSistema)} — plataforma de gestão clínica da {E(_aplicacao.NomeDaClinica)}. ")
                .Append("Este e-mail é enviado automaticamente; não é preciso respondê-lo.")
                .Append("</td></tr></table></td></tr></table></body></html>");

            return html.ToString();
        }

        private string MontarTexto(
            string primeiroNome,
            string titulo,
            IReadOnlyList<string> paragrafos,
            Botao? botao,
            string? codigo,
            string? observacao)
        {
            var texto = new StringBuilder()
                .AppendLine($"{_aplicacao.NomeDoSistema} — {_aplicacao.NomeDaClinica}")
                .AppendLine()
                .AppendLine(titulo)
                .AppendLine()
                .AppendLine($"Olá, {primeiroNome}.")
                .AppendLine();

            foreach (var paragrafo in paragrafos)
            {
                texto.AppendLine(paragrafo).AppendLine();
            }

            if (botao != null)
            {
                texto.AppendLine($"{botao.Texto}: {botao.Url}").AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(codigo))
            {
                texto.AppendLine($"Código: {codigo}").AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(observacao))
            {
                texto.AppendLine(observacao).AppendLine();
            }

            texto.AppendLine("Este e-mail é enviado automaticamente; não é preciso respondê-lo.");

            return texto.ToString();
        }

        private static string PrimeiroNome(string nome)
        {
            var primeiro = nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(primeiro) ? "tudo bem" : primeiro;
        }

        private static string E(string valor) => WebUtility.HtmlEncode(valor);
    }
}
