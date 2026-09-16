using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>HU-012: contratos, exames externos e laudos anexados ao prontuário.</summary>
    public class GerenciarDocumentosUseCase
    {
        private static readonly string[] FormatosPermitidos = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };

        /// <summary>RN-011: limite de 10 MB por documento.</summary>
        private const long TamanhoMaximo = 10L * 1024 * 1024;

        private readonly IDocumentoRepository _documentos;
        private readonly IProntuarioRepository _prontuarios;
        private readonly ArmazenamentoArquivos _armazenamento;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarDocumentosUseCase(
            IDocumentoRepository documentos,
            IProntuarioRepository prontuarios,
            ArmazenamentoArquivos armazenamento,
            UsuarioAtual usuarioAtual)
        {
            _documentos = documentos;
            _prontuarios = prontuarios;
            _armazenamento = armazenamento;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<DocumentoDTO>> Anexar(UploadDocumentoDTO dto)
        {
            if (dto.Arquivo == null || dto.Arquivo.Length == 0)
            {
                return Resultado<DocumentoDTO>.Invalido("Nenhum arquivo foi enviado.");
            }

            if (dto.Arquivo.Length > TamanhoMaximo)
            {
                return Resultado<DocumentoDTO>.Invalido(
                    $"O documento excede o limite de {TamanhoMaximo / (1024 * 1024)} MB.");
            }

            var extensao = Path.GetExtension(dto.Arquivo.FileName).ToLowerInvariant();

            // HU-012, CA-3: formato não permitido é recusado informando os aceitos.
            if (!FormatosPermitidos.Contains(extensao))
            {
                return Resultado<DocumentoDTO>.Invalido(
                    $"Formato não permitido. Formatos aceitos: {string.Join(", ", FormatosPermitidos)}.");
            }

            var prontuario = await ResolverProntuario(dto.ProntuarioId, dto.PacienteId);

            if (prontuario == null)
            {
                return Resultado<DocumentoDTO>.NaoEncontrado(
                    "Informe um prontuário ou um paciente válido para vincular o documento.");
            }

            if (prontuario.Paciente != null && prontuario.Paciente.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<DocumentoDTO>.NaoAutorizado("Este prontuário pertence a outra clínica.");
            }

            var url = await _armazenamento.Salvar(dto.Arquivo, "documentos");

            var documento = new DocumentoClinico
            {
                ProntuarioId = prontuario.Id,
                EnviadoPorId = _usuarioAtual.Id,
                NomeArquivo = dto.Arquivo.FileName,
                TipoDocumento = string.IsNullOrWhiteSpace(dto.TipoDocumento) ? "Outro" : dto.TipoDocumento.Trim(),
                UrlArquivo = url,
                TamanhoBytes = dto.Arquivo.Length
            };

            await _documentos.Adicionar(documento);
            await _documentos.SalvarAlteracoes();

            var salvo = await _documentos.ObterPorId(documento.Id);

            return Resultado<DocumentoDTO>.Ok(MapearParaDTO(salvo ?? documento), "Documento anexado com sucesso.");
        }

        public async Task<Resultado<List<DocumentoDTO>>> ListarPorPaciente(Guid pacienteId)
        {
            var prontuario = await _prontuarios.ObterPorPacienteId(pacienteId);

            if (prontuario == null)
            {
                return Resultado<List<DocumentoDTO>>.Ok(new List<DocumentoDTO>());
            }

            // HU-013: o tutor acessa apenas os documentos dos próprios pets.
            if (_usuarioAtual.EhTutor && prontuario.Paciente?.TutorId != _usuarioAtual.TutorId)
            {
                return Resultado<List<DocumentoDTO>>.NaoAutorizado("Você não tem acesso a este prontuário.");
            }

            var documentos = await _documentos.ObterPorProntuario(prontuario.Id);

            return Resultado<List<DocumentoDTO>>.Ok(documentos.Select(MapearParaDTO).ToList());
        }

        /// <summary>HU-012, CA-2: visualização e download pelo usuário autorizado.</summary>
        public async Task<Resultado<(string Caminho, string NomeArquivo)>> ObterParaDownload(Guid id)
        {
            var documento = await _documentos.ObterPorId(id);

            if (documento == null)
            {
                return Resultado<(string, string)>.NaoEncontrado("Documento não encontrado.");
            }

            if (documento.Prontuario?.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<(string, string)>.NaoAutorizado("Este documento pertence a outra clínica.");
            }

            if (_usuarioAtual.EhTutor && documento.Prontuario?.Paciente?.TutorId != _usuarioAtual.TutorId)
            {
                return Resultado<(string, string)>.NaoAutorizado("Você não tem acesso a este documento.");
            }

            var caminho = _armazenamento.ResolverCaminhoFisico(documento.UrlArquivo);

            if (caminho == null)
            {
                return Resultado<(string, string)>.NaoEncontrado("O arquivo deste documento não está disponível.");
            }

            return Resultado<(string, string)>.Ok((caminho, documento.NomeArquivo));
        }

        private async Task<Prontuario?> ResolverProntuario(Guid? prontuarioId, Guid? pacienteId)
        {
            if (prontuarioId.HasValue && prontuarioId.Value != Guid.Empty)
            {
                return await _prontuarios.ObterPorId(prontuarioId.Value);
            }

            if (pacienteId.HasValue && pacienteId.Value != Guid.Empty)
            {
                return await _prontuarios.ObterPorPacienteId(pacienteId.Value)
                       ?? await _prontuarios.ObterOuCriarPorPacienteId(pacienteId.Value);
            }

            return null;
        }

        public static DocumentoDTO MapearParaDTO(DocumentoClinico documento)
        {
            return new DocumentoDTO
            {
                Id = documento.Id,
                ProntuarioId = documento.ProntuarioId,
                NomeArquivo = documento.NomeArquivo,
                TipoDocumento = documento.TipoDocumento,
                UrlArquivo = documento.UrlArquivo,
                TamanhoBytes = documento.TamanhoBytes,
                EnviadoPor = documento.EnviadoPor?.Nome ?? string.Empty,
                DataUpload = documento.DataUpload
            };
        }
    }
}
