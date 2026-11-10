using FluentAssertions;
using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.UseCases;
using VetCare.Tests.Suporte;

namespace VetCare.Tests
{
    /// <summary>
    /// Gestão das sessões cadastradas: a inativação (a sessão sai da agenda e das contagens, mas
    /// fica no histórico), a reativação e a lista da clínica por situação.
    /// </summary>
    public class GestaoDeSessoesTests : BaseDeTeste
    {
        private GerenciarSessoesUseCase CasoDeGestao(UsuarioAtual usuarioAtual) => new(
            new SessaoRepository(Contexto),
            new AtendimentoRepository(Contexto),
            new PetRepository(Contexto),
            new TratamentoRepository(Contexto),
            new ClinicaRepository(Contexto),
            new BloqueioAgendaRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private CadastrarSessoesUseCase CasoDeCadastro(UsuarioAtual usuarioAtual) => new(
            new SessaoRepository(Contexto),
            new TratamentoRepository(Contexto),
            new ClinicaRepository(Contexto),
            new VeterinarioRepository(Contexto),
            new BloqueioAgendaRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private ConsultarAgendaUseCase CasoDeAgenda(UsuarioAtual usuarioAtual) => new(
            new SessaoRepository(Contexto),
            new VeterinarioRepository(Contexto),
            new AtendimentoRepository(Contexto),
            new TratamentoRepository(Contexto),
            new ClinicaRepository(Contexto),
            usuarioAtual);

        private RegistrarAtendimentoUseCase CasoDeRegistro(UsuarioAtual usuarioAtual) => new(
            new AtendimentoRepository(Contexto),
            new TratamentoRepository(Contexto),
            new SessaoRepository(Contexto),
            new ProntuarioRepository(Contexto),
            new PetRepository(Contexto),
            new MidiaRepository(Contexto),
            new ObservacaoInternaRepository(Contexto),
            new VersaoRegistroRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Assinador(),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private AtualizarStatusSessaoUseCase CasoDeStatus(UsuarioAtual usuarioAtual) => new(
            new SessaoRepository(Contexto),
            new ClinicaRepository(Contexto),
            Dependencias.Notificacoes(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        /// <summary>Próximo dia útil às 14h locais, dentro do expediente da clínica.</summary>
        private static DateTime ProximoHorarioValido(int diasAFrente = 3)
        {
            var data = DateTime.Now.Date.AddDays(diasAFrente);

            while (data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                data = data.AddDays(1);
            }

            return DateTime.SpecifyKind(data.AddHours(14), DateTimeKind.Local).ToUniversalTime();
        }

        private async Task<Guid> Cadastrar(DateTime inicio)
        {
            var resultado = await CasoDeCadastro(ComoAdministrador()).Executar(new CadastrarSessoesDTO
            {
                TratamentoId = Tratamento.Id,
                VeterinarioId = Veterinario.Id,
                Sessoes = { new HorarioDeSessaoDTO { Inicio = inicio, Fim = inicio.AddHours(1) } }
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
            return resultado.Dados![0].Id;
        }

        /// <summary>Sessão gravada direto no banco, para montar situações no passado.</summary>
        private async Task<Sessao> SessaoNoBanco(DateTime inicio, string status = StatusSessao.Confirmada, bool inativa = false)
        {
            var sessao = new Sessao
            {
                TratamentoId = Tratamento.Id,
                VeterinarioId = Veterinario.Id,
                DataHora = inicio,
                DataHoraFim = inicio.AddHours(1),
                Status = status,
                Inativa = inativa
            };

            Contexto.Sessoes.Add(sessao);
            await Contexto.SaveChangesAsync();
            return sessao;
        }

        private async Task RegistrarAtendimento(Guid sessaoId)
        {
            var resultado = await CasoDeRegistro(ComoVeterinario()).Executar(new CriarAtendimentoDTO
            {
                SessaoId = sessaoId,
                TecnicasAplicadas = "Hidroterapia",
                EscalaDor = 3,
                EvolucaoClinica = "Esteira aquática por 15 minutos",
                ReacaoAnimal = "Tolerou bem"
            });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);
        }

        [Fact]
        public async Task Sessao_inativada_sai_da_agenda_libera_o_horario_e_avisa_o_tutor()
        {
            var horario = ProximoHorarioValido();
            var sessaoId = await Cadastrar(horario);
            var avisosAntes = Contexto.Notificacoes.Count(n => n.UsuarioId == UsuarioTutor.Id);

            var resultado = await CasoDeGestao(ComoAdministrador()).Inativar(sessaoId, new InativarSessaoDTO { Motivo = "Cadastrada em dobro" });

            resultado.Sucesso.Should().BeTrue(resultado.Mensagem);

            var sessao = await Contexto.Sessoes.FindAsync(sessaoId);
            sessao!.Inativa.Should().BeTrue();
            sessao.Observacoes.Should().Contain("Inativada: Cadastrada em dobro");

            var agenda = await CasoDeAgenda(ComoAdministrador()).ConsultarPorVisao(Veterinario.Id, horario, "dia");
            agenda.Dados.Should().BeEmpty("a sessão inativada não aparece na agenda");

            var doTutor = await CasoDeAgenda(ComoTutor()).ConsultarAgendaDoTutor(apenasFuturas: false);
            doTutor.Dados.Should().BeEmpty("o tutor não vê sessões inativadas");

            Contexto.Notificacoes.Count(n => n.UsuarioId == UsuarioTutor.Id).Should().Be(avisosAntes + 1);
            Contexto.RegistrosAuditoria.Should().Contain(r => r.Entidade == "Sessao" && r.Acao == "Inativacao");

            // O horário fica livre para outra sessão.
            (await CasoDeCadastro(ComoAdministrador()).Executar(new CadastrarSessoesDTO
            {
                TratamentoId = Tratamento.Id,
                VeterinarioId = Veterinario.Id,
                Sessoes = { new HorarioDeSessaoDTO { Inicio = horario, Fim = horario.AddHours(1) } }
            })).Sucesso.Should().BeTrue();
        }

        [Fact]
        public async Task Sessao_com_atendimento_registrado_nao_e_inativada()
        {
            var sessao = await SessaoNoBanco(DateTime.UtcNow.AddDays(-1));
            await RegistrarAtendimento(sessao.Id);

            var resultado = await CasoDeGestao(ComoAdministrador()).Inativar(sessao.Id, new InativarSessaoDTO());

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Conflito);
            resultado.Mensagem.Should().Contain("atendimento registrado");
        }

        [Fact]
        public async Task Veterinario_so_inativa_sessoes_da_propria_agenda_e_o_tutor_nenhuma()
        {
            var sessaoId = await Cadastrar(ProximoHorarioValido());

            var outroUsuario = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Outro Veterinário",
                Email = "outro@teste.com",
                Perfil = Perfis.Veterinario,
                SenhaHash = "hash"
            };
            var outroVeterinario = new Veterinario { UsuarioId = outroUsuario.Id, Crmv = "CRMV-DF 2000" };
            Contexto.Usuarios.Add(outroUsuario);
            Contexto.Veterinarios.Add(outroVeterinario);
            await Contexto.SaveChangesAsync();

            var doColega = await CasoDeGestao(ComoUsuario(outroUsuario, Clinica.Id, veterinarioId: outroVeterinario.Id))
                .Inativar(sessaoId, new InativarSessaoDTO());
            var doTutor = await CasoDeGestao(ComoTutor()).Inativar(sessaoId, new InativarSessaoDTO());
            var doVeterinario = await CasoDeGestao(ComoVeterinario()).Inativar(sessaoId, new InativarSessaoDTO());

            doColega.Falha.Should().Be(TipoFalha.NaoAutorizado);
            doTutor.Falha.Should().Be(TipoFalha.NaoAutorizado);
            doVeterinario.Sucesso.Should().BeTrue(doVeterinario.Mensagem);
        }

        [Fact]
        public async Task Sessao_inativada_nao_recebe_atendimento_nem_muda_de_status()
        {
            var sessao = await SessaoNoBanco(DateTime.UtcNow.AddDays(-1), inativa: true);

            var registro = await CasoDeRegistro(ComoVeterinario()).Executar(new CriarAtendimentoDTO
            {
                SessaoId = sessao.Id,
                TecnicasAplicadas = "Hidroterapia",
                EscalaDor = 3,
                EvolucaoClinica = "Esteira aquática",
                ReacaoAnimal = "Tolerou bem"
            });

            var status = await CasoDeStatus(ComoAdministrador())
                .Executar(sessao.Id, new AtualizarStatusSessaoDTO { Status = StatusSessao.Cancelada });

            registro.Sucesso.Should().BeFalse();
            registro.Mensagem.Should().Contain("inativada");
            status.Falha.Should().Be(TipoFalha.Conflito);
        }

        [Fact]
        public async Task Reativacao_confere_se_o_horario_continua_livre()
        {
            var horario = ProximoHorarioValido();
            var primeira = await Cadastrar(horario);

            await CasoDeGestao(ComoAdministrador()).Inativar(primeira, new InativarSessaoDTO());
            var segunda = await Cadastrar(horario);

            var ocupado = await CasoDeGestao(ComoAdministrador()).Reativar(primeira);

            ocupado.Sucesso.Should().BeFalse();
            ocupado.Falha.Should().Be(TipoFalha.Conflito);

            await CasoDeGestao(ComoAdministrador()).Inativar(segunda, new InativarSessaoDTO());
            var livre = await CasoDeGestao(ComoAdministrador()).Reativar(primeira);

            livre.Sucesso.Should().BeTrue(livre.Mensagem);
            (await Contexto.Sessoes.FindAsync(primeira))!.Inativa.Should().BeFalse();
        }

        [Fact]
        public async Task Tratamento_conta_so_as_sessoes_do_plano()
        {
            var inicio = ProximoHorarioValido();
            await Cadastrar(inicio);
            var inativada = await Cadastrar(inicio.AddDays(7));
            await SessaoNoBanco(inicio.AddDays(14), StatusSessao.Cancelada);

            await CasoDeGestao(ComoAdministrador()).Inativar(inativada, new InativarSessaoDTO());

            var contagens = await new SessaoRepository(Contexto).ContarPorTratamentos(new[] { Tratamento.Id });

            contagens[Tratamento.Id].Total.Should().Be(1, "canceladas e inativadas não fazem parte do plano");
        }

        [Fact]
        public async Task Gestao_separa_as_sessoes_por_situacao()
        {
            await SessaoNoBanco(ProximoHorarioValido(), StatusSessao.AguardandoConfirmacao);
            await SessaoNoBanco(DateTime.UtcNow.AddDays(-2));
            var realizada = await SessaoNoBanco(DateTime.UtcNow.AddDays(-3));
            await RegistrarAtendimento(realizada.Id);
            await SessaoNoBanco(DateTime.UtcNow.AddDays(-4), StatusSessao.Cancelada);
            await SessaoNoBanco(DateTime.UtcNow.AddDays(-5), inativa: true);

            var todas = await CasoDeGestao(ComoAdministrador())
                .Listar(Paciente.Id, null, null, null, null, null, new ParametrosPagina());

            todas.Sucesso.Should().BeTrue(todas.Mensagem);
            todas.Dados!.Pagina.Total.Should().Be(4, "sem filtro de situação, as inativadas ficam de fora");
            todas.Dados.Contagens.Should().BeEquivalentTo(new Dictionary<string, int>
            {
                [SituacaoSessao.Agendada] = 1,
                [SituacaoSessao.AguardandoRegistro] = 1,
                [SituacaoSessao.Realizada] = 1,
                [SituacaoSessao.Cancelada] = 1,
                [SituacaoSessao.Inativada] = 1
            });

            var pendentes = await CasoDeGestao(ComoVeterinario())
                .Listar(null, null, null, null, null, "aguardando registro", new ParametrosPagina());

            pendentes.Dados!.Pagina.Itens.Should().ContainSingle()
                .Which.Situacao.Should().Be(SituacaoSessao.AguardandoRegistro);

            var realizadas = await CasoDeGestao(ComoAdministrador())
                .Listar(null, null, null, null, null, SituacaoSessao.Realizada, new ParametrosPagina());

            var item = realizadas.Dados!.Pagina.Itens.Should().ContainSingle().Subject;
            item.NumeroSessao.Should().Be(1);
            item.AtendimentoId.Should().NotBeNull();
        }

        [Fact]
        public async Task Situacao_desconhecida_e_recusada()
        {
            var resultado = await CasoDeGestao(ComoAdministrador())
                .Listar(null, null, null, null, null, "Esquecida", new ParametrosPagina());

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.Validacao);
        }
    }
}
