# VetCare

Plataforma de gestão clínica veterinária da **Clínica VetSPA**, especializada em
fisioterapia veterinária. Substitui cadernos, planilhas avulsas e mensageiros genéricos
por um sistema único de pacientes, tutores, agenda, prontuário, mídias e comunicação.

O sistema é composto por três aplicações e um projeto de testes:

| Projeto | O que é | Tecnologia |
|---|---|---|
| `VetCare.API` | API REST e regras de negócio | .NET 10, EF Core, PostgreSQL |
| `VetCare.Web` | Portal da clínica (Administrador, Veterinário, Apoio) e portal web do Tutor | React 19, TypeScript, Vite, Tailwind 4 |
| `VetCare.Mobile` | Aplicativo do Tutor | Expo SDK 57, React Native 0.86 |
| `VetCare.Tests` | Testes unitários das regras de negócio | xUnit, EF Core InMemory, FluentAssertions |

A arquitetura segue o definido no Documento de Arquitetura de Software: camadas
(apresentação → controle → negócio → persistência → banco) com o padrão MVC, comunicação
por API REST sobre HTTPS, autenticação por JWT e controle de acesso por perfil.

---

## Como executar

> Se esta é a primeira vez, siga o [guia passo a passo](COMO-USAR.md), que explica cada comando
> e mostra como conferir que os dados estão sendo gravados no banco.

### Opção A — Docker (pilha completa)

Sobe PostgreSQL, API e portal web de uma vez:

```bash
cp .env.example .env     # ajuste as senhas e a chave JWT
docker compose up -d
```

| Serviço | Endereço |
|---|---|
| Portal web | http://localhost:8080 |
| API | http://localhost:5265 |
| PostgreSQL | localhost:5432 |

A pilha sobe em `Production`, onde a documentação interativa fica desligada. Para
consultá-la, defina `ASPNETCORE_ENVIRONMENT=Development` no `.env`.

O banco é criado e migrado automaticamente na primeira subida. Os volumes
`dados-postgres` e `arquivos-api` preservam dados e mídias entre atualizações da imagem.

### Opção B — Execução local

**1. Banco de dados.** É necessário um PostgreSQL acessível. A string de conexão fica em
`VetCare.API/appsettings.json`:

```json
"DefaultConnection": "Host=localhost;Port=5432;Database=vetcare_db;Username=postgres;Password=admin"
```

Não é preciso criar tabelas manualmente: a API aplica as migrações pendentes ao iniciar.

**2. API.**

```bash
cd VetCare.API
dotnet run
```

Sobe em `http://localhost:5265`.

**3. Portal web.**

```bash
cd VetCare.Web
npm install
npm run dev
```

Abre em `http://localhost:5173`. Para apontar para outra API, defina `VITE_API_URL`.

**4. Aplicativo do tutor.**

```bash
cd VetCare.Mobile
npm install
npx expo start
```

Leia o QR Code com o Expo Go. O aplicativo descobre sozinho o endereço da API a partir do
host do Metro, então normalmente não é preciso configurar IP. Para apontar para outro
servidor, defina `EXPO_PUBLIC_API_URL`.

Requisito: o celular precisa estar na mesma rede do computador que roda a API. O
[guia passo a passo](COMO-USAR.md) traz o diagnóstico dos casos em que a conexão não
acontece.

### Primeiro acesso

Com o banco vazio, a API cria a clínica e **uma única conta de Administrador**:

| E-mail | Senha |
|---|---|
| `admin@vetcare.com` | `vetcare123` |

> **Troque essa senha no primeiro acesso.** Ela existe apenas para destravar a gestão de
> usuários; todo o restante do cadastro (equipe, tutores e pacientes) é feito pelo próprio
> sistema.

Para avaliar o sistema com dados de exemplo em vez de cadastrar tudo à mão, use
[`testes/criar-dados-demonstracao.sh`](testes/README.md).

### Monitoramento

| Endpoint | Para quê |
|---|---|
| `GET /health` | a aplicação está de pé |
| `GET /health/pronto` | a aplicação **e o banco** estão respondendo |
| `GET /scalar` | documentação interativa da API (fora de produção) |

---

## Funcionalidades

### Clínica e equipe
- Cadastro de usuários nos quatro perfis, com ativação e desativação.
- Dados da clínica, duração padrão das sessões e prazo mínimo de cancelamento.
- Trilha de auditoria de tudo o que é criado, alterado, excluído e consultado no
  prontuário, com filtro por entidade, usuário e período.

### Pacientes
- Ficha completa: espécie, raça, pelagem, microchip, castração, peso e idade descritiva.
- Alertas clínicos (alergias, condições crônicas, cirurgias) exibidos em destaque no
  prontuário e visíveis também ao tutor.
- Carteira de vacinação com fabricante, lote, veterinário aplicador e classificação
  automática da dose em **em dia**, **a vencer**, **vencida** ou **dose única**.
- Registro de óbito, que inativa o paciente e encerra os tratamentos em aberto.
- **Cadastro feito pelo próprio tutor**, na web e no aplicativo, para o animal recém-adquirido
  que a clínica ainda não conhece — sem precisar ir até o balcão só para isso. O vínculo com o
  responsável sai do token de quem está autenticado, nunca do corpo da requisição (RN-001), e a
  origem do cadastro fica registrada na auditoria.

### Agenda
- Agenda individual do veterinário (Dia/Semana/Mês) e agenda geral da clínica.
- Bloqueio de períodos por veterinário (férias, congressos), respeitado no agendamento.
- Confirmação e cancelamento pelo tutor, pela web ou pelo aplicativo.
- Lembretes automáticos de confirmação e de vacinas a vencer, em segundo plano.

### Prontuário
- Linha do tempo, evolução de peso e de dor, tratamentos, mídias e documentos.
- Avaliação clínica com os cinco campos exigidos pela RN-010.
- Atendimentos fisioterapêuticos com técnicas, sinais vitais e escala de dor.
- Observações internas restritas à equipe clínica.
- Receituário com múltiplos medicamentos, via, posologia e orientações.
- Impressão de receita e de prontuário, e exportação de listagens em CSV.

### Comunicação
- Mensagens entre tutor e clínica, com histórico e contagem de não lidas.
- Notificações no portal e no aplicativo.

### Atualização em tempo real
Toda gravação no banco é anunciada às telas que já estão abertas, em qualquer perfil e em
qualquer das duas plataformas. Quando o tutor confirma a presença pelo celular, a agenda do
veterinário, a agenda geral do apoio e o painel do administrativo mudam em menos de um
segundo — e o mesmo vale para cancelamentos, novos pacientes, registros no prontuário e
mensagens. Detalhes do funcionamento em [Sincronia entre as telas](#sincronia-entre-as-telas).

### Acesso
- Autenticação JWT, bloqueio após tentativas seguidas e limitação de requisições.
- Recuperação de senha por token de uso único com prazo de validade.

---

## Cobertura dos requisitos

Todas as Histórias de Usuário do Documento de Requisitos estão implementadas.

| HU | Funcionalidade | Onde |
|---|---|---|
| HU-001 | Autenticar usuário | Web, Mobile |
| HU-002 | Gerenciar usuários | Web (Administrador) |
| HU-003 | Gerenciar pacientes | Web (equipe); cadastro pelo próprio tutor na Web e no Mobile |
| HU-004 | Agenda individual do veterinário (Dia/Semana/Mês) | Web |
| HU-005 | Agenda geral da clínica | Web (Administrador/Apoio) |
| HU-006 | Confirmar ou cancelar presença | Web, Mobile (Tutor) |
| HU-007 | Registrar avaliação clínica | Web |
| HU-008 | Registrar atendimento fisioterapêutico | Web |
| HU-009 | Registrar observações internas | Web |
| HU-010 | Anexar mídias à sessão | Web |
| HU-011 | Consultar prontuário | Web, Mobile |
| HU-012 | Gerenciar documentos clínicos | Web |
| HU-013 | Acompanhar tratamento | Web, Mobile (Tutor) |
| HU-014 | Trocar mensagens | Web, Mobile |
| HU-015 | Receber notificações | Web, Mobile |
| HU-016 | Consultar indicadores do dia | Web |
| HU-017 | Consultar relatórios de produtividade | Web |

### Regras de negócio

| RN | Onde é garantida |
|---|---|
| RN-001 Associação tutor–paciente | `GerenciarPacientesUseCase`; chave estrangeira obrigatória. No cadastro feito pelo tutor o responsável vem do token, não do corpo da requisição |
| RN-002 Exclusividade de horário | `SessaoRepository.ExisteConflitoHorario`, considerando a duração da sessão, mais os bloqueios de agenda |
| RN-003 Observações internas restritas | `ConsultarProntuarioUseCase` nem consulta as observações para o Tutor; rota dedicada fechada por perfil |
| RN-004 Integridade do prontuário | Registros não são excluídos; cada edição arquiva a versão anterior em `VersoesRegistrosClinicos`, e nenhuma exclusão do domínio é feita em cascata |
| RN-005 Controle de acesso por perfil | `[Authorize(Roles = ...)]` nos controllers e verificação nos casos de uso |
| RN-006 Bloqueio por tentativas | `AutenticarUsuarioUseCase`: 5 tentativas, bloqueio de 15 minutos |
| RN-007 Unicidade de credenciais | Índice único em `Usuarios.Email` e verificação no cadastro |
| RN-008 Visibilidade por perfil | Indicadores e relatórios filtram por veterinário quando o perfil é Veterinário |
| RN-009 Antecedência de cancelamento | `AtualizarStatusSessaoUseCase`, usando o prazo configurado na clínica |
| RN-010 Campos obrigatórios da avaliação | `RegistrarAvaliacaoUseCase` aponta exatamente quais campos faltam |
| RN-011 Restrição de mídias e documentos | Formato e tamanho validados antes do armazenamento |

### Requisitos não funcionais

- **RNF-001 Usabilidade** — sidebar fixa no portal e navegação por abas no aplicativo.
- **RNF-002 Segurança** — JWT com expiração, senhas com PBKDF2 (210 mil iterações e salt
  por usuário), limitação de requisições nos endpoints de autenticação e upload,
  autorização por perfil em todas as rotas e trilha de auditoria.
- **RNF-003 Privacidade clínica** — observações internas isoladas em entidade própria e
  nunca serializadas para o perfil Tutor.
- **RNF-004 Desempenho** — listagens paginadas com teto de 100 itens por página, índices
  nos campos de busca da agenda e do prontuário, consultas sem rastreamento para leitura e
  carregamento de mídias sob demanda.
- **RNF-005 Confiabilidade** — soft delete de pacientes, versionamento de registros
  clínicos, tratamento uniforme de exceções e `GET /health` para monitoramento.
- **RNF-006 Compatibilidade** — portal responsivo de 390px a desktop; aplicativo nativo.
- **RNF-007 Escalabilidade** — todo registro é associado a uma clínica (tenant), abrindo
  caminho para a evolução SaaS/Multi-Tenant prevista no DAS.
- **RNF-008 Consistência visual** — design system compartilhado entre web e mobile.

---

## Estrutura do backend

```
VetCare.API/
├── Controllers/    endpoints REST, autorização por perfil
├── UseCases/       regras de negócio (uma classe por processo do domínio)
├── Data/           DbContext, repositórios e o seed inicial
├── Models/         entidades do domínio
├── DTOs/           contratos de entrada e saída
├── Security/       hash de senha, emissão de JWT, perfis, limites de requisição
├── Services/       arquivos, notificações, auditoria e lembretes automáticos
├── Common/         Resultado, paginação, tratamento de erros e tradução para HTTP
└── Migrations/     histórico do esquema
```

Os casos de uso não conhecem HTTP: devolvem um `Resultado` com o motivo da falha, e o
controller traduz isso no status correto (400, 401, 403, 404, 409 ou 423).

### Banco de dados

O esquema tem **24 tabelas, 41 chaves estrangeiras e 74 índices**, criado por uma única
migração. Nenhum relacionamento do domínio apaga em cascata: excluir um registro clínico
precisa ser uma decisão explícita, o que preserva a integridade do histórico (RN-004).

### Armazenamento de arquivos

Mídias e documentos são gravados em disco sob `wwwroot`, com o banco guardando apenas os
metadados e a URL. A troca pelo armazenamento externo previsto no DAS (Amazon S3 ou
MinIO) exige mudar somente `Services/ArmazenamentoArquivos.cs`.

### Sincronia entre as telas

Um filtro global (`Common/FiltroDeAtualizacoes.cs`) observa toda requisição de escrita que
termina em 2xx e publica o fato no `Services/CentralDeAtualizacoes.cs`. Ficar nesse ponto —
depois da ação, antes de a resposta sair — é o que garante a cobertura: qualquer endpoint que
grave no banco entra na conta, inclusive os que ainda serão escritos, sem que nenhum caso de
uso precise se lembrar disso. Requisições recusadas por validação ou por permissão não
publicam nada, porque não mexeram no banco.

Os clientes consomem `GET /api/atualizacoes?desde=<versão>`, que fica pendurado no servidor
até haver novidade ou até 25 segundos (long polling). Na prática o efeito é o de uma conexão
em tempo real, sem acrescentar nenhuma dependência aos dois frontends nem exigir build nativo
no aplicativo. No portal, `contexts/AtualizacoesContext.tsx` mantém uma única conexão e o hook
`useAtualizacao(recursos, recarregar)` liga cada tela aos recursos que lhe interessam; no
aplicativo, `contextos/AtualizacoesContext.tsx` faz o mesmo e suspende o laço quando o app vai
para segundo plano.

Dois cuidados valem menção:

- **Privacidade (RN-003).** Uma clínica tem vários tutores. O evento que chega ao perfil Tutor
  vai sem descrição e sem autor: ele só fica sabendo que *algo* mudou e recarrega os próprios
  dados, que a API já filtra. A equipe clínica recebe o evento completo.
- **Cliente atrasado.** O histórico em memória é curto e proposital — ele não substitui o
  banco, apenas avisa que o banco mudou. Quem ficou fora tempo demais recebe `reiniciar: true`
  e recarrega a tela inteira, em vez de aplicar um retrato incompleto.

O estado vive na memória do processo. Numa eventual operação com mais de uma instância da API,
essa peça passa a precisar de um intermediário compartilhado (Redis ou SignalR com backplane);
o contrato HTTP visto pelos frontends continua o mesmo.

### Processos em segundo plano

- `LembreteConfirmacaoService` — a cada 30 minutos, notifica os tutores cujas sessões nas
  próximas 48 horas seguem sem confirmação (HU-015, CA-2).
- `LembreteVacinacaoService` — avisa sobre doses próximas do vencimento.

---

## Testes

```bash
dotnet test VetCare.Tests/VetCare.Tests.csproj   # 91 testes unitários
```

Os roteiros ponta a ponta ficam em [`testes/`](testes/README.md) e exercitam as regras de
negócio pela API, os endpoints do aplicativo, as funcionalidades clínicas e a interface
web em um navegador real — **152 a 154 verificações**, conforme o ambiente.

```bash
bash testes/criar-dados-demonstracao.sh     # cenário de avaliação
bash testes/teste-regras-negocio.sh         # 46
bash testes/teste-api-mobile.sh             # 26
bash testes/teste-funcionalidades-novas.sh  # 33 a 35
bash testes/teste-tempo-real.sh            # 27
node testes/teste-navegador.mjs ./capturas  # 20
```

---

## Pontos de atenção para produção

O sistema está funcional, mas alguns itens dependem de decisões de infraestrutura:

1. **Segredos** — `Jwt:Chave` e a senha do banco estão em `appsettings.json` para
   facilitar a execução local. Em produção, use variáveis de ambiente (`Jwt__Chave`,
   `ConnectionStrings__DefaultConnection`) ou um cofre de segredos. O `docker-compose.yml`
   já lê ambos do `.env`.
2. **HTTPS** — a pilha do compose serve HTTP; coloque um proxy reverso com certificado à
   frente antes de expor à internet.
3. **Armazenamento de mídias** — trocar o disco local por S3/MinIO antes de escalar.
4. **Tempo real** — o chat e as notificações usam consulta periódica. O DAS prevê
   WebSocket; a troca afeta apenas a camada de transporte, não as regras já implementadas.
5. **E-mail** — a recuperação de senha gera um token de uso único, mas o envio por SMTP
   ainda não está conectado: fora de produção o token é devolvido na própria resposta, e
   em produção é preciso ligar o serviço de e-mail.
6. **Backup** — o DAS exige backup diário com zero perda de registros confirmados; isso é
   configuração do servidor PostgreSQL.
