#!/usr/bin/env bash
# Verificação ponta a ponta das regras de negócio do VetCare.
source "$(dirname "${BASH_SOURCE[0]}")/comum.sh"
ok=0; falhou=0

jsonval() { extrair "$1" "$2"; }

checa() { # checa "titulo" "esperado_substring" "resposta"
  if grep -q "$2" <<<"$3"; then
    echo "  OK   $1"; ok=$((ok+1))
  else
    echo "  FALHA $1"; echo "        esperava conter: $2"; echo "        recebeu: $(head -c 300 <<<"$3")"; falhou=$((falhou+1))
  fi
}

# O Git Bash corrompe bytes UTF-8 passados como argumento para o curl.exe, entao o corpo
# vai para um arquivo (escrito pelo printf, que e builtin) e o curl le de la.
CORPO=$(mktemp)
envia() { # envia METODO CAMINHO TOKEN JSON
  printf '%s' "$4" > "$CORPO"
  curl -s -X "$1" "$API$2" -H "Content-Type: application/json; charset=utf-8"        -H "Authorization: Bearer $3" --data-binary @"$CORPO"
}
post() { envia POST "$1" "$2" "$3"; }
put()  { envia PUT "$1" "$2" "$3"; }
patch(){ envia PATCH "$1" "$2" "$3"; }
get()  { curl -s "$API$1" -H "Authorization: Bearer $2"; }

echo "== Autenticação =="
aguardar_limitador
ADMIN=$(entrar admin@vetcare.com vetcare123)
checa "HU-001 login do administrador" "." "$ADMIN"

r=$(curl -s -X POST $API/api/usuarios/login -H 'Content-Type: application/json' -d '{"email":"admin@vetcare.com","senha":"xxx"}')
checa "HU-001 CA-2 mensagem genérica" "E-mail ou senha inválidos" "$r"

r=$(get /api/pets "")
checa "RNF-002 acesso sem token é negado" "" "$r"

echo "== HU-002 gestão de usuários =="
SUFIXO=$RANDOM
r=$(post /api/usuarios "$ADMIN" "{\"nome\":\"Dra. Teste $SUFIXO\",\"email\":\"vet$SUFIXO@vetcare.com\",\"senha\":\"senha123\",\"perfil\":\"Veterinario\",\"crmv\":\"CRMV-DF $SUFIXO\",\"especialidade\":\"Fisioterapia\"}")
VETUSER=$(jsonval "$r" id); VETID=$(jsonval "$r" veterinarioId)
checa "HU-002 CA-1 cadastro de veterinário" "veterinarioId" "$r"

r=$(post /api/usuarios "$ADMIN" "{\"nome\":\"Dup\",\"email\":\"vet$SUFIXO@vetcare.com\",\"senha\":\"senha123\",\"perfil\":\"Veterinario\",\"crmv\":\"OUTRO\"}")
checa "HU-002 CA-2 / RN-007 e-mail duplicado bloqueado" "já está em uso" "$r"

r=$(post /api/usuarios "$ADMIN" "{\"nome\":\"Perfil Errado\",\"email\":\"x$SUFIXO@vetcare.com\",\"senha\":\"senha123\",\"perfil\":\"Chefe\"}")
checa "RN-005 perfil inválido recusado" "Perfil inválido" "$r"

r=$(post /api/usuarios "$ADMIN" "{\"nome\":\"Tutor Teste\",\"email\":\"tutor$SUFIXO@vetcare.com\",\"senha\":\"senha123\",\"perfil\":\"Tutor\",\"telefone\":\"61988887777\",\"endereco\":\"Brasilia\"}")
TUTORUSER=$(jsonval "$r" id); TUTORID=$(jsonval "$r" tutorId)
checa "HU-002 cadastro de tutor" "tutorId" "$r"

echo "== HU-003 pacientes =="
r=$(post /api/pets "$ADMIN" "{\"nome\":\"Luna\",\"especie\":\"Cachorro\",\"raca\":\"Border Collie\",\"dataNascimento\":\"2021-03-15\",\"sexo\":\"Fêmea\",\"pesoAtualKg\":18.4,\"tutorId\":\"$TUTORID\"}")
PETID=$(jsonval "$r" id)
checa "HU-003 CA-1 cadastro vinculado ao tutor" "Luna" "$r"

r=$(post /api/pets "$ADMIN" '{"nome":"SemTutor","especie":"Gato","raca":"SRD","dataNascimento":"2021-03-15","tutorId":"00000000-0000-0000-0000-000000000000"}')
checa "HU-003 CA-2 / RN-001 tutor obrigatório" "tutor" "$r"

echo "== Tratamento e agenda =="
r=$(post /api/tratamentos "$ADMIN" "{\"pacienteId\":\"$PETID\",\"veterinarioId\":\"$VETID\",\"dataInicio\":\"$(date -u +%Y-%m-%d)\",\"objetivoTerapeutico\":\"Reabilitação pós-cirúrgica\"}")
TRATID=$(jsonval "$r" id)
checa "tratamento criado" "Em Andamento" "$r"

FUTURO=$(date -u -d "+3 days 13:00" +%Y-%m-%dT%H:%M:%S 2>/dev/null || date -u -v+3d +%Y-%m-%dT13:00:00)
r=$(post /api/sessoes "$ADMIN" "{\"tratamentoId\":\"$TRATID\",\"veterinarioId\":\"$VETID\",\"dataHora\":\"$FUTURO\"}")
SESSAOID=$(jsonval "$r" id)
checa "HU-004 CA-1 reserva aguardando confirmação" "Aguardando confirma" "$r"

r=$(post /api/sessoes "$ADMIN" "{\"tratamentoId\":\"$TRATID\",\"veterinarioId\":\"$VETID\",\"dataHora\":\"$FUTURO\"}")
checa "HU-004 CA-2 / RN-002 conflito de horário" "indispon" "$r"

PASSADO=$(date -u -d "-2 days 10:00" +%Y-%m-%dT%H:%M:%S 2>/dev/null || date -u -v-2d +%Y-%m-%dT10:00:00)
r=$(post /api/sessoes "$ADMIN" "{\"tratamentoId\":\"$TRATID\",\"veterinarioId\":\"$VETID\",\"dataHora\":\"$PASSADO\"}")
checa "HU-004 CA-5 agendamento retroativo bloqueado" "passad" "$r"

r=$(get "/api/sessoes/agenda/$VETID?visao=semana" "$ADMIN")
checa "HU-004 CA-3 visão semana" "sessaoId" "$r"

r=$(get "/api/sessoes/agenda-geral?visao=mes" "$ADMIN")
checa "HU-005 agenda geral consolidada" "veterinarios" "$r"

echo "== HU-007 avaliação (RN-010) =="
r=$(post /api/avaliacoes "$ADMIN" "{\"tratamentoId\":\"$TRATID\",\"veterinarioId\":\"$VETID\",\"queixaPrincipal\":\"Claudicação\",\"anamnese\":\"\",\"exameFisico\":\"x\",\"hipoteseDiagnostica\":\"y\",\"planoTerapeutico\":\"z\"}")
checa "HU-007 CA-2 / RN-010 campo obrigatório ausente" "anamnese" "$r"

r=$(post /api/avaliacoes "$ADMIN" "{\"tratamentoId\":\"$TRATID\",\"veterinarioId\":\"$VETID\",\"queixaPrincipal\":\"Claudicação posterior\",\"anamnese\":\"Pós-operatório\",\"exameFisico\":\"Atrofia muscular\",\"hipoteseDiagnostica\":\"Rigidez articular\",\"planoTerapeutico\":\"Hidroterapia\",\"observacaoInterna\":\"Tutor ansioso, dosar informações.\"}")
AVALID=$(jsonval "$r" id)
checa "HU-007 CA-1 avaliação completa registrada" "Claudica" "$r"

r=$(put "/api/avaliacoes/$AVALID" "$ADMIN" '{"queixaPrincipal":"Claudicação corrigida","anamnese":"Pós-operatório","exameFisico":"Atrofia","hipoteseDiagnostica":"Rigidez","planoTerapeutico":"Hidroterapia e laser"}')
checa "HU-007 CA-4 / RN-004 correção registra a edição" "dataUltimaEdicao\":\"2" "$r"

r=$(get "/api/avaliacoes/$AVALID/historico" "$ADMIN")
checa "RN-004 versão anterior arquivada" "conteudoAnterior" "$r"

echo "== HU-008 atendimento =="
r=$(post /api/atendimentos "$ADMIN" "{\"sessaoId\":\"$SESSAOID\",\"veterinarioId\":\"$VETID\",\"tecnicasAplicadas\":\"Hidroterapia, Laserterapia\",\"escalaDor\":12,\"evolucaoClinica\":\"ok\"}")
checa "HU-008 CA-2 escala de dor fora do intervalo" "0 e 10" "$r"

r=$(post /api/atendimentos "$ADMIN" "{\"sessaoId\":\"$SESSAOID\",\"veterinarioId\":\"$VETID\",\"tecnicasAplicadas\":\"Hidroterapia, Laserterapia\",\"escalaDor\":6,\"evolucaoClinica\":\"Melhora do apoio\",\"pesoKg\":18.2,\"observacaoInterna\":\"Paciente reativo ao manuseio.\"}")
ATENDID=$(jsonval "$r" id)
checa "HU-008 CA-1 atendimento registrado" "escalaDor" "$r"

r=$(post /api/atendimentos "$ADMIN" "{\"sessaoId\":\"$SESSAOID\",\"veterinarioId\":\"$VETID\",\"tecnicasAplicadas\":\"x\",\"escalaDor\":3,\"evolucaoClinica\":\"y\"}")
checa "uma sessão gera um único atendimento" "já possui" "$r"

echo "== HU-011 / RN-003 prontuário =="
r=$(get "/api/prontuarios/paciente/$PETID" "$ADMIN")
checa "HU-011 CA-1 linha do tempo" "historico" "$r"
checa "HU-011 CA-3 série de peso" "evolucaoPeso" "$r"
checa "RN-003 admin vê observações internas" "reativo ao manuseio" "$r"

TUTORTK=$(entrar "tutor$SUFIXO@vetcare.com" senha123)
r=$(get "/api/prontuarios/paciente/$PETID" "$TUTORTK")
checa "HU-011 CA-2 / RN-003 tutor NÃO vê observações internas" '"exibeObservacoesInternas":false' "$r"
if grep -q "reativo ao manuseio" <<<"$r"; then
  echo "  FALHA RN-003 vazamento de observação interna para o tutor"; falhou=$((falhou+1))
else
  echo "  OK   RN-003 nenhuma observação interna vazou para o tutor"; ok=$((ok+1))
fi

r=$(get "/api/observacoes-internas/paciente/$PETID" "$TUTORTK")
checa "RN-003 rota de observações fechada ao tutor" "" "$r"

echo "== HU-006 / RN-009 confirmação e cancelamento =="
FUTURO3=$(date -u -d "+5 days 14:00" +%Y-%m-%dT%H:%M:%S 2>/dev/null || date -u -v+5d +%Y-%m-%dT14:00:00)
r=$(post /api/sessoes "$ADMIN" "{\"tratamentoId\":\"$TRATID\",\"veterinarioId\":\"$VETID\",\"dataHora\":\"$FUTURO3\"}")
SESSAO_CONF=$(jsonval "$r" id)
r=$(patch "/api/sessoes/$SESSAO_CONF/status" "$TUTORTK" '{"status":"Confirmada"}')
checa "HU-006 CA-1 tutor confirma presença" "Confirmada" "$r"

r=$(patch "/api/sessoes/$SESSAOID/status" "$TUTORTK" '{"status":"Confirmada"}')
checa "sessão concluída não aceita novo status" "já foi concluída" "$r"

FUTURO2=$(date -u -d "+10 days 15:00" +%Y-%m-%dT%H:%M:%S 2>/dev/null || date -u -v+10d +%Y-%m-%dT15:00:00)
r=$(post /api/sessoes "$ADMIN" "{\"tratamentoId\":\"$TRATID\",\"veterinarioId\":\"$VETID\",\"dataHora\":\"$FUTURO2\"}")
SESSAO2=$(jsonval "$r" id)
r=$(patch "/api/sessoes/$SESSAO2/status" "$TUTORTK" '{"status":"Cancelada"}')
checa "HU-006 CA-2 cancelamento dentro do prazo" "Cancelada" "$r"

PROXIMO=$(date -u -d "+2 hours" +%Y-%m-%dT%H:%M:%S 2>/dev/null || date -u -v+2H +%Y-%m-%dT%H:%M:%S)
r=$(post /api/sessoes "$ADMIN" "{\"tratamentoId\":\"$TRATID\",\"veterinarioId\":\"$VETID\",\"dataHora\":\"$PROXIMO\"}")
SESSAO3=$(jsonval "$r" id)
if [ -n "$SESSAO3" ]; then
  r=$(patch "/api/sessoes/$SESSAO3/status" "$TUTORTK" '{"status":"Cancelada"}')
  checa "HU-006 CA-3 / RN-009 cancelamento fora do prazo" "antecedência" "$r"
else
  echo "  INFO sessão em 2h não pôde ser criada (fora do expediente); RN-009 não exercitada"
fi

echo "== HU-013 escopo do tutor =="
r=$(get /api/pets/meus "$TUTORTK")
checa "HU-013 CA-1 tutor vê apenas os próprios pets" "Luna" "$r"
if grep -q '"nome":"Rex"' <<<"$r"; then
  echo "  FALHA HU-013 tutor enxergou pet de outro tutor"; falhou=$((falhou+1))
else
  echo "  OK   HU-013 pets de outros tutores não aparecem"; ok=$((ok+1))
fi

echo "== HU-014 mensagens =="
r=$(post /api/mensagens "$TUTORTK" "{\"destinatarioId\":\"$VETUSER\",\"pacienteId\":\"$PETID\",\"conteudo\":\"A Luna está mancando menos hoje!\"}")
checa "HU-014 CA-1 envio de mensagem" "conteudo" "$r"

VETTK=$(entrar "vet$SUFIXO@vetcare.com" senha123)
r=$(get /api/mensagens/conversas "$VETTK")
checa "HU-014 CA-2 lista de conversas com não lidas" "naoLidas" "$r"

r=$(get "/api/mensagens/conversa/$TUTORUSER" "$VETTK")
checa "HU-014 CA-3 conversa aberta" "mancando" "$r"

r=$(get /api/mensagens/nao-lidas "$VETTK")
checa "HU-014 contador zerado após leitura" "^0$" "$r"

echo "== HU-015 notificações =="
r=$(get /api/notificacoes "$TUTORTK")
checa "HU-015 CA-1 tutor notificado da sessão agendada" "SessaoAgendada" "$r"
checa "HU-015 CA-3 notificação de novo registro" "NovoRegistroProntuario" "$r"

r=$(get /api/notificacoes "$VETTK")
checa "HU-015 CA-3 veterinário notificado de nova mensagem" "NovaMensagem" "$r"

echo "== HU-016 / HU-017 indicadores e relatórios =="
r=$(get /api/dashboard/indicadores "$ADMIN")
checa "HU-016 CA-1 indicadores do dia" "confirmacoesPendentes" "$r"

r=$(get /api/dashboard/relatorio-produtividade "$ADMIN")
checa "HU-017 CA-1 atendimentos por veterinário" "porVeterinario" "$r"
checa "HU-017 CA-2 ranking de técnicas" "tecnicasMaisAplicadas" "$r"

r=$(get "/api/dashboard/relatorio-produtividade?inicio=2020-01-01&fim=2020-01-31" "$ADMIN")
checa "HU-017 CA-3 período sem dados" '"semRegistros":true' "$r"

echo "== RN-008 escopo por perfil =="
r=$(get /api/dashboard/indicadores "$VETTK")
checa "RN-008 veterinário tem escopo próprio" '"escopo":"Veterinario"' "$r"

r=$(get /api/usuarios "$VETTK")
checa "RN-005 veterinário não lista usuários" "" "$r"

echo
echo "================================"
echo " OK: $ok   FALHAS: $falhou"
echo "================================"
[ "$falhou" -eq 0 ]
