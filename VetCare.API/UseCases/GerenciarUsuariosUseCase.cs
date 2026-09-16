using VetCare.API.Common;
using VetCare.API.Data;
using VetCare.API.DTOs;
using VetCare.API.Models;
using VetCare.API.Security;

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
        private readonly UsuarioAtual _usuarioAtual;

        public GerenciarUsuariosUseCase(
            IUsuarioRepository usuarios,
            IVeterinarioRepository veterinarios,
            ITutorRepository tutores,
            IApoioRepository apoios,
            PasswordHasher hasher,
            UsuarioAtual usuarioAtual)
        {
            _usuarios = usuarios;
            _veterinarios = veterinarios;
            _tutores = tutores;
            _apoios = apoios;
            _hasher = hasher;
            _usuarioAtual = usuarioAtual;
        }

        public async Task<Resultado<PaginaDe<UsuarioDTO>>> Listar(
            string? perfil,
            string? busca,
            bool? ativo,
            ParametrosPagina parametros)
        {
            var pagina = await _usuarios.Listar(_usuarioAtual.ClinicaId, perfil, busca, ativo, parametros);
            var dtos = new List<UsuarioDTO>(pagina.Itens.Count);

            foreach (var usuario in pagina.Itens)
            {
                dtos.Add(await MontarDto(usuario));
            }

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

            return Resultado<UsuarioDTO>.Ok(await MontarDto(usuario));
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
                SenhaHash = _hasher.Gerar(dto.Senha),
                Perfil = perfil
            };

            await _usuarios.Adicionar(usuario);

            // O Id é gerado na aplicação, então o vínculo profissional pode ser criado
            // antes de gravar. Uma única persistência evita deixar um usuário sem o
            // registro de Veterinário, Tutor ou Apoio caso algo falhe no meio.
            await CriarVinculoDoPerfil(usuario, dto);

            await _usuarios.SalvarAlteracoes();

            return Resultado<UsuarioDTO>.Ok(await MontarDto(usuario), "Usuário cadastrado com sucesso.");
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
            await _usuarios.SalvarAlteracoes();

            await AtualizarVinculoDoPerfil(usuario, dto);

            return Resultado<UsuarioDTO>.Ok(await MontarDto(usuario), "Usuário atualizado com sucesso.");
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

            var resposta = new SenhaRedefinidaDTO
            {
                UsuarioId = usuario.Id,
                Email = usuario.Email,
                SenhaProvisoria = senhaProvisoria
            };

            return Resultado<SenhaRedefinidaDTO>.Ok(resposta, "Senha redefinida com sucesso.");
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

            usuario.SenhaHash = _hasher.Gerar(dto.NovaSenha);

            _usuarios.Atualizar(usuario);
            await _usuarios.SalvarAlteracoes();

            return Resultado.Ok("Senha alterada com sucesso.");
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
                        Especialidade = dto.Especialidade?.Trim() ?? "Fisioterapia veterinária"
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
                        Setor = dto.Setor?.Trim() ?? "Recepção"
                    });
                    break;
            }
        }

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
                        await _veterinarios.SalvarAlteracoes();
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
                        await _tutores.SalvarAlteracoes();
                    }

                    break;
                }
            }
        }

        private async Task<UsuarioDTO> MontarDto(Usuario usuario)
        {
            var dto = new UsuarioDTO
            {
                Id = usuario.Id,
                Nome = usuario.Nome,
                Email = usuario.Email,
                Perfil = usuario.Perfil,
                Ativo = usuario.Ativo,
                DataCadastro = usuario.DataCadastro,
                UltimoAcesso = usuario.UltimoAcesso
            };

            if (usuario.Perfil == Perfis.Veterinario)
            {
                var veterinario = await _veterinarios.ObterPorUsuarioId(usuario.Id);
                dto.VeterinarioId = veterinario?.Id;
                dto.Crmv = veterinario?.Crmv;
                dto.Especialidade = veterinario?.Especialidade;
            }
            else if (usuario.Perfil == Perfis.Tutor)
            {
                var tutor = await _tutores.ObterPorUsuarioId(usuario.Id);
                dto.TutorId = tutor?.Id;
                dto.Telefone = tutor?.Telefone;
                dto.Endereco = tutor?.Endereco;
            }

            return dto;
        }
    }
}
