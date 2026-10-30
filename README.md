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
| Adminer (interface do banco) | http://localhost:8082 (porta em `ADMINER_PORT`) |

A pilha sobe em `Production`, onde a documentação interativa fica desligada. Para
consultá-la sem mudar o ambiente, defina `API_DOCS=true` no `.env`. Trocar para
`ASPNETCORE_ENVIRONMENT=Development` também funciona, mas abre o CORS para qualquer origem e
faz a API devolver o código de recuperação de senha na própria resposta — serve só para
desenvolvimento.

O banco é criado e migrado automaticamente na primeira subida. Os volumes
`dados-postgres`, `arquivos-api` e `emails-api` preservam dados, mídias e e-mails gravados
localmente entre atualizações da imagem.

#### E-mail e notificações no celular

Os dois canais funcionam sem configuração nenhuma, em modo de demonstração:

- **E-mail** — sem `SMTP_HOST` no `.env`, cada mensagem (recuperação de senha, boas-vindas,
  avisos do tratamento) é gravada como um arquivo `.html` na pasta `emails-enviados` da API.
  Para enviar de verdade, preencha `SMTP_HOST`, `SMTP_USUARIO` e `SMTP_SENHA` (uma conta
  comum do Gmail com "senha de app" basta; o remetente segue a conta do SMTP quando
  `EMAIL_REMETENTE` fica em branco). A API testa a conexão e a autenticação na subida e
  escreve o resultado no log, em português. O `.env.example` traz os valores dos
  provedores mais comuns.
- **Push no celular** — a API usa o serviço de push da Expo e não exige credencial. O
  aplicativo precisa do identificador do projeto Expo em `extra.eas.projectId` (gerado por
  `npx eas init`) e de um build de desenvolvimento ou de loja: o Expo Go, no Android, não
  recebe notificações remotas. A limitação é só do canal push; o mural de avisos dentro do
  aplicativo e o e-mail funcionam também no Expo Go.

### Opção B — Execução local

**1. Banco de dados.** É necessário um PostgreSQL acessível. A string de conexão fica em
`VetCare.API/appsettings.json`:

```json
"DefaultConnection": "Host=localhost;Port=5432;Database=vetcare_db;Username=postgres;Password=admin"
```

Não é preciso criar tabelas manualmente: a API aplica as migrações pendentes ao iniciar.

> **Atualizando um banco que já tem dados?** A migração do fluxo clínico
> (`FluxoClinicoAvaliacaoTratamentoEvolucao`) converte os registros existentes ao iniciar a
> API e não tem volta automática. Faça um backup do banco antes de subir a versão nova (veja
> [Banco de dados](#banco-de-dados)).

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
host do Metro, então normalmente não é preciso configurar IP. Se a API estiver em outra
porta, defina `EXPO_PUBLIC_API_PORT`; para apontar para outro servidor, `EXPO_PUBLIC_API_URL`.

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
- Cadastro da equipe (Administrador, Veterinário e Apoio) na tela de Usuários, com
  ativação e desativação. Tutores têm tela própria, que cria também o acesso deles.
- **Validação dos cadastros**, a mesma no portal, no aplicativo e na API, no cadastro e na
  edição: nome completo só com letras; e-mail com domínio válido; senha de 8 caracteres com
  letras e números; CRMV com UF válida, gravado como "CRMV-DF 12345"; telefone do tutor
  obrigatório e com DDD; CPF opcional com dígitos verificadores, sem repetir entre tutores;
  endereço, especialidade e setor com tamanho coerente. Cada erro aparece no próprio campo,
  e telefone e CPF têm máscara.
- Edição por perfil: a administração edita nome, e-mail e dados profissionais da equipe; a
  equipe clínica edita todo o cadastro do tutor; o próprio tutor mantém telefone e endereço
  em dia, e só a clínica troca o nome e o e-mail dele.
- Dados da clínica, duração padrão das sessões e prazo mínimo de cancelamento.
- Trilha de auditoria focada no que mudou nos dados: quem criou, alterou, inativou ou
  excluiu cada paciente, tutor, usuário e registro clínico, com filtro por ação, registro,
  usuário e período. Logins, consultas e downloads continuam gravados no banco, mas não
  aparecem na tela.

### Pacientes
- **A clínica atende apenas cães**: o cadastro, no portal e no aplicativo do tutor, não tem
  escolha de espécie, e a API recusa outra. Pacientes antigos de outra espécie continuam
  editáveis, sem trocar de espécie.
- Ficha completa: raça (com sugestões de raças de cães), pelagem, microchip, castração, peso
  e idade descritiva.
- Alertas clínicos (alergias, condições crônicas, cirurgias) exibidos em destaque no
  prontuário e visíveis também ao tutor.
- Carteira de vacinação com fabricante, lote, veterinário aplicador e classificação
  automática da dose em **em dia**, **a vencer**, **vencida**, **dose única** ou **concluída**
  (quando a dose seguinte do mesmo produto já foi aplicada).
- **Esquemas com várias doses** (V8, V10, giárdia): cada aplicação registra "dose N de M", e
  a API confere a sequência — a dose 2 só entra depois da dose 1, com o mesmo total.
- **Recorrência** mensal, trimestral, semestral ou anual: sem a data da próxima dose, o
  sistema a calcula pela recorrência; um esquema em aberto não fica sem a próxima dose.
- Carteira com **busca, filtro por tipo e por situação e paginação**; a exclusão de um
  registro exige **justificativa**, que fica na auditoria.
- **Vacinação em atraso** entra nos alertas clínicos do prontuário e nas listas de pacientes,
  ao lado das alergias e comorbidades; o painel inicial lista as doses vencidas e a vencer
  nos próximos 30 dias.
- Registro de óbito, que inativa o paciente e os tratamentos em aberto.
- **Cadastro feito pelo próprio tutor**, na web e no aplicativo, para o animal recém-adquirido
  que a clínica ainda não conhece — sem precisar ir até o balcão só para isso. O vínculo com o
  responsável sai do token de quem está autenticado, nunca do corpo da requisição (RN-001), e a
  origem do cadastro fica registrada na auditoria.

### Agenda
- Agenda individual do veterinário (Dia/Semana/Mês) e agenda geral da clínica.
- Bloqueio de períodos por veterinário (férias, congressos), respeitado no agendamento.
- Confirmação e cancelamento pelo tutor, pela web ou pelo aplicativo.
- Lembretes automáticos de confirmação e de vacinas a vencer, em segundo plano.

### Pacientes e tutores
- **Lista de pacientes enxuta**: cada linha só tem **visualizar** (abre o prontuário) e
  **inativar/reativar**. Paciente não é excluído: a inativação preserva o prontuário. Editar o
  cadastro e vacinar ficam dentro do prontuário do animal.
- **Filtro por raça** na lista de pacientes, além da busca, do veterinário e da situação.
- **Tutores em lista paginada**, como os pacientes, mostrando os **animais vinculados** a cada
  tutor; cada animal leva ao próprio prontuário.
- Cada paciente tem um tutor (RN-001) e um **veterinário responsável**, escolhido no
  cadastro e trocado na edição do cadastro. O novo responsável e o tutor são avisados.
- **Qualquer veterinário atende qualquer paciente**: toda a equipe clínica, inclusive todos os
  veterinários, vê e acessa todos os pacientes da clínica, com prontuário, tutores, vacinas e
  registros. Se a veterinária que acompanha a Pipoca faltar, um colega abre o prontuário,
  agenda a sessão na própria agenda e registra a sessão em nome próprio, sem transferência.
  O responsável organiza a clínica (painel do veterinário e filtro "Meus pacientes"), mas não
  restringe o acesso. A antiga transferência de paciente deixou de existir.
- Os pacientes cadastrados pelo tutor no aplicativo chegam sem responsável. O prontuário avisa,
  e a clínica define o veterinário em "Editar cadastro" (ou na primeira avaliação).
- **Carteira de vacinação**: qualquer veterinário e a administração corrigem e apagam
  aplicações lançadas por engano; cada correção e exclusão fica na auditoria.
- **Confirmação antes de agir**: toda exclusão, cancelamento, inativação e edição do portal
  passa por uma janela de confirmação própria (`ConfirmacaoProvider`), que também pede o
  motivo quando a ação exige um, como o cancelamento de receita.
- **Avisos de retorno no canto inferior direito**: "salvo", "removido" e as falhas de uma ação
  aparecem sobre a página, no canto inferior direito; os de sucesso somem sozinhos.
- Tudo o que é clicável (botões, abas, links, seletores) mostra a mão ao passar o mouse.

### Prontuário e fluxo clínico
O fluxo segue **animal → avaliações → tratamentos originados das avaliações → sessões
registradas em cada tratamento**. No requisito HU-008 o registro de uma sessão se chama
evolução; na interface tudo se chama sessão, para não haver dois nomes para a mesma coisa.

- **Avaliação**: motivo da consulta, observações, possível diagnóstico e veterinário
  responsável (RN-010). Pertence ao animal e pode originar nenhum, um ou vários tratamentos,
  receitas e pedidos de exame.
- **Tratamento**: só existe a partir de uma avaliação. Tem nome, tipo, descrição, duração
  prevista, frequência, sessões previstas e veterinário, com a situação **Planejado**, **Em
  andamento**, **Concluído** ou **Inativado**. Pode ser editado; inativar não apaga as sessões
  registradas e permite reativar; concluído é definitivo. A primeira sessão registrada põe um
  tratamento planejado em andamento.
- **Sessão**: o registro de cada sessão realizada, ligado ao tratamento, com data, número
  (atribuído pelo sistema), procedimentos realizados, o que foi feito, reação do animal,
  observações, relatório, sinais vitais, escala de dor e fotos e vídeos, que podem ser
  escolhidos no próprio formulário, antes de salvar. Pode originar uma receita. É registrada
  no botão **Registrar sessão** do tratamento, da aba Sessões do prontuário ou do menu
  Sessões, escolhendo uma sessão agendada ainda não registrada ou a sessão de agora, sem
  agendamento, que entra na agenda já concluída; na agenda, cada sessão também tem
  **Registrar sessão**.
- **Sessões**, no menu (Administrador e Veterinário) e como aba do prontuário: filtros por
  animal, tratamento, período, veterinário e número da sessão. Dentro de um tratamento
  aparecem só as sessões dele, em ordem cronológica e com filtro, e o histórico completo do
  animal fica a um clique.
- **Linha do tempo**: avaliações, início e encerramento (conclusão ou inativação) dos
  tratamentos, sessões, cirurgias e procedimentos, vacinações, receitas, pedidos de exame e
  observações internas. Cada evento mostra em destaque a data em que foi feito, com
  categoria, descrição resumida e responsável; o período (de/até) e as categorias filtram a
  lista, e selecionar um evento abre o registro completo.
- **Medicação**: as receitas vêm de uma avaliação ou de uma sessão (a origem é
  obrigatória), podem ser impressas e são canceladas com motivo, nunca apagadas.
- **Pedidos de exame** vêm de uma avaliação e podem ser impressos. Quem assinou (e a
  administração) edita o pedido, com a versão anterior guardada, ou o exclui: depois da
  confirmação, a exclusão tira o pedido do prontuário e fica registrada na auditoria.
- **Alertas clínicos** no topo do prontuário, sem título nem moldura: cada um é só o nome e a
  descrição, na cor da gravidade.
- Mídias, documentos e observações internas restritas à equipe clínica.
- Impressão de receita, de pedido de exame e de prontuário, e exportação da trilha de
  auditoria em CSV.

### Comunicação
- Mensagens entre tutor e clínica, com histórico e contagem de não lidas. Enter envia, e o
  campo continua pronto para a próxima mensagem.
  A conversa rola dentro do próprio quadro: a página não cresce conforme chegam mensagens.
- Notificações no portal e no aplicativo, **entregues também por e-mail e por push no celular
  do tutor**. A entrega sai de uma fila no banco com novas tentativas em caso de falha, e cada
  usuário escolhe os canais que quer no próprio perfil.

### Atualização em tempo real
Toda gravação no banco é anunciada às telas que já estão abertas, em qualquer perfil e em
qualquer das duas plataformas. Quando o tutor confirma a presença pelo celular, a agenda do
veterinário, a agenda geral do apoio e o painel do administrativo mudam em menos de um
segundo — e o mesmo vale para cancelamentos, novos pacientes, registros no prontuário e
mensagens. Detalhes do funcionamento em [Sincronia entre as telas](#sincronia-entre-as-telas).

### Acesso
- Autenticação JWT com emissor e público validados, bloqueio após tentativas seguidas e
  limitação de requisições; contas desativadas perdem o acesso em até um minuto, mesmo com
  o token ainda válido.
- **Esqueci minha senha**: o usuário informa o e-mail cadastrado e recebe só um código de 6
  dígitos, válido por 30 minutos, de uso único e com no máximo 5 tentativas, que ele digita no
  portal ou no aplicativo. O e-mail não traz botão nem link para a troca de senha: o código é
  o único caminho. A resposta é a mesma para e-mails desconhecidos.
- **Primeiro acesso**: contas criadas pela clínica sem senha recebem por e-mail um código para
  defini-la (válido por 72 horas), usado em "Esqueci minha senha → Já tenho um código". Toda
  troca de senha gera um aviso de segurança.
- Mídias e documentos clínicos são servidos apenas por URLs assinadas com prazo de validade,
  nunca como arquivos estáticos públicos.

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
| HU-008 | Registrar a evolução da sessão (atendimento fisioterapêutico) | Web |
| HU-009 | Registrar observações internas | Web |
| HU-010 | Anexar mídias à sessão | Web |
| HU-011 | Consultar prontuário | Web, Mobile |
| HU-012 | Gerenciar documentos clínicos | Web |
| HU-013 | Acompanhar tratamento | Web, Mobile (Tutor) |
| HU-014 | Trocar mensagens | Web, Mobile |
| HU-015 | Receber notificações | Web, Mobile |
| HU-016 | Consultar indicadores do dia | Web |

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
| RN-008 Visibilidade por perfil | O tutor vê só os próprios pets; a equipe clínica, inclusive qualquer veterinário, vê todos os pacientes e tutores da clínica (`AcessoAoPaciente`, `AcessoAoTutor`). Cada paciente tem um **veterinário responsável** (`Pets.VeterinarioResponsavelId`), trocado em `PUT /api/pets/{id}`: ele define o recorte do painel do veterinário (indicadores e prevenção), não o acesso. Agenda e assinatura continuam pessoais: o veterinário agenda na própria agenda e registra avaliações, sessões e receitas em nome próprio |
| RN-009 Antecedência de cancelamento | `AtualizarStatusSessaoUseCase`, usando o prazo configurado na clínica |
| RN-010 Campos obrigatórios da avaliação | `RegistrarAvaliacaoUseCase` aponta exatamente quais campos faltam entre motivo da consulta, observações e possível diagnóstico |
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
├── Services/       arquivos, e-mail (SMTP ou caixa de saída local), push (Expo),
│                   entrega de notificações, auditoria e lembretes automáticos
├── Common/         Resultado, paginação, tratamento de erros e tradução para HTTP
└── Migrations/     histórico do esquema
```

Os casos de uso não conhecem HTTP: devolvem um `Resultado` com o motivo da falha, e o
controller traduz isso no status correto (400, 401, 403, 404, 409 ou 423).

### Banco de dados

O esquema tem **26 tabelas, 50 chaves estrangeiras e 88 índices** (contando as chaves
primárias), criado por cinco migrações: a inicial; os canais de entrega das notificações, os
aparelhos registrados para push e o código de recuperação de senha; o veterinário responsável
por paciente; o esquema de doses e a recorrência da carteira de vacinação; e o fluxo clínico
(avaliação do animal → tratamento → evolução, com os pedidos de exame). Nenhum relacionamento
do domínio apaga em cascata: excluir um registro clínico precisa ser uma decisão explícita, o
que preserva a integridade do histórico (RN-004).

A migração do fluxo clínico (`FluxoClinicoAvaliacaoTratamentoEvolucao`) converte os dados de
um banco que já estava em uso, sem perder registros:

- cada avaliação passa a pertencer ao animal; a queixa principal vira o motivo da consulta, a
  hipótese diagnóstica vira o possível diagnóstico, e anamnese, exame físico e plano
  terapêutico são reunidos no campo "Observações";
- cada tratamento é ligado à primeira avaliação registrada nele. O que não tinha nenhuma
  recebe uma avaliação de origem criada na conversão, identificada como tal. O objetivo
  terapêutico vira o nome e a descrição, as sessões previstas partem das sessões agendadas, e
  "Interrompido" passa a "Inativado";
- as evoluções (atendimentos) recebem o número da sessão em ordem cronológica, e a reação do
  animal das antigas fica como "Não registrada.";
- as receitas antigas continuam sem origem; as novas exigem uma.

O caminho de volta (`Down`) não existe: para desfazer a conversão, restaure o backup feito
antes de subir a versão nova.

### Armazenamento de arquivos

Mídias e documentos são gravados em disco na pasta `arquivos/` da API (configurável em
`Arquivos:Pasta`), com o banco guardando apenas os metadados e o caminho. A API nunca os
expõe como arquivos estáticos: cada resposta traz uma URL assinada (`/api/arquivos/...`)
com validade de quatro horas, verificada por `Security/AssinadorDeArquivos.cs`. A troca
pelo armazenamento externo previsto no DAS (Amazon S3 ou MinIO) exige mudar somente
`Services/ArmazenamentoArquivos.cs`.

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
- `EntregadorDeNotificacoes` — lê as notificações pendentes de entrega e as envia por
  e-mail e por push conforme as preferências do destinatário, com até cinco tentativas e
  intervalos crescentes; tokens de push rejeitados pela Expo são desativados.
- `ProcessadorDeEmails` — fila em memória que envia os e-mails transacionais (recuperação
  de senha, boas-vindas, senha provisória) sem segurar a resposta HTTP.

Todos os horários "do dia" (expediente, indicadores, intervalos da agenda) seguem o fuso da
clínica (`Aplicacao:FusoHorario`, padrão `America/Sao_Paulo`), e não o do servidor.

---

## Testes

```bash
dotnet test VetCare.Tests/VetCare.Tests.csproj   # 264 testes unitários; o critério é "Falhou: 0"
```

Os roteiros ponta a ponta ficam em [`testes/`](testes/README.md) e exercitam as regras de
negócio pela API, os endpoints do aplicativo, as funcionalidades clínicas e a interface
web em um navegador real. A quantidade de verificações de cada roteiro está em
[`testes/README.md`](testes/README.md), que é a referência; o critério de aprovação é
"FALHAS: 0".

```bash
bash testes/criar-dados-demonstracao.sh     # cenário de avaliação
bash testes/teste-regras-negocio.sh
bash testes/teste-api-mobile.sh
bash testes/teste-funcionalidades-novas.sh
bash testes/teste-tempo-real.sh
node testes/teste-navegador.mjs ./capturas
```

---

## Pontos de atenção para produção

O sistema está funcional, mas alguns itens dependem de decisões de infraestrutura:

1. **Segredos** — `Jwt:Chave` e a senha do banco estão em `appsettings.json` para
   facilitar a execução local. Em produção, use variáveis de ambiente (`Jwt__Chave`,
   `ConnectionStrings__DefaultConnection`) ou um cofre de segredos. O `docker-compose.yml`
   já lê ambos do `.env`.
2. **HTTPS** — a pilha do compose serve HTTP; coloque um proxy reverso com certificado à
   frente antes de expor à internet. Se a própria API escutar em HTTPS, ligue o
   redirecionamento com `Seguranca__RedirecionarParaHttps=true`.
3. **Armazenamento de mídias** — trocar o disco local por S3/MinIO antes de escalar.
4. **Tempo real** — as telas se atualizam por long polling em `GET /api/atualizacoes`
   (veja "Sincronia entre as telas"). O DAS prevê WebSocket; a troca afeta apenas a camada
   de transporte, não as regras já implementadas.
5. **E-mail e push** — sem `SMTP_HOST` os e-mails ficam em `emails-enviados` e, em
   `Development`, o código de recuperação volta na própria resposta da API para
   permitir a demonstração. Em produção configure o SMTP e gere o `projectId` do Expo
   para o aplicativo (veja [E-mail e notificações no celular](#e-mail-e-notificações-no-celular)).
6. **Backup** — o DAS exige backup diário com zero perda de registros confirmados; isso é
   configuração do servidor PostgreSQL.
