#!/usr/bin/env bash
# Confere que todos os endpoints consumidos pelo aplicativo do tutor respondem
# como as telas esperam. Roda contra a API em execução.
set -u
source "$(dirname "${BASH_SOURCE[0]}")/comum.sh"
ok=0; falhou=0
CORPO=$(mktemp)

envia() { printf '%s' "$4" > "$CORPO"; curl -s -X "$1" "$API$2" -H "Content-Type: application/json; charset=utf-8" -H "Authorization: Bearer $3" --data-binary @"$CORPO"; }
get() { curl -s "$API$1" -H "Authorization: Bearer $2"; }
val() { extrair "$1" "$2"; }

checa() {
  if grep -q "$2" <<<"$3"; then echo "  OK   $1"; ok=$((ok+1));
  else echo "  FALHA $1"; echo "        esperava: $2"; echo "        recebeu: $(head -c 240 <<<"$3")"; falhou=$((falhou+1)); fi
}

echo "== Endpoints usados pelo app do tutor =="

aguardar_limitador
TK=$(entrar tutor@vetcare.com vetcare123)
checa "POST /api/usuarios/login" "." "$TK"

r=$(get /api/usuarios/me "$TK")
checa "GET /api/usuarios/me devolve tutorId" "tutorId" "$r"
checa "GET /api/usuarios/me perfil Tutor" '"perfil":"Tutor"' "$r"

r=$(get /api/pets/meus "$TK")
checa "GET /api/pets/meus (tela Meus pets)" "Thor" "$r"
PET=$(val "$r" id)

r=$(get "/api/sessoes/minhas?apenasFuturas=true" "$TK")
checa "GET /api/sessoes/minhas (tela Agenda)" "podeCancelar" "$r"
# A agenda do tutor mistura sessões já concluídas, canceladas e em aberto; só as
# que continuam em aberto aceitam confirmação de presença.
SESSAO=$(val "$(grep -o '{[^{}]*}' <<<"$r" | grep -Ev '"status":"(Cancelada|Conclu)' | head -1)" id)

r=$(get "/api/prontuarios/paciente/$PET" "$TK")
checa "GET /api/prontuarios/paciente (tela Prontuário)" "historico" "$r"
checa "prontuário traz evolucaoPeso para o gráfico" "evolucaoPeso" "$r"
checa "prontuário traz evolucaoDor para o gráfico" "evolucaoDor" "$r"
checa "prontuário traz documentos" "documentos" "$r"
checa "prontuário traz tratamentos" "tratamentos" "$r"
checa "RN-003 tutor não recebe observações internas" '"exibeObservacoesInternas":false' "$r"

if grep -q "intervenção cirúrgica" <<<"$r" || grep -q "reage ao manuseio" <<<"$r"; then
  echo "  FALHA RN-003 observação interna vazou no app do tutor"; falhou=$((falhou+1))
else
  echo "  OK   RN-003 nenhuma observação interna no payload do tutor"; ok=$((ok+1))
fi

r=$(get /api/mensagens/conversas "$TK")
checa "GET /api/mensagens/conversas" "^\[" "$r"

r=$(get /api/mensagens/contatos "$TK")
checa "GET /api/mensagens/contatos" "perfil" "$r"
VET=$(val "$r" id)

r=$(envia POST /api/mensagens "$TK" "{\"destinatarioId\":\"$VET\",\"pacienteId\":\"$PET\",\"conteudo\":\"O Thor está apoiando melhor a pata hoje.\"}")
checa "POST /api/mensagens (envio pelo app)" "conteudo" "$r"

r=$(get "/api/mensagens/conversa/$VET" "$TK")
checa "GET /api/mensagens/conversa (histórico)" "apoiando melhor" "$r"

r=$(get /api/mensagens/nao-lidas "$TK")
checa "GET /api/mensagens/nao-lidas (badge da aba)" "^[0-9]" "$r"

r=$(get /api/notificacoes "$TK")
checa "GET /api/notificacoes (tela Avisos)" "titulo" "$r"
NOTIF=$(val "$r" id)

r=$(get /api/notificacoes/nao-visualizadas "$TK")
checa "GET /api/notificacoes/nao-visualizadas (badge)" "^[0-9]" "$r"

r=$(curl -s -o /dev/null -w "%{http_code}" -X PATCH "$API/api/notificacoes/$NOTIF/visualizada" -H "Authorization: Bearer $TK")
checa "PATCH /api/notificacoes/{id}/visualizada" "200" "$r"

r=$(curl -s -o /dev/null -w "%{http_code}" -X PATCH "$API/api/notificacoes/todas/visualizadas" -H "Authorization: Bearer $TK")
checa "PATCH /api/notificacoes/todas/visualizadas" "200" "$r"

r=$(curl -s -X PATCH "$API/api/sessoes/$SESSAO/status" -H "Content-Type: application/json" -H "Authorization: Bearer $TK" -d '{"status":"Confirmada"}')
checa "PATCH /api/sessoes/{id}/status (confirmar presença)" "Confirmada" "$r"

# Troca de senha pelo próprio tutor e retorno ao valor original.
r=$(envia PUT /api/usuarios/me/senha "$TK" '{"senhaAtual":"vetcare123","novaSenha":"novaSenha123"}')
checa "PUT /api/usuarios/me/senha (tela Perfil)" "sucesso" "$r"

TK2=$(entrar tutor@vetcare.com novaSenha123)
checa "login com a nova senha" "." "$TK2"

r=$(envia PUT /api/usuarios/me/senha "$TK2" '{"senhaAtual":"novaSenha123","novaSenha":"vetcare123"}')
checa "senha restaurada para o valor de demonstração" "sucesso" "$r"

r=$(envia PUT /api/usuarios/me/senha "$TK2" '{"senhaAtual":"errada","novaSenha":"outra123"}')
checa "senha atual incorreta é recusada" "incorreta" "$r"

echo
echo "================================"
echo " OK: $ok   FALHAS: $falhou"
echo "================================"
[ "$falhou" -eq 0 ]
