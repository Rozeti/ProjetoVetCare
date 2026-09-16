using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>HU-010: fotos e vídeos anexados a uma sessão, respeitando os limites da RN-011.</summary>
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
        private readonly UsuarioAtual _usuarioAtual;

        public AnexarMidiaUseCase(
            IMidiaRepository midias,
            ISessaoRepository sessoes,
            IAtendimentoRepository atendimentos,
            ArmazenamentoArquivos armazenamento,
            UsuarioAtual usuarioAtual)
        {
            _midias = midias;
            _sessoes = sessoes;
            _atendimentos = atendimentos;
            _armazenamento = armazenamento;
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

            if (sessao == null)
            {
                return Resultado<MidiaDTO>.NaoEncontrado("Sessão não encontrada.");
            }

            if (sessao.Tratamento?.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<MidiaDTO>.NaoAutorizado("Esta sessão pertence a outra clínica.");
            }

            if (_usuarioAtual.EhVeterinario && sessao.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return Resultado<MidiaDTO>.NaoAutorizado("Você só pode anexar mídias às suas próprias sessões.");
            }

            // Quando o atendimento não é informado, vinculamos ao da própria sessão, se existir.
            var atendimentoId = dto.AtendimentoId
                                ?? (await _atendimentos.ObterPorSessao(dto.SessaoId))?.Id;

            var url = await _armazenamento.Salvar(dto.Arquivo, "uploads");

            var midia = new MidiaSessao
            {
                SessaoId = dto.SessaoId,
                AtendimentoId = atendimentoId,
                Tipo = FormatosDeVideo.Contains(extensao) ? "Video" : "Imagem",
                NomeArquivo = dto.Arquivo.FileName,
                UrlArquivo = url
            };

            await _midias.Adicionar(midia);
            await _midias.SalvarAlteracoes();

            return Resultado<MidiaDTO>.Ok(MapearParaDTO(midia), "Mídia anexada com sucesso.");
        }

        public async Task<Resultado<List<MidiaDTO>>> ListarPorSessao(Guid sessaoId)
        {
            var midias = await _midias.ObterPorSessao(sessaoId);
            return Resultado<List<MidiaDTO>>.Ok(midias.Select(MapearParaDTO).ToList());
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
                return Resultado.NaoAutorizado("Esta mídia pertence a outra clínica.");
            }

            if (_usuarioAtual.EhVeterinario && sessao.VeterinarioId != _usuarioAtual.VeterinarioId)
            {
                return Resultado.NaoAutorizado("Você só pode remover mídias das suas próprias sessões.");
            }

            _midias.Remover(midia);
            await _midias.SalvarAlteracoes();

            _armazenamento.Remover(midia.UrlArquivo);

            return Resultado.Ok("Mídia removida com sucesso.");
        }

        public static MidiaDTO MapearParaDTO(MidiaSessao midia)
        {
            return new MidiaDTO
            {
                Id = midia.Id,
                SessaoId = midia.SessaoId,
                AtendimentoId = midia.AtendimentoId,
                Tipo = midia.Tipo,
                NomeArquivo = midia.NomeArquivo,
                UrlArquivo = midia.UrlArquivo,
                DataUpload = midia.DataUpload
            };
        }
    }
}
