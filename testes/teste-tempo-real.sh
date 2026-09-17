#!/usr/bin/env bash
# Comprova as duas funcionalidades novas contra a API em execução:
#
#   1. o tutor cadastra sozinho um animal recém-adquirido, pela web ou pelo aplicativo;
#   2. tudo o que é gravado no banco chega às telas já abertas dos demais perfis.
#
# O segundo ponto é verificado do jeito que o usuário o percebe: uma tela da clínica fica
# pendurada esperando novidade enquanto o tutor age, e medimos quanto tempo levou para ela
# acordar — em vez de confiar que "deve funcionar".
set -u
source "$(dirname "${BASH_SOURCE[0]}")/comum.sh"
ok=0; falhou=0
CORPO=$(mktemp)

# O corpo vai em arquivo porque o Git Bash no Windows corrompe UTF-8 passado como
# argumento de linha de comando, o que quebraria os acentos na desserialização.
envia() { printf '%s' "$4" > "$CORPO"; curl -s -X "$1" "$API$2" -H "Content-Type: application/json; charset=utf-8" -H "Authorization: Bearer $3" --data-binary @"$CORPO"; }
get() { curl -s "$API$1" -H "Authorization: Bearer $2"; }
val() { extrair "$1" "$2"; }

checa() {
  if grep -q "$2" <<<"$3"; then echo "  OK   $1"; ok=$((ok+1));
  else echo "  FALHA $1"; echo "        esperava: $2"; echo "        recebeu: $(head -c 240 <<<"$3")"; falhou=$((falhou+1)); fi
}

checa_diferente() {
  if grep -q "$2" <<<"$3"; then echo "  FALHA $1"; echo "        não deveria conter: $2"; falhou=$((falhou+1));
  else echo "  OK   $1"; ok=$((ok+1)); fi
}

# Versão atual do mural: é a partir dela que cada tela pede "o que mudou". O campo é
# numérico, então o extrair() de comum.sh — que só lê valores textuais — não serve aqui.
versao_do_mural() {
  grep -o '"versao":[0-9]*' <<<"$(get '/api/atualizacoes?desde=-1' "$1")" | head -1 | cut -d: -f2
}

agora_ms() { date +%s%3N; }

aguardar_limitador
TK_ADMIN=$(entrar admin@vetcare.com vetcare123)
TK_TUTOR=$(entrar tutor@vetcare.com vetcare123)
TK_VET=$(entrar veterinario@vetcare.com vetcare123)
TK_APOIO=$(entrar apoio@vetcare.com vetcare123)

echo "== Cadastro de pet pelo próprio tutor =="

NOME_PET="Pipoca $RANDOM"
r=$(envia POST /api/pets/meus "$TK_TUTOR" \
  "{\"nome\":\"$NOME_PET\",\"especie\":\"Gato\",\"raca\":\"SRD\",\"sexo\":\"Fêmea\",\"pelagem\":\"rajada\",\"microchip\":\"\",\"castrado\":true,\"dataNascimento\":\"2024-06-15\",\"pesoAtualKg\":3.8}")
checa "tutor cadastra o próprio pet" "$NOME_PET" "$r"
PET_NOVO=$(val "$r" id)
TUTOR_DO_PET=$(val "$r" tutorId)

# RN-001: o responsável sai do token, não do corpo da requisição.
ME=$(get /api/usuarios/me "$TK_TUTOR")
checa "RN-001 pet nasce vinculado ao tutor autenticado" "$(val "$ME" tutorId)" "$TUTOR_DO_PET"

r=$(get /api/pets/meus "$TK_TUTOR")
checa "pet aparece em Meus pets" "$NOME_PET" "$r"

r=$(get "/api/pets?busca=$(printf '%s' "${NOME_PET%% *}")" "$TK_ADMIN")
checa "pet aparece na lista de pacientes da clínica" "$NOME_PET" "$r"

r=$(get "/api/prontuarios/paciente/$PET_NOVO" "$TK_TUTOR")
checa "prontuário nasce junto com o paciente" "historico" "$r"

r=$(get "/api/auditoria?entidade=Paciente" "$TK_ADMIN")
checa "auditoria registra que o cadastro veio do tutor" "feito pelo tutor" "$r"

# A equipe usa o endpoint próprio; o do tutor é exclusivo dele.
r=$(envia POST /api/pets/meus "$TK_ADMIN" \
  "{\"nome\":\"Invasor\",\"especie\":\"Gato\",\"dataNascimento\":\"2024-01-01\"}")
checa_diferente "RN-005 equipe não usa o cadastro do tutor" '"id":' "$r"

r=$(envia POST /api/pets/meus "$TK_TUTOR" \
  "{\"nome\":\"Futuro\",\"especie\":\"Gato\",\"dataNascimento\":\"2099-01-01\"}")
checa "data de nascimento futura continua recusada" "futura" "$r"

r=$(envia POST /api/pets/meus "$TK_TUTOR" "{\"nome\":\"\",\"especie\":\"Gato\",\"dataNascimento\":\"2024-01-01\"}")
checa "nome vazio continua recusado" "obrigatório" "$r"

echo "== Mural de atualizações =="

V=$(versao_do_mural "$TK_ADMIN")
checa "primeira conexão devolve a versão atual na hora" "^[0-9]\+$" "$V"

r=$(get "/api/atualizacoes?desde=$V&espera=0" "$TK_ADMIN")
checa "sem novidade, a resposta vem sem eventos" '"eventos":\[\]' "$r"

echo "== O tutor confirma a presença e a clínica é avisada =="

# Uma sessão em aberto do tutor, que é o que a tela dele oferece para confirmar.
r=$(get "/api/sessoes/minhas?apenasFuturas=true" "$TK_TUTOR")
SESSAO=$(val "$(grep -o '{[^{}]*}' <<<"$r" | grep -Ev '"status":"(Cancelada|Conclu)' | head -1)" id)
checa "tutor tem sessão em aberto para confirmar" "^[0-9a-f-]\{36\}$" "$SESSAO"

V=$(versao_do_mural "$TK_VET")
ESPERA_VET=$(mktemp); ESPERA_ADM=$(mktemp)

# As telas da clínica ficam penduradas, exatamente como o navegador e o app fazem.
curl -s -m 40 "$API/api/atualizacoes?desde=$V&espera=25" -H "Authorization: Bearer $TK_VET" > "$ESPERA_VET" &
PID_VET=$!
curl -s -m 40 "$API/api/atualizacoes?desde=$V&espera=25" -H "Authorization: Bearer $TK_ADMIN" > "$ESPERA_ADM" &
PID_ADM=$!
ESPERA_APOIO=$(mktemp)
curl -s -m 40 "$API/api/atualizacoes?desde=$V&espera=25" -H "Authorization: Bearer $TK_APOIO" > "$ESPERA_APOIO" &
PID_APOIO=$!
sleep 2

INICIO=$(agora_ms)
r=$(envia PATCH "/api/sessoes/$SESSAO/status" "$TK_TUTOR" '{"status":"Confirmada"}')
checa "HU-006 tutor confirma a presença" "Confirmada" "$r"

wait $PID_VET $PID_ADM $PID_APOIO
DECORRIDO=$(( $(agora_ms) - INICIO ))

r=$(cat "$ESPERA_VET")
checa "a agenda do veterinário é avisada" '"recurso":"sessoes"' "$r"
checa "o aviso diz o que mudou" "Confirmada" "$r"
checa "o aviso diz quem mudou" '"autor":"' "$r"

r=$(cat "$ESPERA_ADM")
checa "a tela do administrativo é avisada" '"recurso":"sessoes"' "$r"

r=$(cat "$ESPERA_APOIO")
checa "HU-005 a agenda geral do apoio é avisada" '"recurso":"sessoes"' "$r"

# O valor é generoso de propósito: o que se verifica é que a tela reage ao evento, e não
# que ficou esperando o prazo de 25 segundos acabar.
if [ "$DECORRIDO" -lt 5000 ]; then
  echo "  OK   aviso chega quase instantaneamente (${DECORRIDO} ms)"; ok=$((ok+1))
else
  echo "  FALHA aviso demorou ${DECORRIDO} ms"; falhou=$((falhou+1))
fi

r=$(get "/api/sessoes/minhas?apenasFuturas=true" "$TK_TUTOR")
checa "a sessão consta como confirmada" '"status":"Confirmada"' "$r"

echo "== A clínica cancela e o aplicativo do tutor é avisado =="

V=$(versao_do_mural "$TK_TUTOR")
ESPERA_TUT=$(mktemp)
curl -s -m 40 "$API/api/atualizacoes?desde=$V&espera=25" -H "Authorization: Bearer $TK_TUTOR" > "$ESPERA_TUT" &
PID_TUT=$!
sleep 2

r=$(envia PATCH "/api/sessoes/$SESSAO/status" "$TK_ADMIN" '{"status":"Cancelada","motivo":"Verificação automatizada"}')
checa "clínica cancela a sessão" "Cancelada" "$r"

wait $PID_TUT
r=$(cat "$ESPERA_TUT")
checa "o aplicativo do tutor é avisado" '"recurso":"sessoes"' "$r"

# RN-003: a clínica tem vários tutores, e o aviso que chega a um deles não pode
# descrever o movimento dos outros. Ele só sabe que algo mudou e recarrega o que é seu.
checa "RN-003 o tutor recebe o aviso sem descrição" '"descricao":""' "$r"
checa "RN-003 o tutor recebe o aviso sem autor" '"autor":""' "$r"

echo "== Uma escrita recusada não acorda tela nenhuma =="

V=$(versao_do_mural "$TK_ADMIN")

envia POST /api/pets/meus "$TK_TUTOR" '{"nome":"","especie":"","dataNascimento":"2024-01-01"}' > /dev/null
envia PATCH "/api/sessoes/$SESSAO/status" "$TK_TUTOR" '{"status":"Concluída"}' > /dev/null

r=$(get "/api/atualizacoes?desde=$V&espera=0" "$TK_ADMIN")
checa "requisição recusada não publica evento" '"eventos":\[\]' "$r"

echo "== O cadastro do tutor também chega à clínica na hora =="

V=$(versao_do_mural "$TK_ADMIN")
ESPERA_ADM2=$(mktemp)
curl -s -m 40 "$API/api/atualizacoes?desde=$V&espera=25" -H "Authorization: Bearer $TK_ADMIN" > "$ESPERA_ADM2" &
PID_ADM2=$!
sleep 2

envia POST /api/pets/meus "$TK_TUTOR" \
  "{\"nome\":\"Fubá $RANDOM\",\"especie\":\"Cachorro\",\"dataNascimento\":\"2025-02-10\"}" > /dev/null

wait $PID_ADM2
r=$(cat "$ESPERA_ADM2")
checa "a clínica é avisada do novo paciente" '"recurso":"pets"' "$r"
checa "o aviso identifica a criação" '"acao":"criado"' "$r"

rm -f "$CORPO" "$ESPERA_VET" "$ESPERA_ADM" "$ESPERA_APOIO" "$ESPERA_TUT" "$ESPERA_ADM2"

echo
echo "================================"
echo " OK: $ok   FALHAS: $falhou"
echo "================================"
[ "$falhou" -eq 0 ]
