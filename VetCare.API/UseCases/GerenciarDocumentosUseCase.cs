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
        private readonly IPetRepository _pets;
        private readonly ArmazenamentoArquivos _armazenamento;
        private readonly AssinadorDeArquivos _assinador;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarDocumentosUseCase(
            IDocumentoRepository documentos,
            IProntuarioRepository prontuarios,
            IPetRepository pets,
            ArmazenamentoArquivos armazenamento,
            AssinadorDeArquivos assinador,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _documentos = documentos;
            _prontuarios = prontuarios;
            _pets = pets;
            _armazenamento = armazenamento;
            _assinador = assinador;
            _auditoria = auditoria;
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

            var prontuario = await ResolverProntuarioDaClinica(dto.ProntuarioId, dto.PacienteId);

            if (prontuario == null)
            {
                return Resultado<DocumentoDTO>.NaoEncontrado(
                    "Informe um prontuário ou um paciente válido desta clínica para vincular o documento.");
            }

            var caminho = await _armazenamento.Salvar(dto.Arquivo, AssinadorDeArquivos.PastaDeDocumentos);

            var documento = new DocumentoClinico
            {
                ProntuarioId = prontuario.Id,
                EnviadoPorId = _usuarioAtual.Id,
                NomeArquivo = dto.Arquivo.FileName,
                TipoDocumento = string.IsNullOrWhiteSpace(dto.TipoDocumento) ? "Outro" : dto.TipoDocumento.Trim(),
                UrlArquivo = caminho,
                TamanhoBytes = dto.Arquivo.Length
            };

            await _documentos.Adicionar(documento);
            await _documentos.SalvarAlteracoes();

            var salvo = await _documentos.ObterPorId(documento.Id);

            return Resultado<DocumentoDTO>.Ok(MapearParaDTO(salvo ?? documento, _assinador), "Documento anexado com sucesso.");
        }

        public async Task<Resultado<List<DocumentoDTO>>> ListarPorPaciente(Guid pacienteId)
        {
            var pet = await _pets.ObterPorId(pacienteId);

            if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<DocumentoDTO>>.NaoEncontrado("Paciente não encontrado.");
            }

            // HU-013: o tutor acessa apenas os documentos dos próprios pets; o veterinário, os dos seus pacientes.
            if (!AcessoAoPaciente.Permitido(_usuarioAtual, pet))
            {
                return Resultado<List<DocumentoDTO>>.NaoAutorizado("Você não tem acesso a este prontuário.");
            }

            var prontuario = await _prontuarios.ObterPorPacienteId(pacienteId);

            if (prontuario == null)
            {
                return Resultado<List<DocumentoDTO>>.Ok(new List<DocumentoDTO>());
            }

            var documentos = await _documentos.ObterPorProntuario(prontuario.Id);

            return Resultado<List<DocumentoDTO>>.Ok(documentos.Select(d => MapearParaDTO(d, _assinador)).ToList());
        }

        /// <summary>HU-012, CA-2: visualização e download pelo usuário autorizado, com registro na auditoria.</summary>
        public async Task<Resultado<(string Caminho, string NomeArquivo)>> ObterParaDownload(Guid id)
        {
            var documento = await _documentos.ObterPorId(id);

            if (documento == null || documento.Prontuario?.Paciente?.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<(string, string)>.NaoEncontrado("Documento não encontrado.");
            }

            if (!AcessoAoPaciente.Permitido(_usuarioAtual, documento.Prontuario?.Paciente))
            {
                return Resultado<(string, string)>.NaoAutorizado("Você não tem acesso a este documento.");
            }

            var caminho = _armazenamento.ResolverCaminhoFisico(documento.UrlArquivo);

            if (caminho == null)
            {
                return Resultado<(string, string)>.NaoEncontrado("O arquivo deste documento não está disponível.");
            }

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Download, "DocumentoClinico", documento.Id, $"Download de {documento.NomeArquivo}");

            return Resultado<(string, string)>.Ok((caminho, documento.NomeArquivo));
        }

        /// <summary>
        /// Localiza (ou cria) o prontuário, mas só depois de confirmar que o paciente é desta
        /// clínica. Sem o paciente carregado a verificação não acontece, então a resposta é
        /// "não encontrado" em vez de deixar passar.
        /// </summary>
        private async Task<Prontuario?> ResolverProntuarioDaClinica(Guid? prontuarioId, Guid? pacienteId)
        {
            Prontuario? prontuario = null;

            if (prontuarioId.HasValue && prontuarioId.Value != Guid.Empty)
            {
                prontuario = await _prontuarios.ObterPorId(prontuarioId.Value);
            }
            else if (pacienteId.HasValue && pacienteId.Value != Guid.Empty)
            {
                var pet = await _pets.ObterPorId(pacienteId.Value);

                if (pet == null || pet.ClinicaId != _usuarioAtual.ClinicaId)
                {
                    return null;
                }

                prontuario = await _prontuarios.ObterPorPacienteId(pet.Id)
                             ?? await _prontuarios.ObterOuCriarPorPacienteId(pet.Id);

                prontuario.Paciente ??= pet;
            }

            if (prontuario?.Paciente == null || prontuario.Paciente.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return null;
            }

            return prontuario;
        }

        /// <summary>A URL sai assinada: o arquivo só abre para quem recebeu esta resposta.</summary>
        public static DocumentoDTO MapearParaDTO(DocumentoClinico documento, AssinadorDeArquivos assinador)
        {
            return new DocumentoDTO
            {
                Id = documento.Id,
                ProntuarioId = documento.ProntuarioId,
                NomeArquivo = documento.NomeArquivo,
                TipoDocumento = documento.TipoDocumento,
                UrlArquivo = assinador.Assinar(documento.UrlArquivo),
                TamanhoBytes = documento.TamanhoBytes,
                EnviadoPor = documento.EnviadoPor?.Nome ?? string.Empty,
                DataUpload = documento.DataUpload
            };
        }
    }
}
