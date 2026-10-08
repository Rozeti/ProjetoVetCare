using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// HU-010: fotos e vídeos anexados a uma sessão, respeitando os limites da RN-011.
    /// Quem pode ver o paciente pode anexar e remover as mídias dele (<see cref="AcessoAoPaciente"/>);
    /// como a remoção apaga o arquivo e não tem versão, ela fica na auditoria.
    /// </summary>
    public class AnexarMidiaUseCase
    {
        private static readonly string[] FormatosPermitidos = { ".jpg", ".jpeg", ".png", ".webp", ".mp4", ".mov" };
        private static readonly string[] FormatosDeVideo = { ".mp4", ".mov" };

        /// <summary>RN-011: limite de 25 MB por arquivo de mídia.</summary>
        private const long TamanhoMaximo = 25L * 1024 * 1024;

        private readonly IMidiaRepository _midias;
        private readonly ISessaoRepository _sessoes;
        private readonly IAtendimentoRepository _atendimentos;
        private readonly ArmazenamentoArquivos _armazenamento;
        private readonly AssinadorDeArquivos _assinador;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public AnexarMidiaUseCase(
            IMidiaRepository midias,
            ISessaoRepository sessoes,
            IAtendimentoRepository atendimentos,
            ArmazenamentoArquivos armazenamento,
            AssinadorDeArquivos assinador,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _midias = midias;
            _sessoes = sessoes;
            _atendimentos = atendimentos;
            _armazenamento = armazenamento;
            _assinador = assinador;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<MidiaDTO>> Executar(UploadMidiaDTO dto)
        {
            if (dto.Arquivo == null || dto.Arquivo.Length == 0)
            {
                return Resultado<MidiaDTO>.Invalido("Nenhum arquivo foi enviado.");
            }

            // HU-010, CA-2: arquivo fora do tamanho ou do formato é recusado com o motivo.
            if (dto.Arquivo.Length > TamanhoMaximo)
            {
                return Resultado<MidiaDTO>.Invalido(
                    $"O arquivo excede o limite de {TamanhoMaximo / (1024 * 1024)} MB.");
            }

            var extensao = Path.GetExtension(dto.Arquivo.FileName).ToLowerInvariant();

            if (!FormatosPermitidos.Contains(extensao))
            {
                return Resultado<MidiaDTO>.Invalido(
                    $"Formato não suportado. Formatos aceitos: {string.Join(", ", FormatosPermitidos)}.");
            }

            // HU-010, CA-1: a mídia precisa estar vinculada a uma sessão existente.
            var sessao = await _sessoes.ObterPorIdComRelacionamentos(dto.SessaoId);

            if (sessao == null || sessao.Tratamento?.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<MidiaDTO>.NaoEncontrado("Sessão não encontrada.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, sessao.Tratamento?.Paciente))
            {
                return Resultado<MidiaDTO>.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            if (sessao.Status == StatusSessao.Cancelada)
            {
                return Resultado<MidiaDTO>.Invalido("Não é possível anexar mídias a uma sessão cancelada.");
            }

            // Quando o atendimento não é informado, vinculamos ao da própria sessão, se existir.
            var atendimentoId = dto.AtendimentoId
                                ?? (await _atendimentos.ObterPorSessao(dto.SessaoId))?.Id;

            var caminho = await _armazenamento.Salvar(dto.Arquivo, AssinadorDeArquivos.PastaDeMidias);

            var midia = new MidiaSessao
            {
                SessaoId = dto.SessaoId,
                AtendimentoId = atendimentoId,
                Tipo = FormatosDeVideo.Contains(extensao) ? "Video" : "Imagem",
                NomeArquivo = ArmazenamentoArquivos.NomeSeguro(dto.Arquivo.FileName),
                UrlArquivo = caminho
            };

            await _midias.Adicionar(midia);
            await _midias.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, "MidiaSessao", midia.Id,
                $"{midia.Tipo} {midia.NomeArquivo} anexada à sessão de {sessao.Tratamento?.Paciente?.Nome}");

            return Resultado<MidiaDTO>.Ok(MapearParaDTO(midia, _assinador), "Mídia anexada com sucesso.");
        }

        /// <summary>Equipe da clínica e o tutor do paciente; ninguém mais enxerga as mídias de uma sessão.</summary>
        public async Task<Resultado<List<MidiaDTO>>> ListarPorSessao(Guid sessaoId)
        {
            var sessao = await _sessoes.ObterPorIdComRelacionamentos(sessaoId);

            if (sessao == null || sessao.Tratamento?.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<MidiaDTO>>.NaoEncontrado("Sessão não encontrada.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, sessao.Tratamento?.Paciente))
            {
                return Resultado<List<MidiaDTO>>.NaoAutorizado("Esta sessão não pertence a um pet sob sua responsabilidade.");
            }

            var midias = await _midias.ObterPorSessao(sessaoId);

            return Resultado<List<MidiaDTO>>.Ok(midias.Select(m => MapearParaDTO(m, _assinador)).ToList());
        }

        public async Task<Resultado> Remover(Guid id)
        {
            var midia = await _midias.ObterPorId(id);

            if (midia == null)
            {
                return Resultado.NaoEncontrado("Mídia não encontrada.");
            }

            var sessao = await _sessoes.ObterPorIdComRelacionamentos(midia.SessaoId);

            if (sessao?.Tratamento?.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoEncontrado("Mídia não encontrada.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, sessao.Tratamento?.Paciente))
            {
                return Resultado.NaoAutorizado(AcessoAoPaciente.MensagemNegada);
            }

            _midias.Remover(midia);
            await _midias.SalvarAlteracoes();

            _armazenamento.Remover(midia.UrlArquivo);

            // O arquivo some do disco e não há versão anterior: a auditoria guarda o que era.
            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Exclusao, "MidiaSessao", id,
                $"{midia.Tipo} {midia.NomeArquivo} removida da sessão de {sessao.Tratamento?.Paciente?.Nome}");

            return Resultado.Ok("Mídia removida com sucesso.");
        }

        /// <summary>A URL sai assinada: o arquivo só abre para quem recebeu esta resposta.</summary>
        public static MidiaDTO MapearParaDTO(MidiaSessao midia, AssinadorDeArquivos assinador)
        {
            return new MidiaDTO
            {
                Id = midia.Id,
                SessaoId = midia.SessaoId,
                AtendimentoId = midia.AtendimentoId,
                Tipo = midia.Tipo,
                NomeArquivo = midia.NomeArquivo,
                UrlArquivo = assinador.Assinar(midia.UrlArquivo),
                DataUpload = midia.DataUpload
            };
        }
    }
}
