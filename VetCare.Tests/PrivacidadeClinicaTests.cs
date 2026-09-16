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
    /// RN-003 e RNF-003: a separação entre o que o tutor vê e o que é restrito à equipe
    /// clínica. O critério de aceite exige 100% de acerto, então a verificação é feita
    /// tanto pelo indicador quanto pelo conteúdo devolvido.
    /// </summary>
    public class PrivacidadeClinicaTests : BaseDeTeste
    {
        private const string ConteudoRestrito =
            "Avaliar nova intervenção cirúrgica. Não comunicar ao tutor antes da reavaliação.";

        private ConsultarProntuarioUseCase CriarCasoDeUso(UsuarioAtual usuarioAtual) => new(
            new ProntuarioRepository(Contexto),
            new PetRepository(Contexto),
            new TratamentoRepository(Contexto),
            new SessaoRepository(Contexto),
            new VacinaRepository(Contexto),
            new PrescricaoRepository(Contexto),
            new AlergiaRepository(Contexto),
            Dependencias.Auditoria(Contexto, usuarioAtual),
            usuarioAtual);

        private async Task<AvaliacaoClinica> RegistrarAvaliacaoComObservacaoInterna()
        {
            var avaliacao = new AvaliacaoClinica
            {
                TratamentoId = Tratamento.Id,
                ProntuarioId = Prontuario.Id,
                VeterinarioId = Veterinario.Id,
                QueixaPrincipal = "Claudicação do membro posterior",
                Anamnese = "Pós-operatório",
                ExameFisico = "Atrofia muscular",
                HipoteseDiagnostica = "Rigidez articular",
                PlanoTerapeutico = "Hidroterapia"
            };

            Contexto.AvaliacoesClinicas.Add(avaliacao);

            Contexto.ObservacoesInternas.Add(new ObservacaoInterna
            {
                ProntuarioId = Prontuario.Id,
                AvaliacaoId = avaliacao.Id,
                AutorId = UsuarioVeterinario.Id,
                Conteudo = ConteudoRestrito
            });

            await Contexto.SaveChangesAsync();

            return avaliacao;
        }

        [Fact]
        public async Task Veterinario_enxerga_a_observacao_interna()
        {
            await RegistrarAvaliacaoComObservacaoInterna();

            var resultado = await CriarCasoDeUso(ComoVeterinario()).Executar(Paciente.Id);

            resultado.Sucesso.Should().BeTrue();
            resultado.Dados!.ExibeObservacoesInternas.Should().BeTrue();

            resultado.Dados.Historico
                .SelectMany(i => i.ObservacoesInternas)
                .Should().ContainSingle(o => o.Conteudo == ConteudoRestrito);
        }

        [Fact]
        public async Task Administrador_enxerga_a_observacao_interna()
        {
            await RegistrarAvaliacaoComObservacaoInterna();

            var resultado = await CriarCasoDeUso(ComoAdministrador()).Executar(Paciente.Id);

            resultado.Dados!.ExibeObservacoesInternas.Should().BeTrue();
        }

        [Fact]
        public async Task Tutor_nao_recebe_nenhuma_observacao_interna()
        {
            await RegistrarAvaliacaoComObservacaoInterna();

            var resultado = await CriarCasoDeUso(ComoTutor()).Executar(Paciente.Id);

            resultado.Sucesso.Should().BeTrue();
            resultado.Dados!.ExibeObservacoesInternas.Should().BeFalse();

            resultado.Dados.Historico
                .SelectMany(i => i.ObservacoesInternas)
                .Should().BeEmpty("a RN-003 não admite exceções de visualização");
        }

        [Fact]
        public async Task Conteudo_restrito_nao_aparece_em_nenhum_campo_da_resposta_ao_tutor()
        {
            await RegistrarAvaliacaoComObservacaoInterna();

            var resultado = await CriarCasoDeUso(ComoTutor()).Executar(Paciente.Id);

            // Serializar a resposta inteira cobre qualquer campo que viesse a ser
            // adicionado ao DTO no futuro e acabasse carregando o conteúdo restrito.
            var json = System.Text.Json.JsonSerializer.Serialize(resultado.Dados);

            json.Should().NotContain("intervenção cirúrgica");
            json.Should().NotContain("Não comunicar ao tutor");
        }

        [Fact]
        public async Task Tutor_nao_acessa_o_prontuario_de_pet_de_outro_tutor()
        {
            var outroUsuario = new Usuario
            {
                ClinicaId = Clinica.Id,
                Nome = "Outro Tutor",
                Email = "outro.tutor@teste.com",
                Perfil = Perfis.Tutor,
                SenhaHash = "hash"
            };

            var outroTutor = new Tutor { UsuarioId = outroUsuario.Id };

            Contexto.Usuarios.Add(outroUsuario);
            Contexto.Tutores.Add(outroTutor);
            await Contexto.SaveChangesAsync();

            var comoOutroTutor = ComoUsuario(outroUsuario, Clinica.Id, tutorId: outroTutor.Id);

            var resultado = await CriarCasoDeUso(comoOutroTutor).Executar(Paciente.Id);

            resultado.Sucesso.Should().BeFalse();
            resultado.Falha.Should().Be(TipoFalha.NaoAutorizado);
        }

        [Fact]
        public async Task Caso_de_uso_de_observacao_interna_recusa_o_tutor()
        {
            var usuarioAtual = ComoTutor();

            var casoDeUso = new RegistrarObservacaoInternaUseCase(
                new ObservacaoInternaRepository(Contexto),
                new ProntuarioRepository(Contexto),
                usuarioAtual);

            var leitura = await casoDeUso.ListarPorPaciente(Paciente.Id);
            leitura.Falha.Should().Be(TipoFalha.NaoAutorizado);

            var escrita = await casoDeUso.Executar(new CriarObservacaoInternaDTO
            {
                PacienteId = Paciente.Id,
                Conteudo = "tentativa"
            });

            escrita.Falha.Should().Be(TipoFalha.NaoAutorizado);
        }

        [Fact]
        public async Task Alertas_clinicos_sao_visiveis_ao_tutor()
        {
            Contexto.AlergiasCondicoes.Add(new AlergiaCondicao
            {
                PacienteId = Paciente.Id,
                RegistradoPorId = UsuarioVeterinario.Id,
                Tipo = "Alergia",
                Descricao = "Alergia a dipirona",
                Gravidade = "Grave"
            });

            await Contexto.SaveChangesAsync();

            var resultado = await CriarCasoDeUso(ComoTutor()).Executar(Paciente.Id);

            // Diferente da observação interna, o alerta é informação de segurança que o
            // tutor precisa conhecer.
            resultado.Dados!.AlertasClinicos.Should().ContainSingle(a => a.Descricao == "Alergia a dipirona");
        }
    }
}
