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

r=$(curl -s -o /dev/null -w "%{http_code}" "$API/api/pets")
checa "RNF-002 acesso sem token é negado (401)" "401" "$r"

echo "== HU-002 gestão de usuários =="
SUFIXO=$RANDOM
r=$(post /api/usuarios "$ADMIN" "{\"nome\":\"Dra. Teste Silva\",\"email\":\"vet$SUFIXO@vetcare.com\",\"senha\":\"senha123\",\"perfil\":\"Veterinario\",\"crmv\":\"crmv df $SUFIXO\",\"especialidade\":\"Fisioterapia\"}")
VETUSER=$(jsonval "$r" id); VETID=$(jsonval "$r" veterinarioId)
checa "HU-002 CA-1 cadastro de veterinário" "veterinarioId" "$r"
checa "CRMV gravado no formato do conselho" "\"crmv\":\"CRMV-DF $SUFIXO\"" "$r"

r=$(post /api/usuarios "$ADMIN" "{\"nome\":\"Duplicado Souza\",\"email\":\"vet$SUFIXO@vetcare.com\",\"senha\":\"senha123\",\"perfil\":\"Veterinario\",\"crmv\":\"CRMV-SP 1$SUFIXO\"}")
checa "HU-002 CA-2 / RN-007 e-mail duplicado bloqueado" "já está em uso" "$r"

r=$(post /api/usuarios "$ADMIN" "{\"nome\":\"Perfil Errado\",\"email\":\"x$SUFIXO@vetcare.com\",\"senha\":\"senha123\",\"perfil\":\"Chefe\"}")
checa "RN-005 perfil inválido recusado" "Perfil inválido" "$r"

r=$(post /api/usuarios "$ADMIN" "{\"nome\":\"Paula Rocha\",\"email\":\"paula$SUFIXO@vetcare.com\",\"perfil\":\"Tutor\"}")
checa "tutor não é cadastrado na tela de usuários" "tela de Tutores" "$r"

r=$(post /api/usuarios "$ADMIN" "{\"nome\":\"Ana Souza 2\",\"email\":\"ana$SUFIXO@vetcare.com\",\"perfil\":\"Apoio\"}")
checa "nome com números é recusado" "sem números" "$r"

r=$(post /api/usuarios "$ADMIN" "{\"nome\":\"Bruno Lima\",\"email\":\"bruno$SUFIXO@vetcare.com\",\"perfil\":\"Veterinario\",\"crmv\":\"CRMV-XX 123\"}")
checa "CRMV com UF inexistente é recusado" "UF válida" "$r"

r=$(post /api/usuarios "$ADMIN" "{\"nome\":\"Bruno Lima\",\"email\":\"bruno$SUFIXO@vetcare.com\",\"senha\":\"abcdefgh\",\"perfil\":\"Apoio\"}")
checa "senha sem números é recusada" "letras e números" "$r"

r=$(post /api/tutores "$ADMIN" "{\"nome\":\"Tutor Teste Lima\",\"email\":\"tutor$SUFIXO@vetcare.com\",\"senha\":\"senha123\",\"telefone\":\"61988887777\",\"endereco\":\"Brasilia, DF\"}")
TUTORUSER=$(jsonval "$r" usuarioId); TUTORID=$(jsonval "$r" id)
checa "HU-002 cadastro de tutor pela tela de tutores" "usuarioId" "$r"
checa "telefone do tutor gravado com DDD e máscara" '"telefone":"(61) 98888-7777"' "$r"

r=$(post /api/tutores "$ADMIN" "{\"nome\":\"Tutor Sem Contato\",\"email\":\"semcontato$SUFIXO@vetcare.com\",\"telefone\":\"8888-7777\"}")
checa "telefone sem DDD é recusado" "DDD" "$r"

r=$(post /api/tutores "$ADMIN" "{\"nome\":\"Tutor Cpf Errado\",\"email\":\"cpf$SUFIXO@vetcare.com\",\"telefone\":\"61988881111\",\"cpf\":\"123.456.789-00\"}")
checa "CPF com dígito verificador errado é recusado" "CPF inválido" "$r"

echo "== HU-003 pacientes =="
r=$(post /api/pets "$ADMIN" "{\"nome\":\"Luna\",\"especie\":\"Cachorro\",\"raca\":\"Border Collie\",\"dataNascimento\":\"2021-03-15\",\"sexo\":\"Fêmea\",\"pesoAtualKg\":18.4,\"tutorId\":\"$TUTORID\",\"veterinarioResponsavelId\":\"$VETID\"}")
PETID=$(jsonval "$r" id)
checa "HU-003 CA-1 cadastro vinculado ao tutor" "Luna" "$r"
checa "Paciente nasce com veterinário responsável" "nomeVeterinarioResponsavel\":\"Dra. Teste" "$r"

r=$(post /api/pets "$ADMIN" '{"nome":"SemTutor","especie":"Cachorro","raca":"SRD","dataNascimento":"2021-03-15","tutorId":"00000000-0000-0000-0000-000000000000"}')
checa "HU-003 CA-2 / RN-001 tutor obrigatório" "tutor" "$r"

r=$(post /api/pets "$ADMIN" "{\"nome\":\"Mingau\",\"especie\":\"Gato\",\"raca\":\"SRD\",\"dataNascimento\":\"2021-03-15\",\"tutorId\":\"$TUTORID\",\"veterinarioResponsavelId\":\"$VETID\"}")
checa "a clínica atende apenas cães" "apenas cães" "$r"

echo "== HU-007 avaliação (RN-010) =="
# A avaliação pertence ao animal e é o ponto de partida do fluxo clínico.
r=$(post /api/avaliacoes "$ADMIN" "{\"pacienteId\":\"$PETID\",\"veterinarioId\":\"$VETID\",\"motivoConsulta\":\"Claudicação\",\"observacoes\":\"\",\"diagnosticoPossivel\":\"y\"}")
checa "HU-007 CA-2 / RN-010 campo obrigatório ausente" "observa" "$r"

r=$(post /api/avaliacoes "$ADMIN" "{\"pacienteId\":\"$PETID\",\"veterinarioId\":\"$VETID\",\"motivoConsulta\":\"Claudicação posterior\",\"observacoes\":\"Pós-operatório; atrofia muscular\",\"diagnosticoPossivel\":\"Rigidez articular\",\"observacaoInterna\":\"Tutor ansioso, dosar informações.\"}")
AVALID=$(jsonval "$r" id)
checa "HU-007 CA-1 avaliação completa registrada" "Claudica" "$r"

r=$(put "/api/avaliacoes/$AVALID" "$ADMIN" '{"motivoConsulta":"Claudicação corrigida","observacoes":"Pós-operatório","diagnosticoPossivel":"Rigidez"}')
checa "HU-007 CA-4 / RN-004 correção registra a edição" "dataUltimaEdicao\":\"2" "$r"

r=$(get "/api/avaliacoes/$AVALID/historico" "$ADMIN")
checa "RN-004 versão anterior arquivada" "conteudoAnterior" "$r"

echo "== Tratamento a partir da avaliação e agenda =="
r=$(post /api/tratamentos "$ADMIN" "{\"veterinarioId\":\"$VETID\",\"nome\":\"Sem origem\",\"tipo\":\"Fisioterapia\",\"descricao\":\"Teste\",\"duracaoPrevista\":\"1 mês\",\"frequencia\":\"semanal\",\"sessoesPrevistas\":4}")
checa "tratamento sem avaliação de origem é recusado" "valia" "$r"

r=$(post /api/tratamentos "$ADMIN" "{\"avaliacaoId\":\"$AVALID\",\"veterinarioId\":\"$VETID\",\"nome\":\"Reabilitação pós-cirúrgica\",\"tipo\":\"Fisioterapia\",\"descricao\":\"Hidroterapia e laserterapia\",\"duracaoPrevista\":\"8 semanas\",\"frequencia\":\"2 sessões por semana\",\"sessoesPrevistas\":8}")
TRATID=$(jsonval "$r" id)
checa "tratamento criado a partir da avaliação (planejado)" "Planejado" "$r"
checa "tratamento guarda a avaliação de origem" "\"avaliacaoId\":\"$AVALID\"" "$r"

FUTURO=$(date -u -d "+3 days 13:00" +%Y-%m-%dT%H:%M:%S 2>/dev/null || date -u -v+3d +%Y-%m-%dT13:00:00)
r=$(post /api/sessoes "$ADMIN" "{\"tratamentoId\":\"$TRATID\",\"veterinarioId\":\"$VETID\",\"dataHora\":\"$FUTURO\"}")
SESSAOID=$(jsonval "$r" id)
checa "HU-004 CA-1 reserva aguardando confirmação" "Aguardando confirma" "$r"

r=$(post /api/sessoes "$ADMIN" "{\"tratamentoId\":\"$TRATID\",\"veterinarioId\":\"$VETID\",\"dataHora\":\"$FUTURO\"}")
checa "HU-004 CA-2 / RN-002 conflito de horário" "indispon" "$r"

PASSADO=$(date -u -d "-2 days 10:00" +%Y-%m-%dT%H:%M:%S 2>/dev/null || date -u -v-2d +%Y-%m-%dT10:00:00)
r=$(post /api/sessoes "$ADMIN" "{\"tratamentoId\":\"$TRATID\",\"veterinarioId\":\"$VETID\",\"dataHora\":\"$PASSADO\"}")
checa "HU-004 CA-5 agendamento retroativo bloqueado" "passad" "$r"

# A semana consultada é a da sessão recém-criada: em quinta, sexta ou sábado ela cai na
# semana seguinte, e a visão da semana corrente viria vazia sem que nada estivesse errado.
r=$(get "/api/sessoes/agenda/$VETID?visao=semana&data=${FUTURO%%T*}" "$ADMIN")
checa "HU-004 CA-3 visão semana" "sessaoId" "$r"

r=$(get "/api/sessoes/agenda-geral?visao=mes" "$ADMIN")
checa "HU-005 agenda geral consolidada" "veterinarios" "$r"

echo "== HU-008 evolução =="
r=$(post /api/atendimentos "$ADMIN" "{\"sessaoId\":\"$SESSAOID\",\"veterinarioId\":\"$VETID\",\"tecnicasAplicadas\":\"Hidroterapia, Laserterapia\",\"escalaDor\":12,\"evolucaoClinica\":\"ok\",\"reacaoAnimal\":\"ok\"}")
checa "HU-008 CA-2 escala de dor fora do intervalo" "0 e 10" "$r"

r=$(post /api/atendimentos "$ADMIN" "{\"sessaoId\":\"$SESSAOID\",\"veterinarioId\":\"$VETID\",\"tecnicasAplicadas\":\"Hidroterapia\",\"escalaDor\":4,\"evolucaoClinica\":\"Melhora do apoio\"}")
checa "evolução exige a reação do animal" "reagiu" "$r"

r=$(post /api/atendimentos "$ADMIN" "{\"sessaoId\":\"$SESSAOID\",\"veterinarioId\":\"$VETID\",\"tecnicasAplicadas\":\"Hidroterapia, Laserterapia\",\"escalaDor\":6,\"evolucaoClinica\":\"Melhora do apoio\",\"reacaoAnimal\":\"Tolerou bem a esteira\",\"relatorio\":\"Sessão sem intercorrências\",\"pesoKg\":18.2,\"observacaoInterna\":\"Paciente reativo ao manuseio.\"}")
ATENDID=$(jsonval "$r" id)
checa "HU-008 CA-1 evolução registrada com o número da sessão" "\"numeroSessao\":1" "$r"

r=$(get "/api/tratamentos/$TRATID" "$ADMIN")
checa "a primeira evolução põe o tratamento em andamento" "Em Andamento" "$r"

r=$(post /api/atendimentos "$ADMIN" "{\"sessaoId\":\"$SESSAOID\",\"veterinarioId\":\"$VETID\",\"tecnicasAplicadas\":\"x\",\"escalaDor\":3,\"evolucaoClinica\":\"y\",\"reacaoAnimal\":\"z\"}")
checa "uma sessão é registrada uma única vez" "já foi registrada" "$r"

# Sessão realizada sem agendamento: a evolução nasce do tratamento, e a sessão entra na
# agenda já concluída.
r=$(post /api/atendimentos "$ADMIN" "{\"tratamentoId\":\"$TRATID\",\"veterinarioId\":\"$VETID\",\"tecnicasAplicadas\":\"Cinesioterapia\",\"escalaDor\":3,\"evolucaoClinica\":\"Exercícios de propriocepção\",\"reacaoAnimal\":\"Colaborativo\"}")
checa "sessão sem agendamento registrada direto no tratamento" "\"numeroSessao\":2" "$r"
SESSAOLIVRE=$(jsonval "$r" sessaoId)
r=$(get "/api/sessoes/tratamento/$TRATID" "$ADMIN")
checa "a sessão sem agendamento entra na agenda já concluída" "Conclu" "$(grep -o "\"id\":\"$SESSAOLIVRE\"[^}]*" <<<"$r")"

r=$(post /api/atendimentos "$ADMIN" "{\"veterinarioId\":\"$VETID\",\"tecnicasAplicadas\":\"x\",\"escalaDor\":3,\"evolucaoClinica\":\"y\",\"reacaoAnimal\":\"z\"}")
checa "evolução exige a sessão ou o tratamento" "sessão agendada" "$r"

r=$(get "/api/atendimentos?tratamentoId=$TRATID" "$ADMIN")
checa "evoluções filtradas pelo tratamento" "\"nomeTratamento\":\"Reabilitação" "$r"

echo "== Receitas e pedidos de exame =="
r=$(post /api/prescricoes "$ADMIN" "{\"pacienteId\":\"$PETID\",\"veterinarioId\":\"$VETID\",\"itens\":[{\"medicamento\":\"Meloxicam 2mg\",\"dosagem\":\"meio comprimido\",\"frequencia\":\"a cada 24 horas\"}]}")
checa "receita sem origem é recusada" "origem da receita" "$r"

r=$(post /api/prescricoes "$ADMIN" "{\"pacienteId\":\"$PETID\",\"veterinarioId\":\"$VETID\",\"atendimentoId\":\"$ATENDID\",\"itens\":[{\"medicamento\":\"Meloxicam 2mg\",\"dosagem\":\"meio comprimido\",\"frequencia\":\"a cada 24 horas\"}]}")
checa "receita emitida a partir da sessão registrada" "Sessão nº 1" "$r"

r=$(post /api/pedidos-exame "$ADMIN" "{\"avaliacaoId\":\"$AVALID\",\"exames\":\"Raio-X do joelho\",\"justificativa\":\"Controle pós-operatório\"}")
checa "pedido de exame emitido a partir da avaliação" "\"avaliacaoId\":\"$AVALID\"" "$r"
PEDIDOID=$(jsonval "$r" id)

r=$(put "/api/pedidos-exame/$PEDIDOID" "$ADMIN" "{\"avaliacaoId\":\"$AVALID\",\"exames\":\"Ultrassonografia do joelho\",\"justificativa\":\"Avaliar o menisco\"}")
checa "pedido de exame editado" "Ultrassonografia do joelho" "$r"

# Excluir tira o pedido do sistema: ele não fica no prontuário como cancelado.
r=$(curl -s -X DELETE "$API/api/pedidos-exame/$PEDIDOID" -H "Authorization: Bearer $ADMIN")
checa "pedido de exame excluído" "exclu" "$r"
r=$(get "/api/pedidos-exame/paciente/$PETID" "$ADMIN")
if grep -q "$PEDIDOID" <<<"$r"; then
  echo "  FALHA o pedido excluído sai do prontuário"; falhou=$((falhou+1))
else
  echo "  OK   o pedido excluído sai do prontuário"; ok=$((ok+1))
fi

echo "== Situação do tratamento =="
r=$(put "/api/tratamentos/$TRATID" "$ADMIN" '{"status":"Inativado"}')
checa "tratamento inativado" "Inativado" "$r"

r=$(get "/api/atendimentos?tratamentoId=$TRATID" "$ADMIN")
checa "inativar não apaga as evoluções" "\"numeroSessao\":1" "$r"

r=$(put "/api/tratamentos/$TRATID" "$ADMIN" '{"status":"Em Andamento"}')
checa "tratamento reativado" "Em Andamento" "$r"

r=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$API/api/pets/$PETID" -H "Authorization: Bearer $ADMIN")
checa "paciente não é excluído, só inativado (405)" "405" "$r"

echo "== HU-011 / RN-003 prontuário =="
r=$(get "/api/prontuarios/paciente/$PETID" "$ADMIN")
checa "HU-011 CA-1 linha do tempo" "historico" "$r"
checa "linha do tempo com categoria e responsável" "\"categoria\":\"Sessão\"" "$r"
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

r=$(curl -s -o /dev/null -w "%{http_code}" "$API/api/observacoes-internas/paciente/$PETID" -H "Authorization: Bearer $TUTORTK")
checa "RN-003 rota de observações fechada ao tutor (403)" "403" "$r"

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
if grep -q '"nome":"Thor"' <<<"$r"; then
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

echo "== HU-016 indicadores =="
r=$(get /api/dashboard/indicadores "$ADMIN")
checa "HU-016 CA-1 indicadores do dia" "confirmacoesPendentes" "$r"

echo "== RN-008 escopo por perfil =="
r=$(get /api/dashboard/indicadores "$VETTK")
checa "RN-008 veterinário tem escopo próprio" '"escopo":"Veterinario"' "$r"

r=$(curl -s -o /dev/null -w "%{http_code}" "$API/api/usuarios" -H "Authorization: Bearer $VETTK")
checa "RN-005 veterinário não lista usuários (403)" "403" "$r"

echo "== Qualquer veterinário atende qualquer paciente =="
# Um colega cobre a ausência da veterinária responsável pela Luna, sem transferência.
r=$(post /api/usuarios "$ADMIN" "{\"nome\":\"Dr. Cobertura Lima\",\"email\":\"cobertura$SUFIXO@vetcare.com\",\"senha\":\"senha123\",\"perfil\":\"Veterinario\",\"crmv\":\"CRMV-DF 2$SUFIXO\",\"especialidade\":\"Clínica geral\"}")
VET2ID=$(jsonval "$r" veterinarioId)
VET2TK=$(entrar "cobertura$SUFIXO@vetcare.com" senha123)

r=$(get "/api/pets?busca=Luna" "$VET2TK")
checa "o colega encontra o paciente na lista" "Luna" "$r"

r=$(get "/api/prontuarios/paciente/$PETID" "$VET2TK")
checa "o colega abre o prontuário do paciente" "historico" "$r"

r=$(curl -s -o /dev/null -w "%{http_code}" -X PATCH "$API/api/pets/$PETID/veterinario-responsavel" -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" -d "{\"veterinarioId\":\"$VET2ID\"}")
checa "a transferência de paciente deixou de existir (404)" "404" "$r"

r=$(put "/api/pets/$PETID" "$ADMIN" "{\"nome\":\"Luna\",\"especie\":\"Cachorro\",\"raca\":\"Border Collie\",\"sexo\":\"Fêmea\",\"dataNascimento\":\"2021-03-15\",\"pesoAtualKg\":18.4,\"tutorId\":\"$TUTORID\",\"veterinarioResponsavelId\":\"$VET2ID\"}")
checa "o responsável é trocado na edição do cadastro" "nomeVeterinarioResponsavel\":\"Dr. Cobertura" "$r"

echo
echo "================================"
echo " OK: $ok   FALHAS: $falhou"
echo "================================"
[ "$falhou" -eq 0 ]
