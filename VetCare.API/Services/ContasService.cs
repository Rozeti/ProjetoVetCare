using Microsoft.Extensions.Options;
using VetCare.API.Data;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services.Email;

namespace VetCare.API.Services
{
    /// <summary>Token e código recém-gerados, ainda em claro, para irem no e-mail.</summary>
    public sealed record CredenciaisEmitidas(string Token, string Codigo, DateTime ExpiraEm);

    /// <summary>
    /// Comunicação do ciclo de vida da conta: boas-vindas, recuperação de senha, senha
    /// provisória e aviso de senha alterada. Concentra a emissão das credenciais
    /// temporárias e a escolha do e-mail certo, para que os casos de uso que criam ou
    /// mexem em contas não repitam esse roteiro.
    /// </summary>
    public class ContasService
    {
        /// <summary>"Esqueci minha senha": curto, porque o pedido é imediato.</summary>
        public static readonly TimeSpan ValidadeDaRecuperacao = TimeSpan.FromMinutes(30);

        /// <summary>Conta recém-criada: a pessoa pode abrir o e-mail dias depois.</summary>
        public static readonly TimeSpan ValidadeDoPrimeiroAcesso = TimeSpan.FromHours(72);

        private readonly ITokenRedefinicaoRepository _tokens;
        private readonly FilaDeEmails _fila;
        private readonly ModelosDeEmail _modelos;
        private readonly OpcoesDeEmail _opcoesDeEmail;
        private readonly IWebHostEnvironment _ambiente;

        public ContasService(
            ITokenRedefinicaoRepository tokens,
            FilaDeEmails fila,
            ModelosDeEmail modelos,
            IOptions<OpcoesDeEmail> opcoesDeEmail,
            IWebHostEnvironment ambiente)
        {
            _tokens = tokens;
            _fila = fila;
            _modelos = modelos;
            _opcoesDeEmail = opcoesDeEmail.Value;
            _ambiente = ambiente;
        }

        /// <summary>
        /// Em desenvolvimento e sem servidor SMTP, as credenciais voltam na própria resposta
        /// da API para o fluxo poder ser percorrido sem abrir a caixa de saída local. Em
        /// produção isso nunca acontece: o único caminho é o e-mail.
        /// </summary>
        public bool ExporCredenciaisNaResposta => _ambiente.IsDevelopment() && !_opcoesDeEmail.SmtpConfigurado;

        public async Task<CredenciaisEmitidas> EnviarRecuperacaoDeSenha(Usuario usuario)
        {
            var credenciais = await EmitirCredenciais(usuario, FinalidadesDoToken.Recuperacao, ValidadeDaRecuperacao);

            _fila.Enfileirar(_modelos.RecuperacaoDeSenha(usuario, credenciais.Token, credenciais.Codigo, ValidadeDaRecuperacao));

            return credenciais;
        }

        /// <summary>
        /// Conta criada pela clínica. Se a equipe já informou uma senha, o e-mail só dá as
        /// boas-vindas; caso contrário, a pessoa recebe link e código para criar a própria.
        /// </summary>
        public async Task<CredenciaisEmitidas?> EnviarBoasVindas(Usuario usuario, bool senhaDefinidaPelaClinica)
        {
            if (senhaDefinidaPelaClinica)
            {
                _fila.Enfileirar(_modelos.BoasVindas(usuario));
                return null;
            }

            var credenciais = await EmitirCredenciais(usuario, FinalidadesDoToken.PrimeiroAcesso, ValidadeDoPrimeiroAcesso);

            _fila.Enfileirar(_modelos.PrimeiroAcesso(usuario, credenciais.Token, credenciais.Codigo, ValidadeDoPrimeiroAcesso));

            return credenciais;
        }

        /// <summary>HU-002, CA-5: a senha provisória gerada pelo Administrador chega também ao próprio usuário.</summary>
        public void EnviarSenhaProvisoria(Usuario usuario, string senhaProvisoria) =>
            _fila.Enfileirar(_modelos.SenhaProvisoria(usuario, senhaProvisoria));

        /// <summary>Aviso de segurança: quem não fez a troca precisa ficar sabendo na hora.</summary>
        public void EnviarAvisoDeSenhaAlterada(Usuario usuario) =>
            _fila.Enfileirar(_modelos.SenhaAlterada(usuario));

        private async Task<CredenciaisEmitidas> EmitirCredenciais(Usuario usuario, string finalidade, TimeSpan validade)
        {
            var token = CredenciaisTemporarias.GerarToken();
            var codigo = CredenciaisTemporarias.GerarCodigo();

            var registro = new TokenRedefinicaoSenha
            {
                UsuarioId = usuario.Id,
                Finalidade = finalidade,
                ExpiraEm = DateTime.UtcNow.Add(validade)
            };

            registro.TokenHash = CredenciaisTemporarias.HashDoToken(token);
            registro.CodigoHash = CredenciaisTemporarias.HashDoCodigo(registro.Id, codigo);

            // Só o pedido mais recente vale: um link antigo esquecido na caixa de entrada
            // não pode continuar abrindo a conta.
            await _tokens.InvalidarAnteriores(usuario.Id);
            await _tokens.Adicionar(registro);
            await _tokens.SalvarAlteracoes();

            return new CredenciaisEmitidas(token, codigo, registro.ExpiraEm);
        }
    }
}
