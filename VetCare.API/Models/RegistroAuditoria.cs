namespace VetCare.API.Models
{
    /// <summary>
    /// Trilha de auditoria dos acessos e alterações em dados clínicos. Atende à
    /// rastreabilidade exigida pela RNF-005 e ao tratamento de dados pessoais.
    /// </summary>
    public class RegistroAuditoria
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ClinicaId { get; set; }
        public Guid UsuarioId { get; set; }
        public string NomeUsuario { get; set; } = string.Empty;
        public string Perfil { get; set; } = string.Empty;

        /// <summary>Um dos valores de <c>AuditoriaService.Acoes</c>: Login, LoginNegado, Consulta, Criacao, Alteracao, Inativacao, Exclusao, Download, SolicitacaoDeSenha ou RedefinicaoSenha.</summary>
        public string Acao { get; set; } = string.Empty;

        public string Entidade { get; set; } = string.Empty;
        public Guid? EntidadeId { get; set; }
        public string Detalhe { get; set; } = string.Empty;
        public string EnderecoIp { get; set; } = string.Empty;
        public DateTime DataHora { get; set; } = DateTime.UtcNow;
    }
}
