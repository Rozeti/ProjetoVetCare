namespace VetCare.API.Common
{
    /// <summary>
    /// O banco guarda tudo em UTC, mas o expediente da clínica, o "dia de hoje" dos
    /// indicadores e as datas escritas nos e-mails são do fuso em que a clínica vive. Este
    /// relógio faz a conversão sempre pelo fuso configurado (<c>Aplicacao:FusoHorario</c>),
    /// e não pelo do servidor — um contêiner roda em UTC, e sem isso uma sessão às 17h
    /// seria recusada como "fora do expediente".
    /// </summary>
    public sealed class RelogioDaClinica
    {
        public const string FusoPadrao = "America/Sao_Paulo";

        private static RelogioDaClinica _padrao = new(FusoPadrao);

        /// <summary>Instância usada pelos pontos estáticos do código; configurada na subida da API.</summary>
        public static RelogioDaClinica Padrao => _padrao;

        public static void Configurar(string? fusoHorario) => _padrao = new RelogioDaClinica(fusoHorario);

        public TimeZoneInfo Fuso { get; }

        public RelogioDaClinica(string? fusoHorario)
        {
            Fuso = Resolver(fusoHorario);
        }

        /// <summary>Agora, no fuso da clínica. Kind fica Unspecified de propósito: não é UTC nem o fuso do servidor.</summary>
        public DateTime Agora => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Fuso);

        public DateTime Hoje => Agora.Date;

        public DateTime ParaLocal(DateTime instante)
        {
            var utc = instante.Kind switch
            {
                DateTimeKind.Utc => instante,
                DateTimeKind.Local => instante.ToUniversalTime(),
                _ => DateTime.SpecifyKind(instante, DateTimeKind.Utc)
            };

            return TimeZoneInfo.ConvertTimeFromUtc(utc, Fuso);
        }

        /// <summary>Interpreta um horário escrito "na hora da clínica" e devolve o instante em UTC.</summary>
        public DateTime ParaUtc(DateTime horarioLocal) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(horarioLocal, DateTimeKind.Unspecified), Fuso);

        /// <summary>
        /// Normaliza o que chega numa requisição: com "Z" é UTC; com deslocamento o .NET
        /// já converteu para o fuso do servidor; sem nada é o horário da clínica, que é o
        /// que uma pessoa digita.
        /// </summary>
        public DateTime NormalizarParaUtc(DateTime valor) => valor.Kind switch
        {
            DateTimeKind.Utc => valor,
            DateTimeKind.Local => valor.ToUniversalTime(),
            _ => ParaUtc(valor)
        };

        /// <summary>Dia de calendário da clínica correspondente ao valor recebido, seja ele UTC ou não.</summary>
        public DateTime DiaDaClinica(DateTime valor) =>
            valor.Kind == DateTimeKind.Unspecified ? valor.Date : ParaLocal(valor).Date;

        /// <summary>Intervalo [início, fim) em UTC que cobre um dia inteiro da clínica.</summary>
        public (DateTime Inicio, DateTime Fim) IntervaloDoDia(DateTime dia)
        {
            var inicio = DiaDaClinica(dia);
            return (ParaUtc(inicio), ParaUtc(inicio.AddDays(1)));
        }

        public string Formatar(DateTime instante, string formato = "dd/MM/yyyy 'às' HH:mm") =>
            ParaLocal(instante).ToString(formato);

        private static TimeZoneInfo Resolver(string? id)
        {
            foreach (var candidato in new[] { id, FusoPadrao })
            {
                if (string.IsNullOrWhiteSpace(candidato))
                {
                    continue;
                }

                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(candidato);
                }
                catch (TimeZoneNotFoundException)
                {
                    // Tenta o próximo candidato.
                }
                catch (InvalidTimeZoneException)
                {
                    // Idem.
                }
            }

            return TimeZoneInfo.Local;
        }
    }
}
