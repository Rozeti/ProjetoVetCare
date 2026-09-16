using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>HU-014: troca de mensagens entre tutor e veterinário.</summary>
    public class MensagensUseCase
    {
        /// <summary>Janela usada para considerar um usuário online na lista de conversas.</summary>
        private static readonly TimeSpan JanelaPresenca = TimeSpan.FromMinutes(5);

        private readonly IMensagemRepository _mensagens;
        private readonly IUsuarioRepository _usuarios;
        private readonly IPetRepository _pets;
        private readonly NotificacaoService _notificacoes;
        private readonly UsuarioAtual _usuarioAtual;

        public MensagensUseCase(
            IMensagemRepository mensagens,
            IUsuarioRepository usuarios,
            IPetRepository pets,
            NotificacaoService notificacoes,
            UsuarioAtual usuarioAtual)
        {
            _mensagens = mensagens;
            _usuarios = usuarios;
            _pets = pets;
            _notificacoes = notificacoes;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<MensagemDTO>> Enviar(EnviarMensagemDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Conteudo))
            {
                return Resultado<MensagemDTO>.Invalido("A mensagem não pode ficar vazia.");
            }

            if (dto.DestinatarioId == _usuarioAtual.Id)
            {
                return Resultado<MensagemDTO>.Invalido("Não é possível enviar uma mensagem para si mesmo.");
            }

            var destinatario = await _usuarios.ObterPorId(dto.DestinatarioId);

            if (destinatario == null || destinatario.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<MensagemDTO>.NaoEncontrado("Destinatário não encontrado nesta clínica.");
            }

            if (!destinatario.Ativo)
            {
                return Resultado<MensagemDTO>.Invalido("Este destinatário está inativo e não pode receber mensagens.");
            }

            // HU-014, CA-1 e RN-005: o tutor só conversa com a equipe clínica, nunca com outros tutores.
            if (_usuarioAtual.EhTutor && destinatario.Perfil == Perfis.Tutor)
            {
                return Resultado<MensagemDTO>.NaoAutorizado(
                    "Tutores podem trocar mensagens apenas com a equipe da clínica.");
            }

            var mensagem = new Mensagem
            {
                RemetenteId = _usuarioAtual.Id,
                DestinatarioId = dto.DestinatarioId,
                PacienteId = await ResolverPaciente(dto.PacienteId),
                Conteudo = dto.Conteudo.Trim()
            };

            await _mensagens.Adicionar(mensagem);
            await _mensagens.SalvarAlteracoes();

            // HU-015, CA-3: nova mensagem gera notificação para o destinatário.
            await _notificacoes.NotificarNovaMensagem(dto.DestinatarioId, _usuarioAtual.Nome, mensagem.Conteudo);

            return Resultado<MensagemDTO>.Ok(MapearParaDTO(mensagem, _usuarioAtual.Id, _usuarioAtual.Nome));
        }

        /// <summary>HU-014, CA-2: lista de conversas com status e contador de não lidas.</summary>
        public async Task<Resultado<List<ConversaDTO>>> ListarConversas()
        {
            var mensagens = await _mensagens.ObterMensagensDoUsuario(_usuarioAtual.Id);

            var conversas = mensagens
                .GroupBy(m => m.RemetenteId == _usuarioAtual.Id ? m.DestinatarioId : m.RemetenteId)
                .Select(grupo =>
                {
                    var ultima = grupo.OrderByDescending(m => m.DataEnvio).First();
                    var interlocutor = ultima.RemetenteId == _usuarioAtual.Id ? ultima.Destinatario : ultima.Remetente;

                    return new ConversaDTO
                    {
                        UsuarioId = grupo.Key,
                        Nome = interlocutor?.Nome ?? string.Empty,
                        Perfil = interlocutor?.Perfil ?? string.Empty,
                        UltimaMensagem = ultima.Conteudo,
                        DataUltimaMensagem = ultima.DataEnvio,
                        NaoLidas = grupo.Count(m => m.DestinatarioId == _usuarioAtual.Id && !m.Lida),
                        Online = EstaOnline(interlocutor)
                    };
                })
                .OrderByDescending(c => c.DataUltimaMensagem)
                .ToList();

            return Resultado<List<ConversaDTO>>.Ok(conversas);
        }

        /// <summary>HU-014, CA-3: abrir a conversa marca as mensagens recebidas como lidas.</summary>
        public async Task<Resultado<List<MensagemDTO>>> ObterConversa(Guid outroUsuarioId, bool marcarComoLida = true)
        {
            var outro = await _usuarios.ObterPorId(outroUsuarioId);

            if (outro == null || outro.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<List<MensagemDTO>>.NaoEncontrado("Usuário não encontrado nesta clínica.");
            }

            if (marcarComoLida)
            {
                await _mensagens.MarcarConversaComoLida(_usuarioAtual.Id, outroUsuarioId);
            }

            var mensagens = await _mensagens.ObterConversa(_usuarioAtual.Id, outroUsuarioId);

            var dtos = mensagens
                .Select(m => MapearParaDTO(m, _usuarioAtual.Id, m.Remetente?.Nome ?? string.Empty))
                .ToList();

            return Resultado<List<MensagemDTO>>.Ok(dtos);
        }

        /// <summary>Contatos disponíveis para iniciar uma conversa, conforme o perfil do usuário.</summary>
        public async Task<Resultado<List<UsuarioDTO>>> ListarContatos()
        {
            var todos = await _usuarios.ListarTodos(_usuarioAtual.ClinicaId, null, true);

            // RN-005: o tutor conversa apenas com a equipe clínica, nunca com outros tutores.
            var contatos = todos
                .Where(u => u.Id != _usuarioAtual.Id)
                .Where(u => !_usuarioAtual.EhTutor || u.Perfil != Perfis.Tutor)
                .Select(u => new UsuarioDTO
                {
                    Id = u.Id,
                    Nome = u.Nome,
                    Email = u.Email,
                    Perfil = u.Perfil,
                    Ativo = u.Ativo,
                    UltimoAcesso = u.UltimoAcesso
                })
                .OrderBy(u => u.Nome)
                .ToList();

            return Resultado<List<UsuarioDTO>>.Ok(contatos);
        }

        public async Task<Resultado<int>> ContarNaoLidas()
        {
            return Resultado<int>.Ok(await _mensagens.ContarNaoLidas(_usuarioAtual.Id));
        }

        private async Task<Guid?> ResolverPaciente(Guid? pacienteId)
        {
            if (!pacienteId.HasValue || pacienteId.Value == Guid.Empty)
            {
                return null;
            }

            var pet = await _pets.ObterPorId(pacienteId.Value);

            return pet != null && pet.ClinicaId == _usuarioAtual.ClinicaId ? pet.Id : null;
        }

        private static bool EstaOnline(Usuario? usuario)
        {
            return usuario?.UltimoAcesso.HasValue == true &&
                   DateTime.UtcNow - usuario.UltimoAcesso.Value <= JanelaPresenca;
        }

        private static MensagemDTO MapearParaDTO(Mensagem mensagem, Guid usuarioAtualId, string nomeRemetente)
        {
            return new MensagemDTO
            {
                Id = mensagem.Id,
                RemetenteId = mensagem.RemetenteId,
                NomeRemetente = nomeRemetente,
                DestinatarioId = mensagem.DestinatarioId,
                PacienteId = mensagem.PacienteId,
                NomePaciente = mensagem.Paciente?.Nome,
                Conteudo = mensagem.Conteudo,
                DataEnvio = mensagem.DataEnvio,
                Lida = mensagem.Lida,
                Propria = mensagem.RemetenteId == usuarioAtualId
            };
        }
    }
}
