using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Consulta da trilha de auditoria, restrita ao Administrador. A tela mostra só o que
    /// mudou nos dados (criação, alteração, inativação e exclusão de pacientes, tutores,
    /// usuários e registros clínicos); logins, consultas e downloads continuam gravados no
    /// banco, mas não aparecem aqui.
    /// </summary>
    public class ConsultarAuditoriaUseCase
    {
        private readonly IAuditoriaRepository _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public ConsultarAuditoriaUseCase(IAuditoriaRepository auditoria, UsuarioAtual usuarioAtual)
        {
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<PaginaDe<RegistroAuditoriaDTO>>> Listar(
            Guid? usuarioId,
            string? acao,
            string? entidade,
            DateTime? inicio,
            DateTime? fim,
            ParametrosPagina parametros)
        {
            var acoes = string.IsNullOrWhiteSpace(acao)
                ? AuditoriaService.Acoes.AlteracoesDeDados
                : AuditoriaService.Acoes.AlteracoesDeDados.Where(a => a == acao).ToList();

            // Uma ação fora da lista (um login, por exemplo) não tem o que mostrar.
            if (acoes.Count == 0)
            {
                return Resultado<PaginaDe<RegistroAuditoriaDTO>>.Ok(
                    PaginaDe<RegistroAuditoriaDTO>.Criar(Array.Empty<RegistroAuditoriaDTO>(), 1, parametros.Tamanho, 0));
            }

            var pagina = await _auditoria.Listar(
                _usuarioAtual.ClinicaId, usuarioId, acoes, entidade, inicio, fim, parametros);

            return Resultado<PaginaDe<RegistroAuditoriaDTO>>.Ok(pagina.Converter(registro => new RegistroAuditoriaDTO
            {
                Id = registro.Id,
                UsuarioId = registro.UsuarioId,
                NomeUsuario = registro.NomeUsuario,
                Perfil = registro.Perfil,
                Acao = registro.Acao,
                Entidade = registro.Entidade,
                EntidadeId = registro.EntidadeId,
                Detalhe = registro.Detalhe,
                EnderecoIp = registro.EnderecoIp,
                DataHora = registro.DataHora
            }));
        }
    }
}
