namespace VetCare.API.DTOs
{
    /// <summary>Uma alteração ocorrida no banco, entregue às telas já abertas.</summary>
    public class EventoAtualizacaoDTO
    {
        public long Versao { get; set; }

        /// <summary>Recurso afetado em minúsculas: "sessoes", "pets", "mensagens"...</summary>
        public string Recurso { get; set; } = string.Empty;

        /// <summary>"criado", "atualizado" ou "removido".</summary>
        public string Acao { get; set; } = string.Empty;

        /// <summary>
        /// Texto pronto para exibição. Vem vazio para o perfil Tutor: ele recebe apenas o
        /// aviso de que algo mudou e recarrega os próprios dados, que a API já filtra
        /// (RN-003 e HU-013, CA-1).
        /// </summary>
        public string Descricao { get; set; } = string.Empty;

        /// <summary>Quem provocou a alteração; também omitido para o perfil Tutor.</summary>
        public string Autor { get; set; } = string.Empty;

        /// <summary>Verdadeiro quando foi o próprio usuário que provocou a alteração.</summary>
        public bool Propria { get; set; }

        public DateTime Em { get; set; }
    }

    public class FeedAtualizacoesDTO
    {
        /// <summary>Versão a devolver na próxima consulta, no parâmetro <c>desde</c>.</summary>
        public long Versao { get; set; }

        /// <summary>
        /// Indica que o cliente ficou tempo demais fora e perdeu eventos: em vez de tentar
        /// aplicar alterações parciais, ele deve recarregar a tela inteira.
        /// </summary>
        public bool Reiniciar { get; set; }

        public List<EventoAtualizacaoDTO> Eventos { get; set; } = new();
    }
}
