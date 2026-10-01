using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;
using VetCare.API.Services;

namespace VetCare.API.UseCases
{
    /// <summary>
    /// HU-002: cadastro, edição, ativação/desativação e redefinição de senha dos usuários
    /// da clínica. Cadastrar um Veterinário, Tutor ou Apoio cria, na mesma operação, o
    /// registro profissional correspondente — evitando usuários órfãos sem vínculo.
    /// </summary>
    public class GerenciarUsuariosUseCase
    {
        private readonly IUsuarioRepository _usuarios;
        private readonly IVeterinarioRepository _veterinarios;
        private readonly ITutorRepository _tutores;
        private readonly IApoioRepository _apoios;
        private readonly PasswordHasher _hasher;
        private readonly ContasService _contas;
        private readonly ContasAtivas _contasAtivas;
        private readonly AuditoriaService _auditoria;
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarUsuariosUseCase(
            IUsuarioRepository usuarios,
            IVeterinarioRepository veterinarios,
            ITutorRepository tutores,
            IApoioRepository apoios,
            PasswordHasher hasher,
            ContasService contas,
            ContasAtivas contasAtivas,
            AuditoriaService auditoria,
            UsuarioAtual usuarioAtual)
        {
            _usuarios = usuarios;
            _veterinarios = veterinarios;
            _tutores = tutores;
            _apoios = apoios;
            _hasher = hasher;
            _contas = contas;
            _contasAtivas = contasAtivas;
            _auditoria = auditoria;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<PaginaDe<UsuarioDTO>>> Listar(
            string? perfil,
            string? busca,
            bool? ativo,
            ParametrosPagina parametros)
        {
            var pagina = await _usuarios.Listar(_usuarioAtual.ClinicaId, perfil, busca, ativo, parametros);
            var ids = pagina.Itens.Select(u => u.Id).ToList();

            // Os vínculos da página inteira vêm em duas consultas, e não em uma por usuário.
            var veterinarios = (await _veterinarios.ObterPorUsuarios(ids)).ToDictionary(v => v.UsuarioId);
            var tutores = (await _tutores.ObterPorUsuarios(ids)).ToDictionary(t => t.UsuarioId);

            var dtos = pagina.Itens
                .Select(usuario => MapearParaDTO(
                    usuario,
                    veterinarios.GetValueOrDefault(usuario.Id),
                    tutores.GetValueOrDefault(usuario.Id)))
                .ToList();

            return Resultado<PaginaDe<UsuarioDTO>>.Ok(
                PaginaDe<UsuarioDTO>.Criar(dtos, pagina.Pagina, pagina.Tamanho, pagina.Total));
        }

        public async Task<Resultado<UsuarioDTO>> ObterPorId(Guid id)
        {
            var usuario = await _usuarios.ObterPorId(id);

            if (usuario == null || usuario.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<UsuarioDTO>.NaoEncontrado("Usuário não encontrado.");
            }

            return Resultado<UsuarioDTO>.Ok(await MapearComVinculos(usuario));
        }

        public async Task<Resultado<UsuarioDTO>> Cadastrar(CriarUsuarioDTO dto, Guid clinicaId)
        {
            if (!Perfis.EhValido(dto.Perfil))
            {
                return Resultado<UsuarioDTO>.Invalido(
                    $"Perfil inválido. Use um destes: {string.Join(", ", Perfis.Validos)}.");
            }

            // RN-007: o e-mail precisa ser único no sistema.
            if (await _usuarios.EmailExiste(dto.Email))
            {
                return Resultado<UsuarioDTO>.Conflito("Este e-mail já está em uso por outro usuário.");
            }

            var senhaDefinidaPelaClinica = !string.IsNullOrWhiteSpace(dto.Senha);

            if (senhaDefinidaPelaClinica && PasswordHasher.ValidarForca(dto.Senha) is { } erroDeSenha)
            {
                return Resultado<UsuarioDTO>.Invalido(erroDeSenha);
            }

            var perfil = Perfis.Normalizar(dto.Perfil);

            if (perfil == Perfis.Veterinario)
            {
                if (string.IsNullOrWhiteSpace(dto.Crmv))
                {
                    return Resultado<UsuarioDTO>.Invalido("Informe o CRMV do veterinário.");
                }

                if (await _veterinarios.CrmvExiste(dto.Crmv))
                {
                    return Resultado<UsuarioDTO>.Conflito("Já existe um veterinário cadastrado com este CRMV.");
                }
            }

            var usuario = new Usuario
            {
                ClinicaId = clinicaId,
                Nome = dto.Nome.Trim(),
                Email = dto.Email.Trim().ToLowerInvariant(),
                // Sem senha informada, a conta nasce com uma senha aleatória que ninguém conhece;
                // o usuário define a própria pelo link e código do e-mail de boas-vindas.
                SenhaHash = _hasher.Gerar(senhaDefinidaPelaClinica ? dto.Senha! : _hasher.GerarSenhaProvisoria()),
                Perfil = perfil
            };

            await _usuarios.Adicionar(usuario);

            // O Id é gerado na aplicação, então o vínculo profissional pode ser criado
            // antes de gravar. Uma única persistência evita deixar um usuário sem o
            // registro de Veterinário, Tutor ou Apoio caso algo falhe no meio.
            await CriarVinculoDoPerfil(usuario, dto);

            await _usuarios.SalvarAlteracoes();

            await _contas.EnviarBoasVindas(usuario, senhaDefinidaPelaClinica);

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Criacao, "Usuario", usuario.Id, $"Cadastro de {usuario.Nome} ({perfil})");

            return Resultado<UsuarioDTO>.Ok(
                await MapearComVinculos(usuario),
                senhaDefinidaPelaClinica
                    ? "Usuário cadastrado com sucesso."
                    : "Usuário cadastrado. Ele recebeu por e-mail o link para criar a própria senha.");
        }

        public async Task<Resultado<UsuarioDTO>> Atualizar(Guid id, AtualizarUsuarioDTO dto)
        {
            var usuario = await _usuarios.ObterPorId(id);

            if (usuario == null || usuario.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<UsuarioDTO>.NaoEncontrado("Usuário não encontrado.");
            }

            if (await _usuarios.EmailExiste(dto.Email, id))
            {
                return Resultado<UsuarioDTO>.Conflito("Este e-mail já está em uso por outro usuário.");
            }

            // HU-002, CA-3: a atualização preserva os vínculos existentes do usuário.
            usuario.Nome = dto.Nome.Trim();
            usuario.Email = dto.Email.Trim().ToLowerInvariant();

            _usuarios.Atualizar(usuario);
            await AtualizarVinculoDoPerfil(usuario, dto);

            // Usuário e vínculo profissional saem numa única gravação.
            await _usuarios.SalvarAlteracoes();

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Alteracao, "Usuario", usuario.Id, $"Edição de {usuario.Nome}");

            return Resultado<UsuarioDTO>.Ok(await MapearComVinculos(usuario), "Usuário atualizado com sucesso.");
        }

        public async Task<Resultado> AlterarStatus(Guid id, bool ativo)
        {
            var usuario = await _usuarios.ObterPorId(id);

            if (usuario == null || usuario.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado.NaoEncontrado("Usuário não encontrado.");
            }

            if (id == _usuarioAtual.Id && !ativo)
            {
                return Resultado.Invalido("Você não pode desativar a própria conta.");
            }

            // HU-002, CA-4: desativar impede novos acessos, mas nenhum dado é excluído.
            usuario.Ativo = ativo;
            usuario.TentativasFalhas = 0;
            usuario.BloqueadoAte = null;

            _usuarios.Atualizar(usuario);
            await _usuarios.SalvarAlteracoes();

            // A desativação vale na hora, mesmo para um token ainda dentro do prazo.
            _contasAtivas.Invalidar(usuario.Id);

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.Inativacao, "Usuario", usuario.Id,
                ativo ? $"Ativação de {usuario.Nome}" : $"Desativação de {usuario.Nome}");

            return Resultado.Ok(ativo ? "Usuário ativado com sucesso." : "Usuário desativado com sucesso.");
        }

        public async Task<Resultado<SenhaRedefinidaDTO>> RedefinirSenha(Guid id)
        {
            var usuario = await _usuarios.ObterPorId(id);

            if (usuario == null || usuario.ClinicaId != _usuarioAtual.ClinicaId)
            {
                return Resultado<SenhaRedefinidaDTO>.NaoEncontrado("Usuário não encontrado.");
            }

            // HU-002, CA-5: o sistema gera uma nova senha provisória para o usuário.
            var senhaProvisoria = _hasher.GerarSenhaProvisoria();

            usuario.SenhaHash = _hasher.Gerar(senhaProvisoria);
            usuario.TentativasFalhas = 0;
            usuario.BloqueadoAte = null;

            _usuarios.Atualizar(usuario);
            await _usuarios.SalvarAlteracoes();

            // Além de aparecer para o Administrador, a senha chega ao próprio usuário.
            _contas.EnviarSenhaProvisoria(usuario, senhaProvisoria);

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.RedefinicaoSenha, "Usuario", usuario.Id,
                $"Senha provisória gerada pelo administrador para {usuario.Nome}");

            var resposta = new SenhaRedefinidaDTO
            {
                UsuarioId = usuario.Id,
                Email = usuario.Email,
                SenhaProvisoria = senhaProvisoria
            };

            return Resultado<SenhaRedefinidaDTO>.Ok(resposta, $"Senha provisória gerada e enviada por e-mail para {usuario.Email}.");
        }

        public async Task<Resultado> AlterarPropriaSenha(AlterarSenhaDTO dto)
        {
            var usuario = await _usuarios.ObterPorId(_usuarioAtual.Id);

            if (usuario == null)
            {
                return Resultado.NaoEncontrado("Usuário não encontrado.");
            }

            if (!_hasher.Verificar(dto.SenhaAtual, usuario.SenhaHash, out _))
            {
                return Resultado.Invalido("A senha atual informada está incorreta.");
            }

            if (dto.NovaSenha == dto.SenhaAtual)
            {
                return Resultado.Invalido("A nova senha precisa ser diferente da atual.");
            }

            usuario.SenhaHash = _hasher.Gerar(dto.NovaSenha);

            _usuarios.Atualizar(usuario);
            await _usuarios.SalvarAlteracoes();

            _contas.EnviarAvisoDeSenhaAlterada(usuario);

            await _auditoria.RegistrarDoUsuarioAtual(
                AuditoriaService.Acoes.RedefinicaoSenha, "Usuario", usuario.Id, "Senha alterada pelo próprio usuário");

            return Resultado.Ok("Senha alterada com sucesso.");
        }

        /// <summary>HU-015: o usuário escolhe se quer os avisos também por e-mail e no celular.</summary>
        public async Task<Resultado<UsuarioDTO>> AtualizarPreferenciasDeNotificacao(PreferenciasDeNotificacaoDTO dto)
        {
            var usuario = await _usuarios.ObterPorId(_usuarioAtual.Id);

            if (usuario == null)
            {
                return Resultado<UsuarioDTO>.NaoEncontrado("Usuário não encontrado.");
            }

            usuario.NotificarPorEmail = dto.NotificarPorEmail;
            usuario.NotificarPorPush = dto.NotificarPorPush;

            _usuarios.Atualizar(usuario);
            await _usuarios.SalvarAlteracoes();

            return Resultado<UsuarioDTO>.Ok(await MapearComVinculos(usuario), "Preferências de notificação atualizadas.");
        }

        private async Task CriarVinculoDoPerfil(Usuario usuario, CriarUsuarioDTO dto)
        {
            switch (usuario.Perfil)
            {
                case Perfis.Veterinario:
                    await _veterinarios.Adicionar(new Veterinario
                    {
                        UsuarioId = usuario.Id,
                        Crmv = dto.Crmv?.Trim() ?? string.Empty,
                        Especialidade = string.IsNullOrWhiteSpace(dto.Especialidade)
                            ? "Fisioterapia veterinária"
                            : dto.Especialidade.Trim()
                    });
                    break;

                case Perfis.Tutor:
                    await _tutores.Adicionar(new Tutor
                    {
                        UsuarioId = usuario.Id,
                        Telefone = dto.Telefone?.Trim() ?? string.Empty,
                        Endereco = dto.Endereco?.Trim() ?? string.Empty,
                        Cpf = dto.Cpf?.Trim() ?? string.Empty
                    });
                    break;

                case Perfis.Apoio:
                    await _apoios.Adicionar(new ApoioAdministrativo
                    {
                        UsuarioId = usuario.Id,
                        Setor = string.IsNullOrWhiteSpace(dto.Setor) ? "Recepção" : dto.Setor.Trim()
                    });
                    break;
            }
        }

        /// <summary>Campos omitidos (nulos) mantêm o valor atual; só o que veio preenchido é alterado.</summary>
        private async Task AtualizarVinculoDoPerfil(Usuario usuario, AtualizarUsuarioDTO dto)
        {
            switch (usuario.Perfil)
            {
                case Perfis.Veterinario:
                {
                    var veterinario = await _veterinarios.ObterPorUsuarioId(usuario.Id);

                    if (veterinario != null)
                    {
                        veterinario.Crmv = dto.Crmv?.Trim() ?? veterinario.Crmv;
                        veterinario.Especialidade = dto.Especialidade?.Trim() ?? veterinario.Especialidade;
                        _veterinarios.Atualizar(veterinario);
                    }

                    break;
                }

                case Perfis.Tutor:
                {
                    var tutor = await _tutores.ObterPorUsuarioId(usuario.Id);

                    if (tutor != null)
                    {
                        tutor.Telefone = dto.Telefone?.Trim() ?? tutor.Telefone;
                        tutor.Endereco = dto.Endereco?.Trim() ?? tutor.Endereco;
                        tutor.Cpf = dto.Cpf?.Trim() ?? tutor.Cpf;
                        _tutores.Atualizar(tutor);
                    }

                    break;
                }

                case Perfis.Apoio:
                {
                    var apoio = await _apoios.ObterPorUsuarioId(usuario.Id);

                    if (apoio != null && !string.IsNullOrWhiteSpace(dto.Setor))
                    {
                        apoio.Setor = dto.Setor.Trim();
                        _apoios.Atualizar(apoio);
                    }

                    break;
                }
            }
        }

        private async Task<UsuarioDTO> MapearComVinculos(Usuario usuario)
        {
            var veterinario = usuario.Perfil == Perfis.Veterinario
                ? await _veterinarios.ObterPorUsuarioId(usuario.Id)
                : null;

            var tutor = usuario.Perfil == Perfis.Tutor
                ? await _tutores.ObterPorUsuarioId(usuario.Id)
                : null;

            return MapearParaDTO(usuario, veterinario, tutor);
        }

        public static UsuarioDTO MapearParaDTO(Usuario usuario, Veterinario? veterinario, Tutor? tutor)
        {
            return new UsuarioDTO
            {
                Id = usuario.Id,
                Nome = usuario.Nome,
                Email = usuario.Email,
                Perfil = usuario.Perfil,
                Ativo = usuario.Ativo,
                DataCadastro = usuario.DataCadastro,
                UltimoAcesso = usuario.UltimoAcesso,
                NotificarPorEmail = usuario.NotificarPorEmail,
                NotificarPorPush = usuario.NotificarPorPush,
                VeterinarioId = veterinario?.Id,
                Crmv = veterinario?.Crmv,
                Especialidade = veterinario?.Especialidade,
                TutorId = tutor?.Id,
                Telefone = tutor?.Telefone,
                Endereco = tutor?.Endereco,
                Cpf = tutor?.Cpf
            };
        }
    }
}
