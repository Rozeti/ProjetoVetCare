using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Security;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Consulta da trilha de auditoria. Restrita ao Administrador: saber quem acessou
    /// cada prontuário é informação sensível por si só.
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
            var pagina = await _auditoria.Listar(
                _usuarioAtual.ClinicaId, usuarioId, acao, entidade, inicio, fim, parametros);

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
