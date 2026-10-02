using System.Diagnostics;
using Microsoft.Extensions.Options;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Security;

namespace VetCare.API.Services.Email
{
    /// <summary>
    /// Deixa o administrador conferir, pela própria tela de configurações, se os e-mails
    /// estão saindo de verdade. O envio de teste não passa pela fila: a resposta precisa
    /// dizer na hora se o servidor aceitou a mensagem ou por que recusou.
    /// </summary>
    public class DiagnosticoDeEmail
    {
        private readonly IServicoDeEmail _servico;
        private readonly ModelosDeEmail _modelos;
        private readonly OpcoesDeEmail _opcoes;
        private readonly IUsuarioRepository _usuarios;
        private readonly UsuarioAtual _usuarioAtual;
        private readonly ILogger<DiagnosticoDeEmail> _logger;

        public DiagnosticoDeEmail(
            IServicoDeEmail servico,
            ModelosDeEmail modelos,
            IOptions<OpcoesDeEmail> opcoes,
            IUsuarioRepository usuarios,
            UsuarioAtual usuarioAtual,
            ILogger<DiagnosticoDeEmail> logger)
        {
            _servico = servico;
            _modelos = modelos;
            _opcoes = opcoes.Value;
            _usuarios = usuarios;
            _usuarioAtual = usuarioAtual;
            _logger = logger;
        }

        public SituacaoDeEmailDTO Situacao() => new()
        {
            Canal = Canal,
            EnviaDeVerdade = _servico.EnviaDeVerdade,
            Servidor = _opcoes.SmtpConfigurado ? $"{_opcoes.Smtp.Host}:{_opcoes.Smtp.Porta}" : null,
            Remetente = _opcoes.RemetenteEfetivo,
            NomeRemetente = _opcoes.NomeRemetente,
            Descricao = _servico.Descricao
        };

        /// <summary>Envia um e-mail de teste para o endereço da própria conta de quem pediu.</summary>
        public async Task<Resultado<TesteDeEmailDTO>> EnviarTeste(CancellationToken cancelamento)
        {
            var usuario = await _usuarios.ObterPorId(_usuarioAtual.Id);

            if (usuario == null || string.IsNullOrWhiteSpace(usuario.Email))
            {
                return Resultado<TesteDeEmailDTO>.Invalido("A sua conta não tem um e-mail cadastrado para receber o teste.");
            }

            var mensagem = _modelos.Teste(usuario);
            var cronometro = Stopwatch.StartNew();

            try
            {
                await _servico.Enviar(mensagem, cancelamento);
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException)
            {
                var explicacao = FalhasDeEmail.Descrever(excecao);

                _logger.LogWarning(excecao, "E-mail de teste para {Destinatario} falhou: {Explicacao}", usuario.Email, explicacao);

                return Resultado<TesteDeEmailDTO>.Invalido(explicacao);
            }

            cronometro.Stop();

            var resposta = new TesteDeEmailDTO
            {
                Destinatario = usuario.Email,
                Canal = Canal,
                DuracaoMs = (int)cronometro.ElapsedMilliseconds,
                Mensagem = _servico.EnviaDeVerdade
                    ? $"E-mail de teste enviado para {usuario.Email}. Confira a caixa de entrada e, se não aparecer, a pasta de spam."
                    : "Nenhum servidor de e-mail está configurado: o e-mail de teste foi gravado na pasta " +
                      $"\"{_opcoes.PastaDaCaixaDeSaida}\" da API em vez de ser enviado."
            };

            return Resultado<TesteDeEmailDTO>.Ok(resposta);
        }

        private string Canal => _servico.EnviaDeVerdade ? "smtp" : "local";
    }
}
