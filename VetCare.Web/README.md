# VetCare.Web

Portal web do VetCare. Atende a equipe da clínica (Administrador, Veterinário e Apoio
administrativo) e também oferece ao Tutor uma visão web do tratamento dos seus pets.

## Executando

```bash
npm install
npm run dev
```

A aplicação abre em `http://localhost:5173` e espera a API em `http://localhost:5265`.
Para apontar para outro endereço, crie um `.env`:

```
VITE_API_URL=https://api.suaclinica.com.br
```

Scripts disponíveis:

| Comando | O que faz |
|---|---|
| `npm run dev` | Servidor de desenvolvimento com HMR |
| `npm run build` | Verificação de tipos e build de produção em `dist/` |
| `npm run preview` | Serve o build gerado |
| `npm run lint` | Oxlint |

## Organização

```
src/
├── components/     Layout com sidebar fixa, UI compartilhada e gráfico de evolução
├── contexts/       AuthContext: sessão, perfil e permissões
├── pages/          uma tela por História de Usuário
│   └── componentes/ modais de agendamento, atendimento, avaliação e tratamento
├── services/       cliente HTTP e tratamento de erros da API
├── types/          contratos compartilhados com a API
└── utils/          formatação de datas, pesos e estilos de status
```

## Navegação por perfil

A sidebar monta os itens a partir do perfil do usuário, e cada rota declara quem pode
alcançá-la (RN-005). Quem não tem permissão é levado à página inicial do próprio perfil
em vez de ver uma tela de erro.

| Perfil | Páginas |
|---|---|
| Administrador | Painel, Agenda geral, Pacientes, Tutores, Mensagens, Notificações, Relatórios, Usuários, Configurações |
| Veterinário | Painel, Agenda, Pacientes, Tutores, Mensagens, Notificações, Relatórios |
| Apoio | Painel, Agenda geral, Pacientes, Tutores, Mensagens, Notificações |
| Tutor | Meus pets, Minha agenda, Mensagens, Notificações |

Esta é uma camada de conveniência de navegação. A defesa real está na API, que repete a
verificação de perfil em cada endpoint.

## Design system

Os tokens ficam em `src/index.css`, no bloco `@theme` do Tailwind 4. As classes
utilitárias do projeto (`vc-card`, `vc-botao-primario`, `vc-campo`, `vc-etiqueta`…) são
declaradas com a diretiva `@utility` — no Tailwind 4 apenas utilitários podem ser
reaproveitados por `@apply` dentro de outras regras, que é como as variantes de botão
derivam de `vc-botao`.
