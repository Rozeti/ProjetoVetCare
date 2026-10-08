using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// Configurações da clínica (tenant): parâmetros operacionais usados pelas regras de
    /// negócio, como a antecedência mínima de cancelamento (RN-009) e o expediente (HU-004).
    /// Mudar esses parâmetros muda o que o sistema permite, por isso cada alteração fica
    /// na auditoria.
    /// </summary>
    public class GerenciarClinicaUseCase
    {
        private readonly IClinicaRepository _clinicas;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarClinicaUseCase(IClinicaRepository clinicas, AuditoriaService auditoria, UsuarioAtual usuarioAtual)
        {
            _clinicas = clinicas;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<ClinicaDTO>> Obter()
        {
            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);

            if (clinica == null)
            {
                return Resultado<ClinicaDTO>.NaoEncontrado("Clínica não encontrada.");
            }

            var dto = MapearParaDTO(clinica);

            // O tutor precisa do nome, do contato e do prazo de cancelamento (RN-009); o CNPJ é da administração.
            if (_usuarioAtual.EhTutor)
            {
                dto.Cnpj = string.Empty;
            }

            return Resultado<ClinicaDTO>.Ok(dto);
        }

        public async Task<Resultado<ClinicaDTO>> Atualizar(AtualizarClinicaDTO dto)
        {
            var clinica = await _clinicas.ObterPorId(_usuarioAtual.ClinicaId);

            if (clinica == null)
            {
                return Resultado<ClinicaDTO>.NaoEncontrado("Clínica não encontrada.");
            }

            if (string.IsNullOrWhiteSpace(dto.Nome))
            {
                return Resultado<ClinicaDTO>.Invalido("Informe o nome da clínica.");
            }

            if (!TimeSpan.TryParse(dto.HorarioAbertura, out var abertura) ||
                !TimeSpan.TryParse(dto.HorarioFechamento, out var fechamento))
            {
                return Resultado<ClinicaDTO>.Invalido("Informe os horários no formato HH:mm.");
            }

            if (abertura < TimeSpan.Zero || fechamento > TimeSpan.FromHours(24))
            {
                return Resultado<ClinicaDTO>.Invalido("Os horários precisam estar entre 00:00 e 24:00.");
            }

            if (fechamento <= abertura)
            {
                return Resultado<ClinicaDTO>.Invalido("O horário de fechamento deve ser posterior ao de abertura.");
            }

            // Sem isso nenhuma sessão caberia no expediente (HU-004, CA-1).
            if (fechamento - abertura < TimeSpan.FromMinutes(dto.DuracaoSessaoMinutos))
            {
                return Resultado<ClinicaDTO>.Invalido(
                    "O expediente precisa comportar pelo menos uma sessão com a duração padrão informada.");
            }

            var mudancas = DescreverMudancas(clinica, dto, abertura, fechamento);

            clinica.Nome = dto.Nome.Trim();
            clinica.Cnpj = dto.Cnpj.Trim();
            clinica.Telefone = dto.Telefone.Trim();
            clinica.Endereco = dto.Endereco.Trim();
            clinica.HorasMinimasCancelamento = dto.HorasMinimasCancelamento;
            clinica.DuracaoSessaoMinutos = dto.DuracaoSessaoMinutos;
            clinica.HorarioAbertura = abertura;
            clinica.HorarioFechamento = fechamento;

            _clinicas.Atualizar(clinica);
            await _clinicas.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, "Clinica", clinica.Id,
                mudancas.Count == 0 ? "Configurações salvas sem alterações" : string.Join("; ", mudancas));

            return Resultado<ClinicaDTO>.Ok(MapearParaDTO(clinica), "Configurações da clínica atualizadas com sucesso.");
        }

        /// <summary>Só o que mudou vai para a trilha: é o que interessa a quem for ler.</summary>
        private static List<string> DescreverMudancas(Clinica atual, AtualizarClinicaDTO dto, TimeSpan abertura, TimeSpan fechamento)
        {
            var mudancas = new List<string>();

            if (atual.Nome != dto.Nome.Trim()) mudancas.Add($"nome: {atual.Nome} → {dto.Nome.Trim()}");
            if (atual.Cnpj != dto.Cnpj.Trim()) mudancas.Add("CNPJ alterado");
            if (atual.Telefone != dto.Telefone.Trim()) mudancas.Add("telefone alterado");
            if (atual.Endereco != dto.Endereco.Trim()) mudancas.Add("endereço alterado");

            if (atual.HorasMinimasCancelamento != dto.HorasMinimasCancelamento)
            {
                mudancas.Add($"antecedência de cancelamento: {atual.HorasMinimasCancelamento}h → {dto.HorasMinimasCancelamento}h");
            }

            if (atual.DuracaoSessaoMinutos != dto.DuracaoSessaoMinutos)
            {
                mudancas.Add($"duração da sessão: {atual.DuracaoSessaoMinutos}min → {dto.DuracaoSessaoMinutos}min");
            }

            if (atual.HorarioAbertura != abertura || atual.HorarioFechamento != fechamento)
            {
                var de = $"{Hora(atual.HorarioAbertura)}-{Hora(atual.HorarioFechamento)}";
                var para = $"{Hora(abertura)}-{Hora(fechamento)}";
                mudancas.Add($"expediente: {de} → {para}");
            }

            return mudancas;
        }

        private static string Hora(TimeSpan valor) => valor.ToString(@"hh\:mm");

        private static ClinicaDTO MapearParaDTO(Clinica clinica) => new()
        {
            Id = clinica.Id,
            Nome = clinica.Nome,
            Cnpj = clinica.Cnpj,
            Telefone = clinica.Telefone,
            Endereco = clinica.Endereco,
            HorasMinimasCancelamento = clinica.HorasMinimasCancelamento,
            HorarioAbertura = clinica.HorarioAbertura.ToString(@"hh\:mm"),
            HorarioFechamento = clinica.HorarioFechamento.ToString(@"hh\:mm"),
            DuracaoSessaoMinutos = clinica.DuracaoSessaoMinutos
        };
    }
}
