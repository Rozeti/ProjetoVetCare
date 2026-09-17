# Como subir e testar o VetCare

Guia passo a passo, sem pressupor conhecimento prévio. Ao final você terá o sistema no ar,
saberá onde os dados ficam guardados e como conferir, com os próprios olhos, que tudo o que
você digita está sendo gravado no banco.

---

## Parte 0 — Antes de começar

Se você está pegando o projeto pela primeira vez (clonou do GitHub, por exemplo), comece aqui.

### O que instalar

Para **apenas rodar o sistema**, só duas coisas são necessárias:

| Programa | Para quê | Onde baixar |
|---|---|---|
| **Docker Desktop** | Sobe o banco, a API e o portal | https://www.docker.com/products/docker-desktop/ |
| **Git** | Baixa o projeto e envia suas alterações | https://git-scm.com/downloads |

Para **mexer no código** ou **usar o aplicativo do celular**, some a estes:

| Programa | Para quê | Onde baixar |
|---|---|---|
| **Node.js 20 ou superior** | Portal web e aplicativo do tutor | https://nodejs.org (escolha a versão LTS) |
| **.NET SDK 10** | API | https://dotnet.microsoft.com/download |

Instale com as opções padrão, clicando em "Avançar". Depois **reinicie o computador** — o
Docker precisa disso para funcionar direito no Windows.

Para conferir se deu certo, abra o terminal (tecla Windows, digite `cmd`, Enter) e rode:

```
git --version
docker --version
```

Cada um deve responder com um número de versão. Se disser "não é reconhecido como um comando",
a instalação não terminou ou o computador não foi reiniciado.

### Baixar o projeto

Escolha uma pasta onde o projeto vai ficar — por exemplo a Área de Trabalho. No terminal:

```
cd %USERPROFILE%\Desktop
git clone <endereço-do-repositorio>
cd ProjetoVetCare
```

O `<endereço-do-repositorio>` é o link que aparece no botão verde **Code** da página do
projeto no GitHub. Se o repositório for privado, o Git vai pedir seu usuário e senha do
GitHub na primeira vez — veja a observação no fim desta parte.

> **Não baixe como .zip pelo botão "Download ZIP".** Sem o Git, você não consegue receber as
> atualizações dos colegas nem enviar as suas.

### Primeiro acesso a um repositório privado

Ao clonar um repositório privado, o Git abre uma janela do navegador pedindo para você entrar
na sua conta do GitHub. Autorize e pronto: ele guarda o acesso e não pergunta de novo.

Se em vez da janela aparecer um pedido de usuário e senha no terminal, saiba que a senha da
conta **não funciona** — o GitHub exige um "token". Nesse caso, o mais simples é instalar o
[GitHub Desktop](https://desktop.github.com/), entrar com sua conta uma vez e deixar que ele
configure o acesso; depois os comandos do Git passam a funcionar normalmente.

---

## Parte 1 — O que é cada peça

O VetCare tem quatro peças. Pense numa clínica de verdade:

| Peça | O que faz | Comparação |
|---|---|---|
| **Banco de dados** (PostgreSQL) | Guarda tudo: pacientes, sessões, prontuários | O arquivo de fichas da clínica |
| **API** (`VetCare.API`) | Aplica as regras e conversa com o banco | A recepção, que sabe as regras e busca as fichas |
| **Portal web** (`VetCare.Web`) | A tela que a equipe usa no computador | O balcão de atendimento |
| **Aplicativo** (`VetCare.Mobile`) | A tela que o tutor usa no celular | O aplicativo do cliente |

O portal **nunca** fala direto com o banco. Ele pede à API, e a API decide o que pode ou não.
É por isso que um tutor não consegue ver a observação interna do veterinário nem trocando o
endereço no navegador: a API não entrega esse dado para o perfil dele.

As telas também não ficam paradas esperando alguém apertar F5. Cada uma mantém uma linha aberta
com a API, que avisa na hora em que algo é gravado no banco. Quando o tutor confirma a presença
pelo celular, a agenda do veterinário, a agenda geral do apoio e o painel do administrativo
mudam sozinhos em menos de um segundo — e o mesmo vale para cancelamentos, pets novos e
mensagens. O Teste 3, mais adiante, mostra isso funcionando com duas janelas lado a lado.

---

## Parte 2 — Onde fica o banco de dados

**Sim, é um PostgreSQL rodando dentro do Docker.** Não existe nenhum arquivo de banco na pasta
do projeto — ele vive dentro de um contêiner.

Um **contêiner** é como um computador pequeno e isolado rodando dentro do seu. O contêiner do
banco tem o PostgreSQL instalado e nada mais. Ele guarda os dados num **volume**, que é um
espaço em disco gerenciado pelo Docker, separado do contêiner. Essa separação existe por um
motivo prático: você pode apagar e recriar o contêiner que os dados continuam lá, porque eles
estão no volume, não no contêiner.

Para o banco existir, o contêiner precisa estar ligado. Se o Docker Desktop estiver fechado ou
o contêiner parado, a API não consegue salvar nada e mostra erro de conexão.

### As tabelas

O banco se chama `vetcare_db` e tem **24 tabelas**. As principais:

| Tabela | Guarda |
|---|---|
| `Clinicas` | A clínica (horário de funcionamento, prazo de cancelamento) |
| `Usuarios` | Todas as contas de acesso, com a senha criptografada |
| `Veterinarios`, `Tutores`, `ApoiosAdministrativos` | Os dados específicos de cada perfil |
| `Pets` | Os pacientes |
| `Tratamentos`, `Sessoes`, `Atendimentos`, `AvaliacoesClinicas` | O andamento clínico |
| `Prontuarios`, `ObservacoesInternas`, `VersoesRegistrosClinicos` | O histórico e as versões antigas |
| `Vacinas`, `Prescricoes`, `ItensPrescricao`, `AlergiasCondicoes` | Carteira, receitas e alertas |
| `Mensagens`, `Notificacoes` | A comunicação |
| `RegistrosAuditoria` | Quem fez o quê e quando |

Você **não precisa criar nenhuma tabela**. Quando a API inicia e encontra um banco vazio, ela
cria as 24 tabelas sozinha e cadastra a clínica mais uma conta de administrador. É só isso que
existe hoje — o resto você cadastra pelo sistema.

---

## Parte 3 — Subir tudo (jeito recomendado)

Este é o caminho mais simples: um único comando sobe o banco, a API e o portal web juntos.

### Passo 1 — Abra o Docker Desktop

Procure "Docker Desktop" no menu Iniciar e abra. Espere até o ícone da baleia, no canto
inferior direito, parar de se mexer. Se ele ficar "Starting..." por muito tempo, reinicie o
computador e tente de novo.

### Passo 2 — Abra o terminal na pasta do projeto

Abra a pasta `ProjetoVetCare` no Explorador de Arquivos, clique na barra de endereço no topo,
apague o que estiver escrito, digite `cmd` e aperte Enter. Vai abrir uma janela preta já
posicionada na pasta certa.

### Passo 3 — Libere as portas

O Docker vai usar as portas 5432 (banco), 5265 (API) e 8080 (portal). Se outra coisa já
estiver usando alguma delas, o sistema sobe pela metade e você recebe erro no login.

**3a. Desligue qualquer PostgreSQL avulso.** Se você já rodou um banco em contêiner nesta
máquina, ele pode estar ocupando a porta 5432. Veja o que está ligado:

```
docker ps
```

Se aparecer algum contêiner de PostgreSQL fora da lista do projeto, desligue-o pelo nome —
por exemplo:

```
docker stop vetcare-postgres
```

> "No such container" significa que ele não existe nesta máquina e a porta já está livre.

**3b. Feche qualquer API ou portal rodando fora do Docker.** Se em algum momento você seguiu a
Parte 4 deste guia, pode haver um `dotnet run` ou um `npm run dev` ainda aberto em outra janela
de terminal. Feche essas janelas, ou aperte `Ctrl + C` dentro delas.

Para conferir se a porta da API está livre:

```
netstat -ano | findstr :5265
```

Se **não aparecer nada**, está livre — seguir em frente. Se aparecer alguma linha, é porque
ainda tem algo rodando ali; volte e feche.

> **Por que isso importa:** se uma API antiga continuar na porta 5265, o Windows entrega o
> endereço `localhost:5265` para ela, e não para a do Docker. Aí o portal abre normalmente, mas
> o login falha com **"Ocorreu um erro inesperado"** — porque essa API antiga está procurando um
> banco que não existe mais, ou com uma senha diferente da que você colocou no `.env`.

### Passo 4 — Crie o arquivo de senhas

```
copy .env.example .env
```

Isso cria um arquivo novo chamado `.env`. **Abra o `.env`** — e não o `.env.example` — no
Bloco de Notas e troque duas linhas:

- `POSTGRES_PASSWORD=` — coloque uma senha sua para o banco
- `JWT_CHAVE=` — coloque uma frase longa, **com pelo menos 32 caracteres**

Salve e feche. O `.env` nunca vai para o Git: ele guarda as senhas desta máquina. Já o
`.env.example` é o modelo que vai para o repositório — por isso ele não pode ter senha de
verdade dentro.

> **Não use o caractere `$` nas senhas.** O Docker entende o cifrão como início de uma
> variável e apaga o trecho seguinte: uma chave `7xX$mK9!bP2` chega à aplicação como `7xX!bP2`,
> fica curta demais e a API não sobe, reiniciando em laço. Se você vir a API em
> `Restarting` no `docker compose ps`, é quase sempre isso. Prefira letras, números e hífens.

### Passo 5 — Suba tudo

```
docker compose up -d
```

Na primeira vez isso demora de 3 a 10 minutos: o Docker está baixando o PostgreSQL e
construindo a API e o portal. Nas próximas vezes leva segundos.

O `-d` significa "deixe rodando em segundo plano". Você pode fechar o terminal que continua no ar.

### Passo 6 — Confira se subiu

```
docker compose ps
```

Você deve ver três linhas com `(healthy)`:

```
NAME                 SERVICE    STATUS
vetcare-api-1        api        Up (healthy)
vetcare-postgres-1   postgres   Up (healthy)
vetcare-web-1        web        Up (healthy)
```

`healthy` quer dizer que o próprio sistema se testou e está respondendo. Se algum ficar
`starting` por mais de um minuto, veja o que houve com:

```
docker compose logs api
```

### Passo 7 — Entre no sistema

Abra o navegador em **http://localhost:8080**

| Campo | Valor |
|---|---|
| E-mail | `admin@vetcare.com` |
| Senha | `vetcare123` |

**Troque essa senha agora.** Clique no seu nome, no rodapé da barra lateral esquerda, e depois
em "Alterar senha". Essa senha inicial existe só para destravar o primeiro acesso.

### Para desligar tudo

```
docker compose stop
```

Os dados continuam salvos. Para ligar de novo, `docker compose start`.

> ⚠️ **Nunca use `docker compose down -v`.** O `-v` apaga os volumes, ou seja, **apaga o banco
> inteiro**. O `docker compose down` sozinho (sem `-v`) é seguro: remove os contêineres mas
> preserva os dados.

---

## Parte 4 — Subir para desenvolver (jeito alternativo)

Use este caminho se você vai **mexer no código**, porque aqui as alterações aparecem na hora,
sem precisar reconstruir imagem.

Você vai precisar de três janelas de terminal abertas ao mesmo tempo.

**Janela 1 — o banco:**

```
docker start vetcare-postgres
```

**Janela 2 — a API:**

```
cd VetCare.API
dotnet run
```

Deixe rodando. Vai aparecer `Now listening on: http://localhost:5265`.

**Janela 3 — o portal web:**

```
cd VetCare.Web
npm install
npm run dev
```

O `npm install` só é necessário na primeira vez. Depois aparece o endereço
**http://localhost:5173** — é lá que você acessa.

O aplicativo do tutor tem uma parte só para ele: veja a **Parte 5**.

---

## Parte 5 — Acessar pelo celular

O tutor acompanha o tratamento pelo aplicativo. Ele não roda no Docker: fica no seu
computador e o celular se conecta a ele pela rede. Vale tanto se você subiu pelo Docker
(Parte 3) quanto no modo desenvolvimento (Parte 4) — em ambos a API fica disponível para a
rede local.

### O que você precisa

- O celular e o computador **na mesma rede Wi-Fi**. Não funciona com o celular no 4G/5G.
- O aplicativo **Expo Go**, gratuito, instalado no celular (Play Store ou App Store).
- O sistema no ar, com `docker compose ps` mostrando tudo `healthy`.

### Passo 1 — Inicie o aplicativo

Numa janela de terminal nova, na pasta do projeto:

```
cd VetCare.Mobile
npm install
npx expo start
```

O `npm install` só é necessário na primeira vez e demora alguns minutos. Depois aparece um
**QR Code** no terminal, junto de um endereço no formato `exp://192.168.x.x:8081`.

Deixe essa janela aberta: enquanto o aplicativo estiver em uso, ela precisa continuar rodando.

### Passo 2 — Abra no celular

- **Android:** abra o Expo Go e toque em "Scan QR code".
- **iPhone:** abra a câmera normal e aponte para o QR Code; aparece um aviso para abrir no
  Expo Go.

Na primeira vez o aplicativo demora de 30 segundos a 2 minutos para carregar, porque o
computador está montando o pacote e enviando para o celular. Depois disso abre rápido.

### Passo 3 — Entre como tutor

Use o e-mail e a senha de um usuário com **perfil Tutor** — o mesmo que você criou no portal.
O aplicativo é só para tutores: administrador e veterinário usam o portal web.

Ele mostra os pets do tutor, a agenda com confirmação e cancelamento de sessão, o prontuário
(sem as observações internas), a carteira de vacinação, as receitas, as mensagens e os avisos.

### Como o aplicativo acha a API sozinho

Você não precisa configurar IP nenhum. Quando o celular lê o QR Code, ele descobre o endereço
do seu computador na rede — por exemplo `192.168.101.6` — e o aplicativo usa esse mesmo
endereço com a porta da API: `http://192.168.101.6:5265`.

É por isso que os dois precisam estar na mesma Wi-Fi: `localhost`, no celular, é o próprio
celular, e não o seu computador.

### Se o aplicativo abrir mas não conseguir entrar

O sintoma é a mensagem "Não foi possível falar com o servidor" na tela de login, ou uma espera
longa que termina em erro. Tente nesta ordem:

**1. Descubra o IP do seu computador.** Num terminal:

```
ipconfig
```

Procure o bloco do seu adaptador de rede (Wi-Fi ou Ethernet) e anote o **Endereço IPv4** —
algo como `192.168.101.6`. Ignore endereços que comecem com `172.` seguidos de "WSL" ou
"vEthernet": são adaptadores virtuais e o celular não os enxerga.

**2. Confirme que a API responde nesse IP.** Abra no navegador **do próprio computador**,
trocando pelo seu IP:

```
http://192.168.101.6:5265/health
```

Se responder `Healthy`, a API está acessível pela rede. Se não responder, o Firewall do Windows
está bloqueando: ao subir o Docker pela primeira vez aparece uma janela pedindo permissão —
marque **Redes particulares** e permita.

**3. Force o endereço no aplicativo.** Pare o Expo com `Ctrl + C` e reinicie informando o IP
(troque pelo seu):

```
set EXPO_PUBLIC_API_URL=http://192.168.101.6:5265
npx expo start --clear
```

Isso resolve o caso mais comum no Windows, em que o Expo anuncia o IP de um adaptador virtual
em vez do da sua rede.

**4. Se ainda assim não for**, sua rede pode estar isolando os aparelhos entre si — comum em
Wi-Fi de condomínio, de empresa e em redes "de visitantes". Nesse caso use o modo túnel, que
passa por fora da rede local:

```
npx expo start --tunnel
```

Na primeira vez ele pede para instalar um complemento; aceite. Fica mais lento, mas funciona
em qualquer rede.

### E abrir o portal web pelo celular?

Dá, mas exige um ajuste, porque o portal foi montado para atender em `localhost`. Duas coisas
precisam saber o IP do computador: o endereço da API que vai embutido no portal, e a lista de
origens que a API aceita.

Abra o `.env` e acrescente (ou ajuste) estas duas linhas, trocando pelo seu IP:

```
API_URL=http://192.168.101.6:5265
WEB_URL=http://192.168.101.6:8080
```

Depois reconstrua o portal e reinicie a API:

```
docker compose up -d --build web api
```

Aí é só abrir **http://192.168.101.6:8080** no navegador do celular. Seus dados não são
afetados: só os contêineres do portal e da API são trocados, o banco fica intacto.

> Se depois disso o portal parar de abrir em `http://localhost:8080` no computador, é esperado:
> o endereço passou a ser o do IP. Para valer nos dois, escreva os dois na mesma linha, separados
> por vírgula: `WEB_URL=http://localhost:8080,http://192.168.101.6:8080`. O `API_URL`, porém,
> aceita um só — deixe o do IP, que funciona também no computador.

---

## Parte 6 — Testar se está tudo funcionando

### Teste 1 — A API está viva?

Abra no navegador: **http://localhost:5265/health/pronto**

Resposta esperada:

```json
{"status":"Healthy","duracaoMs":12.3,"verificacoes":[{"nome":"banco-de-dados","status":"Healthy","descricao":null}]}
```

`"status":"Healthy"` nas duas posições significa: a API está de pé **e** conseguiu falar com o
banco. Se a segunda estiver `Unhealthy`, o problema é o banco (contêiner parado ou senha errada
no `.env`).

### Teste 2 — Percorra o fluxo completo pela tela

Faça nesta ordem, porque cada passo depende do anterior:

1. **Usuários → Novo usuário.** Crie um veterinário (perfil Veterinário, preencha o CRMV).
2. **Usuários → Novo usuário.** Crie um tutor (perfil Tutor, com telefone).
3. **Pacientes → Novo paciente.** Cadastre um pet e escolha o tutor do passo 2.
   *O sistema não deixa salvar sem tutor — é a RN-001.*
4. **Abra o prontuário do pet → Tratamentos → Novo tratamento.** Descreva o objetivo.
5. **Prontuário → Registrar avaliação.** Preencha os cinco campos clínicos.
   *Tente deixar um em branco: o sistema recusa e diz qual falta — é a RN-010.*
6. **Agenda geral → Nova sessão.** Marque um horário.
   *Tente marcar outra sessão no mesmo horário do mesmo veterinário: o sistema recusa — é a RN-002.*
7. **Prontuário → Vacinação → Registrar.** Lance uma vacina com data da próxima dose.
8. **Prontuário → Receitas → Nova receita.** Adicione dois medicamentos, escolha o
   **veterinário responsável** e emita. Depois clique em imprimir.

> Sobre o campo "Veterinário responsável": ele aparece para você porque está logado como
> administrador, que não é um profissional habilitado a prescrever. A receita é um documento
> técnico e precisa ter um nome e um CRMV. Quando quem emite é o próprio veterinário, o campo
> nem aparece — o sistema assina em nome dele e recusa qualquer outro nome. Se a lista vier
> vazia, é porque ainda não existe veterinário cadastrado: volte ao passo 1.

Depois **saia e entre como o tutor** que você criou. Ele deve ver o próprio pet, a agenda e o
prontuário — **sem** as observações internas, e sem os menus Usuários, Relatórios e Auditoria.

### Teste 3 — O tutor cadastra um pet e a clínica vê na hora

Este teste mostra as duas funcionalidades mais recentes ao mesmo tempo. Ele precisa de **duas
janelas abertas lado a lado** — pode ser o navegador normal numa e uma janela anônima na outra,
para as duas contas não brigarem pela mesma sessão.

1. **Janela A:** entre como **administrador** e deixe a tela **Pacientes** aberta.
2. **Janela B:** entre como **tutor** e vá em **Meus pets**.
3. Na janela B, clique em **Cadastrar pet**. Preencha nome, espécie e data de nascimento e
   salve. Repare que **não existe campo de tutor**: o sistema sabe quem você é e vincula o
   animal a você — é a RN-001 sendo garantida pelo servidor, e não pela tela.
4. **Olhe para a janela A sem tocar em nada.** Em cerca de um segundo o pet novo aparece
   sozinho na lista de pacientes da clínica.

Agora o caminho inverso, que é o que acontece no dia a dia:

5. **Janela A:** vá em **Agenda geral** e deixe aberta numa data em que exista sessão.
6. **Janela B:** vá em **Minha agenda** e clique em **Confirmar presença** numa sessão.
7. **Janela A** muda sozinha: a sessão passa de *Aguardando confirmação* para *Confirmada*.

Vale o mesmo para o cancelamento, e vale nos dois sentidos — se a clínica cancelar uma sessão
na janela A, a agenda do tutor na janela B se atualiza sozinha. No aplicativo do celular é
igual: deixe a tela aberta e ela acompanha o que a clínica faz.

> No rodapé do menu lateral há a indicação **"Atualizando em tempo real"**. Se ela mudar para
> **"Reconectando..."**, a conexão com a API caiu; as telas voltam a buscar os dados de tempos
> em tempos até ela se restabelecer, então nada se perde.

O mesmo cadastro existe no aplicativo: em **Meus pets**, botão **+ Cadastrar**.

### Teste 4 — Os roteiros automáticos

Estes são 154 verificações que o sistema faz em si mesmo. Elas criam dados de demonstração,
então **rode num banco de teste, não no que você já começou a usar de verdade.**

Precisa do Git Bash (vem junto com o Git). Clique com o botão direito na pasta do projeto →
"Open Git Bash here".

```bash
bash testes/criar-dados-demonstracao.sh
bash testes/teste-regras-negocio.sh
bash testes/teste-api-mobile.sh
bash testes/teste-funcionalidades-novas.sh
bash testes/teste-tempo-real.sh
```

Cada um termina com um resumo. O esperado é:

```
 OK: 46   FALHAS: 0
 OK: 26   FALHAS: 0
 OK: 35   FALHAS: 0
 OK: 27   FALHAS: 0
```

Há ainda um quinto roteiro que abre o Microsoft Edge de verdade e clica pelas telas sozinho:

```bash
npm --prefix testes i puppeteer-core@23
node testes/teste-navegador.mjs
```

Ele gera prints em `capturas/` e termina com `OK: 20   FALHAS: 0`.

E os testes das regras isoladas, que não precisam do sistema no ar:

```
dotnet test VetCare.Tests/VetCare.Tests.csproj
```

Esperado: `Aprovado: 91`.

---

## Parte 7 — Conferir que os dados estão mesmo no banco

Esta é a prova definitiva de que nada está só "na tela".

### Jeito 1 — Contar os registros

Cadastre um paciente pelo portal. Depois, no terminal:

```
docker compose exec postgres psql -U postgres -d vetcare_db -c "SELECT \"Nome\", \"Especie\", \"Raca\" FROM \"Pets\";"
```

> Se você estiver no modo desenvolvimento (Parte 4), o comando é
> `docker exec vetcare-postgres psql -U postgres -d vetcare_db -c "..."`

O pet que você acabou de cadastrar tem que aparecer na lista. Se aparecer, está gravado em
disco — não some ao fechar o navegador.

### Jeito 2 — Ver quantos registros há em cada tabela

```
docker compose exec postgres psql -U postgres -d vetcare_db -c "SELECT relname AS tabela, n_live_tup AS registros FROM pg_stat_user_tables WHERE n_live_tup > 0 ORDER BY 2 DESC;"
```

Isso lista só as tabelas que já têm conteúdo. Cadastre algo novo, rode de novo e veja o número
subir. Num banco recém-criado aparecem três linhas:

```
        tabela         | registros
-----------------------+-----------
 __EFMigrationsHistory |         1
 Usuarios              |         1
 Clinicas              |         1
```

`__EFMigrationsHistory` não é dado seu: é o controle interno que registra qual versão do
desenho das tabelas já foi aplicada. `Usuarios` é o administrador e `Clinicas` é a VetSPA.

### Jeito 3 — O teste que não deixa dúvida

1. Cadastre um paciente pelo portal.
2. Desligue tudo: `docker compose stop`
3. Ligue de novo: `docker compose start`
4. Espere um minuto e entre no portal outra vez.

O paciente continua lá. Isso prova que ele está no disco, e não na memória.

### Jeito 4 — A trilha de auditoria

Entre como administrador e abra o menu **Auditoria**. Cada cadastro, alteração, exclusão e até
cada consulta a prontuário aparece ali, com quem fez, quando e de qual endereço de rede. É a
mesma informação da tabela `RegistrosAuditoria`, só que numa tela legível — e dá para exportar
em CSV para abrir no Excel.

---

## Parte 8 — Fazer backup

Os dados são seus e ninguém mais tem cópia. Faça backup antes de qualquer mexida grande:

```
docker compose exec postgres pg_dump -U postgres vetcare_db > backup-vetcare.sql
```

Isso gera um arquivo `backup-vetcare.sql` na pasta do projeto, com tudo dentro. Guarde numa
pasta segura ou na nuvem.

Para restaurar num banco vazio:

```
docker compose exec -T postgres psql -U postgres -d vetcare_db < backup-vetcare.sql
```

---

## Parte 9 — Quando algo dá errado

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| **"Ocorreu um erro inesperado"** ao tentar entrar | Uma API antiga, fora do Docker, está ocupando a porta 5265 | Veja o quadro abaixo desta tabela |
| "Não foi possível falar com o servidor" no portal | A API não está no ar | `docker compose ps` e veja se `api` está `healthy`; senão `docker compose logs api` |
| `/health/pronto` diz `Unhealthy` no banco | Contêiner do banco parado | `docker compose start postgres` |
| A API fica **para sempre** em `health: starting` e `docker compose logs api` mostra `28P01: password authentication failed` | Você trocou `POSTGRES_PASSWORD` no `.env` depois que o banco já existia | Veja o quadro "A senha do banco não bate com o `.env`" abaixo |
| A API fica em `Restarting` e o log diz "Jwt:Chave com pelo menos 32 bytes" | A chave no `.env` tem um `$`, que o Docker apagou junto com o resto | Troque `JWT_CHAVE` por uma frase longa sem `$` e rode `docker compose up -d` |
| "port is already allocated" ao subir | Outro programa usa a porta 5432 ou 8080 | `docker stop vetcare-postgres`, ou mude `POSTGRES_PORT` / `WEB_PORT` no `.env` |
| "Muitas tentativas" no login | Proteção contra ataque de senha | Espere 1 minuto |
| "Conta bloqueada" no login | 5 senhas erradas seguidas (RN-006) | Espere 15 minutos, ou peça a um administrador para redefinir |
| O celular não acha a API | Celular em outra rede | Conecte no mesmo Wi-Fi do computador |
| Tudo travou e você quer recomeçar | — | `docker compose down` e depois `docker compose up -d` (**sem** `-v`, senão apaga os dados) |

---

### A senha do banco não bate com o `.env`

O sintoma: depois de um `docker compose up -d --build`, o `api` nunca sai de `health: starting`,
e `docker compose logs api` repete `password authentication failed for user "postgres"` até a
API desistir, reiniciar e começar de novo.

A causa é uma pegadinha do próprio PostgreSQL. Ele só lê `POSTGRES_PASSWORD` **uma vez**, no
momento em que cria o volume de dados; daí em diante a senha mora dentro do volume, e mudar o
`.env` não muda nada lá. Então, se o banco subiu pela primeira vez com a senha de exemplo e você
só depois colocou a senha de verdade no `.env`, os contêineres antigos continuam funcionando
(ainda carregam a senha antiga), mas o primeiro `--build` recria a API com a senha nova — e ela
para de conseguir entrar.

A correção é alinhar a senha do banco à do `.env`, o que preserva todos os dados. Troque
`SENHA-DO-ENV` pelo valor que está em `POSTGRES_PASSWORD` no seu `.env`:

```
docker exec -it vetcare-postgres-1 psql -U postgres -c "ALTER USER postgres PASSWORD 'SENHA-DO-ENV';"
docker compose restart api
```

Dez segundos depois, `docker compose ps` deve mostrar o `api` como `healthy`.

> Por que o primeiro comando funciona sem pedir senha? Dentro do contêiner, conexões locais são
> de confiança. É só por esse caminho que dá para trocar a senha — pela rede, a API precisa
> acertá-la.

Se preferir recomeçar do zero em vez de corrigir, `docker compose down -v` apaga o volume e o
banco nasce de novo com a senha atual do `.env` — mas **apaga todos os dados**.

### O erro "Ocorreu um erro inesperado" no login

Esse é o tropeço mais comum de quem já rodou o projeto no modo desenvolvimento antes. O
sintoma: o portal abre certinho em http://localhost:8080, mas ao entrar aparece a mensagem
vermelha, e o console do navegador (tecla F12) mostra `500 (Internal Server Error)` em
`:5265/api/usuarios/login`.

**O que está acontecendo.** Existem duas APIs ligadas ao mesmo tempo: a do Docker e uma antiga,
aberta com `dotnet run` numa janela de terminal que ficou esquecida. Quando duas coisas
disputam a mesma porta, o Windows entrega o `localhost` para a que reservou o endereço mais
específico — normalmente a antiga. Só que ela aponta para o banco de desenvolvimento, que você
desligou no Passo 3, então qualquer login dá erro.

**Como confirmar.** Rode:

```
netstat -ano | findstr :5265
```

Olhe **apenas as linhas que terminam em `LISTENING`** e compare o último número de cada uma —
ele identifica o programa. Quando só o Docker está no ar, o número é o mesmo nas duas linhas:

```
  TCP    0.0.0.0:5265     0.0.0.0:0     LISTENING     14528
  TCP    [::]:5265        [::]:0        LISTENING     14528
```

Se aparecer uma linha `LISTENING` com um número **diferente**, ou uma que comece com
`127.0.0.1:5265`, é a API antiga. As linhas que dizem `TIME_WAIT` são conexões já encerradas e
podem ser ignoradas.

**Como resolver.** Feche a janela de terminal onde o `dotnet run` está rodando (ou aperte
`Ctrl + C` dentro dela). Depois confira:

```
curl http://localhost:5265/health/pronto
```

Tem que responder `"status":"Healthy"` nas duas posições. Aí é só recarregar o portal e entrar
normalmente — não precisa reiniciar o Docker.

Se você não encontrar a janela, encerre pelo número do processo (troque `1234` pelo número que
o `netstat` mostrou na última coluna):

```
taskkill /PID 1234 /F
```

---

## Resumo dos endereços

| O quê | Endereço |
|---|---|
| Portal web (Docker) | http://localhost:8080 |
| Portal web (desenvolvimento) | http://localhost:5173 |
| API | http://localhost:5265 |
| Saúde da API e do banco | http://localhost:5265/health/pronto |
| Banco de dados | localhost:5432 — banco `vetcare_db`, usuário `postgres` |
| Aplicativo (Expo/Metro) | http://localhost:8081 — no celular, use o QR Code |

Para alcançar pelo celular, troque `localhost` pelo IP do computador na rede, que você
descobre com `ipconfig`. O aplicativo faz essa troca sozinho; o portal web exige o ajuste
descrito na Parte 5.
