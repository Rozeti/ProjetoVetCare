# Testes do VetCare

Além dos testes unitários em `VetCare.Tests`, esta pasta reúne roteiros que exercitam as
Histórias de Usuário e as Regras de Negócio do Documento de Requisitos contra a aplicação
em execução, comprovando o comportamento ponta a ponta.

| Roteiro | Verificações | Precisa de |
|---|---|---|
| `teste-regras-negocio.sh` | 46 | API em `http://localhost:5265` |
| `teste-api-mobile.sh` | 26 | API + dados de demonstração |
| `teste-funcionalidades-novas.sh` | 33 a 35 | API + dados de demonstração |
| `teste-navegador.mjs` | 20 | API + front-end em `http://localhost:5173` + Microsoft Edge |
| `criar-dados-demonstracao.sh` | — | API |

`comum.sh` não é executável: reúne as funções que os roteiros de shell compartilham
(extração de campos JSON e login resiliente à limitação de requisições).

Suba a aplicação antes de rodar:

```bash
# Terminal 1 — API
cd VetCare.API && dotnet run

# Terminal 2 — front-end web
cd VetCare.Web && npm run dev
```

E, como o banco nasce apenas com a conta de Administrador, crie o cenário de avaliação:

```bash
bash testes/criar-dados-demonstracao.sh
```

## 1. Regras de negócio (API)

Autenticação, bloqueio por tentativas (RN-006), unicidade de e-mail (RN-007), vínculo
tutor–paciente (RN-001), conflito e retroatividade de agenda (RN-002), campos
obrigatórios da avaliação (RN-010), escala de dor, privacidade das observações internas
(RN-003), prazo de cancelamento (RN-009), escopo por perfil (RN-005 e RN-008),
indicadores e relatórios.

```bash
bash testes/teste-regras-negocio.sh
```

> Os scripts escrevem o corpo das requisições em arquivo antes de chamar o `curl`. Isso é
> proposital: o Git Bash no Windows corrompe bytes UTF-8 passados como argumento de linha
> de comando, o que faria acentos quebrarem a desserialização JSON.

## 2. Endpoints do aplicativo do tutor

Confere os pontos de integração que as telas do `VetCare.Mobile` consomem, incluindo a
garantia de que o payload entregue ao Tutor não contém observações internas (RN-003).

```bash
bash testes/teste-api-mobile.sh
```

## 3. Funcionalidades clínicas e de plataforma

Carteira de vacinação e classificação das doses, receituário, alertas clínicos,
bloqueios de agenda, trilha de auditoria, paginação (RNF-004), recuperação de senha por
token de uso único e limitação de requisições nos endpoints de autenticação.

```bash
bash testes/teste-funcionalidades-novas.sh
```

> O roteiro termina esgotando a janela do limitador de requisições, de propósito. Todos
> os roteiros esperam a janela liberar antes de começar, então podem ser encadeados sem
> pausa manual.

A contagem varia porque o roteiro se adapta ao ambiente da API, que ele detecta sozinho.
Em `Development` percorre a recuperação de senha de ponta a ponta, usando o token que a
resposta devolve; em `Production` verifica justamente o contrário — que o token não é
exposto e que a documentação da API está desligada.

## 4. Interface web (navegador real)

Roteiro com Puppeteer sobre o Microsoft Edge: percorre login, painel, pacientes,
prontuário com todas as abas, agenda geral, relatórios, auditoria, responsividade em tela
de celular e a visão filtrada do tutor. Gera capturas de tela na pasta indicada.

```bash
npm --prefix testes i puppeteer-core@23   # apenas na primeira vez
node testes/teste-navegador.mjs ./capturas
```

Por padrão o roteiro abre `http://localhost:5173`, o paciente `Thor` e entra como
`tutor@vetcare.com`. Tudo isso é configurável — `WEB_URL` permite apontá-lo para o portal
servido pelo Docker, ou para o IP da máquina na rede local:

```bash
WEB_URL=http://localhost:8080 PACIENTE_DEMO=Nina \
  EMAIL_TUTOR=outro@vetcare.com SENHA_TUTOR=senha \
  node testes/teste-navegador.mjs ./capturas
```

## Dados de demonstração

`criar-dados-demonstracao.sh` monta, a partir da conta de administrador, um cenário
coerente com a documentação: veterinária, recepção, tutor, dois pacientes, alertas
clínicos, carteira de vacinação com uma dose vencida e outra a vencer, tratamento
pós-cirúrgico, avaliação clínica, três atendimentos com evolução de peso e dor, duas
sessões futuras aguardando confirmação, receita com dois medicamentos e observações
internas restritas.

As contas criadas usam a senha `vetcare123` e servem apenas para avaliação do sistema.
**Não execute este script em um ambiente real.**

## Testes unitários

Independem de API no ar e cobrem as regras em isolamento, com banco em memória:

```bash
dotnet test VetCare.Tests/VetCare.Tests.csproj
```
