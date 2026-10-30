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

        /// <summary>
        /// "Esqueci minha senha": o e-mail leva só o código, sem botão nem link para a troca de
        /// senha. A pessoa digita o código na tela em que fez o pedido, no portal ou no
        /// aplicativo; quem tiver acesso à caixa de entrada não consegue trocar a senha com um
        /// clique, e um e-mail falso com link é fácil de reconhecer.
        /// </summary>
        public MensagemDeEmail RecuperacaoDeSenha(Usuario usuario, string codigo, TimeSpan validade)
        {
            return Montar(
                usuario,
                assunto: "Código para redefinir a senha",
                titulo: "Seu código para redefinir a senha",
                paragrafos: new[]
                {
                    $"Recebemos um pedido para redefinir a senha da sua conta no {_aplicacao.NomeDoSistema}.",
                    "Para escolher a nova senha, digite o código abaixo na tela \"Esqueci minha senha\", no portal " +
                    "ou no aplicativo, onde você fez o pedido.",
                },
                botao: null,
                codigo: codigo,
                observacao:
                    $"O código vale por {DescreverValidade(validade)} e só pode ser usado uma vez. A clínica nunca pede " +
                    $"este código, e o {_aplicacao.NomeDoSistema} não envia links para trocar a senha. Se você não pediu " +
                    "a redefinição, ignore este e-mail: sua senha continua a mesma.");
        }

        /// <summary>Conta criada pela clínica sem senha: como na recuperação, o e-mail leva só o código.</summary>
        public MensagemDeEmail PrimeiroAcesso(Usuario usuario, string codigo, TimeSpan validade)
        {
            return Montar(
                usuario,
                assunto: $"Bem-vindo(a) ao {_aplicacao.NomeDoSistema}: crie sua senha",
                titulo: $"Sua conta na {_aplicacao.NomeDaClinica} está pronta",
                paragrafos: new[]
                {
                    $"A {_aplicacao.NomeDaClinica} criou o seu acesso ao {_aplicacao.NomeDoSistema}, com o e-mail {usuario.Email}.",
                    "Para criar a sua senha, abra o portal ou o aplicativo, toque em \"Esqueci minha senha\" na tela de " +
                    "entrada, informe o seu e-mail, escolha \"Já tenho um código\" e digite o código abaixo.",
                },
                botao: null,
                codigo: codigo,
                observacao:
                    $"O código vale por {DescreverValidade(validade)}. Se o prazo passar, use \"Esqueci minha senha\" " +
                    "para receber um novo. A clínica nunca pede este código.");
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
