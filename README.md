# Clube da Onça

App do clube social em **.NET MAUI** (Android, iOS, Windows) com API **ASP.NET Core** + PostgreSQL.
Planejamento completo das fases: carteirinha com QR, reservas, financeiro, eventos e ocorrências.

## Estrutura

| Projeto | Papel |
|---|---|
| `src/ClubeDaOnca.App` | App MAUI (MVVM com CommunityToolkit, Shell, Refit) |
| `src/ClubeDaOnca.Api` | API Minimal (Identity + JWT, EF Core/Npgsql, Scalar) |
| `src/ClubeDaOnca.Domain` | Regras de negócio puras (perfis, CPF, ...) |
| `src/ClubeDaOnca.Contracts` | DTOs compartilhados entre App e API |
| `tests/*` | xUnit; testes da API usam Testcontainers (precisa do Docker) |

## Rodando em desenvolvimento

```powershell
docker compose up -d db                                   # Postgres na porta 5440
dotnet run --project src/ClubeDaOnca.Api --launch-profile http   # API em http://localhost:5080
```

- Documentação interativa: http://localhost:5080/scalar
- Admin inicial (só em Development): `admin@clubedaonca.local` / `Admin@1234`
- Contas de exemplo (só em Development, `Seed:UsuariosExemplo`): `portaria@clubedaonca.local` (Portaria) e `presidente@clubedaonca.local` (presidente do mandato vigente), as duas com a senha `Admin@1234`

App:

```powershell
dotnet build src/ClubeDaOnca.App -t:Run -f net10.0-windows10.0.19041.0   # Windows
dotnet build src/ClubeDaOnca.App -t:Run -f net10.0-android               # emulador Android
```

O emulador Android acessa a API por `10.0.2.2`. Para celular físico, ajuste `Services/ApiConfig.cs` com o IP do PC.
iOS exige um Mac (pareado ao Visual Studio ou em nuvem).

## Fase 1: cadastro, carteirinha e portaria

- **Sócios** (`/socios`, perfis Secretaria/Admin): cadastro do titular com número de título sequencial (a partir de 1000), dependentes, situação do título (Ativo, Suspenso, Cancelado) e renovação do QR. Ao cadastrar, a API cria a conta do sócio e devolve uma **senha provisória**, que só aparece uma vez. No primeiro acesso o app obriga a troca dessa senha.
- **Carteirinha** (`/carteirinha`): titular e dependentes ativos. O QR é dinâmico (muda a cada 30 s e vale cerca de 1 minuto) e o app gera o código sozinho com uma chave por pessoa, por isso funciona sem internet.
- **Portaria** (`/portaria/validar`, perfis Portaria/Secretaria/Admin): a validação é sempre feita na API, e toda leitura fica registrada em `/portaria/acessos`. No app, o campo de leitura aceita leitor de QR USB/Bluetooth, que funciona como teclado.
- **Renovar QR**: use quando o sócio perder o celular. Os códigos antigos deixam de valer.

A portaria também lê o QR pela **câmera** (botão "📷 Câmera").

## Fase 2: reservas de espaços

- **Espaços** (`/espacos`; a secretaria cadastra e o sócio consulta): dias e horário de funcionamento, duração de cada horário, antecedência mínima e máxima, limite de reservas ativas por título, taxa, prazo de cancelamento e se a reserva exige aprovação.
- **Agenda** (`/espacos/{id}/agenda?data=`): horários livres, reservados ou indisponíveis. Todas as regras são aplicadas no servidor, no fuso do clube (`Clube:FusoHorario`, padrão `America/Sao_Paulo`).
- **Reservas** (`/reservas`): o titular reserva, acompanha e cancela dentro do prazo. A secretaria aprova, recusa (informando o motivo) ou cancela. Um índice único no banco impede duas reservas ativas no mesmo horário, mesmo com pedidos simultâneos.
- Em desenvolvimento são criados três espaços de exemplo (`Seed:EspacosExemplo`).
- Quando a reserva fica confirmada, a taxa vira uma cobrança no Financeiro. Se a reserva for cancelada ou recusada, a cobrança ainda em aberto é cancelada.

## Fase 3: financeiro

- **Mensalidades**: o valor depende da categoria (Individual, Familiar, Remido) e o vencimento cai no dia configurado. Um job do **Hangfire** gera as mensalidades todo dia 1º às 6h, e a secretaria também pode gerá-las pelo app. Repetir a geração não duplica nada, porque há um índice único por título e mês.
- **Pagamento**: o sócio paga por **Pix** (QR e copia-e-cola) ou **boleto**. O gateway avisa por **webhook** e a baixa é automática. Receber o mesmo aviso duas vezes não causa problema.
- **Gestão**: lista de cobranças (vencidas, em aberto, pagas), baixa manual de pagamentos feitos no balcão, cancelamento (isenções), lista de inadimplentes e configuração de valores e regras (só o Admin pode alterar).
- **Inadimplência**: o título fica inadimplente quando há cobrança vencida além da tolerância. As reservas ficam bloqueadas, e na portaria aparece um aviso âmbar ou o bloqueio, conforme a configuração.
- **Gateway**: `Pagamentos:Gateway` = `Asaas` em produção. Em desenvolvimento usa-se o `Simulado`, que tem o botão "Simular pagamento confirmado". O painel dos jobs fica em http://localhost:5080/hangfire (só em desenvolvimento).

### Ligando o Asaas (sandbox ou produção)

1. Crie a conta em https://sandbox.asaas.com e gere a chave da API (Integrações → Chave de API).
2. Configure as variáveis `Pagamentos__Gateway=Asaas`, `Pagamentos__Asaas__ApiKey=...`, `Pagamentos__Asaas__WebhookToken=<um segredo seu>` e, em produção, `Pagamentos__Asaas__Url=https://api.asaas.com/v3`.
3. No Asaas, cadastre o webhook apontando para `https://<sua-api>/webhooks/asaas`, com os eventos de cobrança e o mesmo token de autenticação.

## Fase 4: várias associações (unidades)

- **Isolamento**: cada associação tem seus próprios sócios, espaços, reservas, cobranças, portaria e configuração financeira. Todas as tabelas têm `AssociacaoId`, e um filtro global do EF Core aplica a associação do usuário logado (claim `associacao_id` do JWT). Sem associação definida, as consultas não retornam nada.
- **Login**: o usuário informa só o e-mail (ou CPF) e a senha. O sistema descobre em qual associação a conta foi cadastrada. Cada conta pertence a uma associação. Se a mesma pessoa estiver em duas unidades, ela usa um e-mail diferente em cada uma.
- **Perfis**: o **SuperAdmin** cadastra as unidades e, ao criar uma, já recebe o login e a senha provisória do administrador dela. O **Admin** mantém só os dados da própria unidade. Desativar uma associação bloqueia o login dos usuários dela, e os dados são mantidos.
- **Diretoria**: o presidente tem mandato de 2 anos (o fim é calculado e não pode haver sobreposição), com aviso 60 dias antes do fim quando não há sucessor. Os diretores são cadastrados com cargo e período.
- **Migração**: a migração `MultiAssociacao` cria a associação padrão "Clube da Onça" e atribui a ela todos os dados existentes. O admin inicial recebe também o perfil SuperAdmin.
- **Jobs e webhook**: o job de mensalidades percorre todas as associações ativas. O webhook do gateway localiza o pagamento em qualquer associação.

## Fase 5: cadastro completo e histórico

- **Cadastro do associado**: nome, e-mail, telefone, CPF, data de nascimento, gênero (Masculino, Feminino, Outro ou Prefere não informar), qualificação/profissão, nacionalidade, estado civil, RG (com órgão emissor), endereço completo e data de cadastro.
  - A data de cadastro vem preenchida com hoje e pode ser alterada para sócios antigos.
  - O app busca o endereço pelo CEP no ViaCEP.
  - Endereço, gênero e profissão são opcionais para salvar. Serão exigidos ao gerar o contrato.
- **Titular ou dependente**: o cabeçalho mostra "Titular · Título nº X" ou "Dependente de Fulano". O dependente também tem gênero, e-mail, telefone e profissão. O endereço dele é o do titular.
- **Histórico de alterações**: toda inclusão ou alteração de sócio, dependente ou associação é gravada campo a campo na tabela `HistoricoAlteracoes`, com quem fez, quando, o valor anterior e o novo.
  - A gravação acontece no próprio `SaveChanges` do `AppDbContext`, então nenhuma tela consegue escapar dela.
  - A tela do sócio mostra o histórico, incluindo o dos dependentes, e no rodapé "Última alteração por X em dd/mm/aaaa hh:mm". A API correspondente é `GET /socios/{id}/historico`.
- **Filtros**: a lista de sócios filtra por gênero e situação (`GET /socios?genero=Feminino&situacao=Ativo`).

## Fase 6: contrato de adesão

- **Fluxo**:
  1. A secretaria cria o contrato. Valor e vencimento vêm da configuração financeira e podem ser ajustados.
  2. A secretaria envia a proposta. O candidato recebe por e-mail o contrato em PDF e um link de concordância, válido por 15 dias.
  3. O candidato concorda pelo link, numa página pública. O sistema registra data, hora e IP.
  4. O **presidente do mandato vigente** aprova ou reprova a ficha numa tela do app. O presidente não recebe e-mail.
  5. Na aprovação, a vigência fica em 12 meses e o sócio passa para **Ativo**.
- **Candidato**: todo sócio novo nasce **"Em adesão"**, sem acesso na portaria, sem reservas e sem mensalidade. Na reprovação, vira Cancelado.
- **Modelo**: `src/ClubeDaOnca.Api/Templates/ContratoAdesao.docx`.
  - Os campos `{{...}}` são preenchidos pelo sistema.
  - A linha `{{Dependente.*}}` da tabela é repetida para cada dependente.
  - O texto pode ser editado no Word, desde que os campos entre chaves continuem iguais.
- **PDF**: gerado pelo LibreOffice (caminho em `Contratos:LibreOffice`; se não informado, o sistema procura nos locais padrão). Sem LibreOffice, o sistema envia o .docx. A imagem Docker já instala o LibreOffice.
- **Pendências**: o contrato só é gerado com o cadastro completo: endereço, profissão, nacionalidade, estado civil e RG.
- **Presidente**: ao cadastrar o mandato com e-mail, o sistema cria a conta dele e mostra a senha provisória uma única vez. A tela "Fichas para aprovação" aparece na Início.
- **Desligamento**: cancela as mensalidades que ainda vão vencer e deixa o título Cancelado. As parcelas já vencidas continuam devidas.
- **Renovação**:
  - Manual: gera um novo rascunho com os dados atuais, que passa de novo por aceite e aval.
  - Automática: um job diário às 5h30 renova os contratos vencidos que têm renovação automática. O anterior fica "Encerrado".
- **E-mail** (`Email:Modo`):
  - `Smtp` em produção: configurar `Email:Smtp:Host`, `Porta`, `Usuario`, `Senha` e `Email:Remetente`.
  - `Arquivo` em desenvolvimento: grava um .eml em `src/ClubeDaOnca.Api/App_Data/emails`.
  - `Memoria` nos testes.
- **URL do link de aceite**: `Contratos:UrlPublica` (em produção, o endereço público da API).

## Fase 7: 12 parcelas e boletos a partir do contrato

- **Na aprovação do presidente**, o sistema cria as 12 mensalidades do contrato.
  - Cada uma tem número de parcela (1/12 a 12/12), competência, valor e vencimento. A primeira vence no "primeiro vencimento" do contrato; as demais, no dia de vencimento de cada mês.
  - Depois, o sistema registra **um boleto por parcela** no gateway. Com o Hangfire ligado isso roda em segundo plano, com novas tentativas automáticas. Os que falharem podem ser emitidos de novo pelo botão "Emitir boletos pendentes", sem duplicar.
- **Telas**:
  - O contrato mostra as parcelas com número, data de emissão, vencimento, valor, situação e linha digitável.
  - O financeiro do sócio e a gestão financeira mostram as mesmas informações.
- **Job mensal**: não gera mensalidade avulsa para quem tem contrato aprovado cobrindo o mês. Continua gerando para os sócios antigos sem contrato.
- **Cancelamento**: cancelar uma cobrança, desligar o sócio ou cancelar uma reserva também cancela o boleto/Pix no Asaas.
- **Conta do Asaas por associação**: em **Gestão → Associação → Pagamentos (Asaas)**, o administrador cadastra a chave da API e o token do webhook da unidade.
  - As chaves ficam criptografadas no banco (ASP.NET Data Protection, com as chaves de criptografia na tabela `DataProtectionKeys`) e nunca voltam pela API.
  - Sem chave própria, a unidade usa a conta geral do servidor (`Pagamentos:Asaas:ApiKey`).
  - O webhook de cada unidade é `https://<api>/webhooks/asaas/<id-da-associação>`. O endereço aparece na tela.

## Fase 8: eventos, locais e reservas no evento

- **Locais**: os espaços ganharam **tipo** (quadra, quiosque, churrasqueira, salão, piscina, campo, auditório) e **capacidade de pessoas**. Novos locais são cadastrados em Gestão → Espaços.
- **Cadastro do evento** (Gestão → 🎉 Eventos):
  - Dados: título, descrição, início e término, um ou mais locais, capacidade (até a soma da capacidade dos locais), "aberto ao público", divulgar aos associados, divulgar nas redes sociais (só se aberto ao público) e prazo de cancelamento das reservas.
- **Agenda primeiro**:
  - O botão "Verificar agenda" mostra se os locais estão livres. Ao salvar, o sistema **bloqueia** o evento se houver reserva comum ou outro evento no período e lista os conflitos.
  - O evento, mesmo em rascunho, ocupa a agenda: os horários aparecem ocupados e as reservas comuns nesse período são recusadas.
- **Ambientes com vagas**: cada evento tem tipos de ambiente (ex.: "Mesa para 8" com 40 vagas, "Quiosque" com 6). Cada ambiente define pessoas por reserva, limite por título e valor (opcional).
- **Publicação**: o rascunho só aparece para os sócios depois de publicado.
- **Reserva do sócio** (aba **Eventos** do app):
  - O sócio reserva um ambiente informando quantas pessoas vão.
  - O sistema respeita as vagas, o limite por título e a capacidade total. A linha do evento fica travada durante a reserva, para a última vaga não sair duas vezes.
  - Se o ambiente tiver valor, a taxa entra no financeiro do sócio (Pix ou boleto).
  - O sócio cancela pelo app até o prazo definido. A taxa não paga é cancelada junto.
- **Cancelar o evento**: cancela todas as reservas e as taxas ainda não pagas (também no Asaas) e libera a agenda dos locais.
- **Divulgação**: as opções ficam guardadas no evento. O envio pelo app, e-mail, WhatsApp e redes sociais é a Fase 9.

## Fase 9: divulgação e avisos

- **Central de avisos no app**: o card "🔔 Avisos" na tela Início mostra a contagem de não lidos. Nela chegam comunicados, divulgação de eventos, mensalidade vencendo (3 dias antes), contrato aprovado e evento cancelado.
- **Preferências (LGPD)**: na tela de Avisos, o sócio liga e desliga os avisos no app, o e-mail e o WhatsApp para as **divulgações**. Avisos de cobrança e de contrato chegam sempre. A escolha fica registrada no histórico do cadastro.
- **Comunicados** (Gestão → 📣 Comunicados): título, mensagem e canais (app, e-mail, WhatsApp). O envio vai para a fila do Hangfire e aceita reprocessamento sem repetir mensagens.
  - O relatório mostra quantos receberam por canal e, ao tocar, **quem não recebeu e por quê**: sem e-mail, celular inválido, canal desligado, título inativo ou canal não configurado.
- **Eventos**:
  - Ao **publicar** um evento com "divulgar aos associados", a divulgação sai sozinha.
  - O botão "📣 Divulgar agora" reenvia com um texto livre.
  - Eventos abertos ao público têm "📷 Post para redes sociais": imagem 1080×1080 (PNG, gerada com SkiaSharp) e legenda com hashtags. A legenda vai para a área de transferência e a imagem abre no compartilhamento do celular (Instagram, Facebook, WhatsApp).
- **WhatsApp**: usa a **API oficial da Meta** (WhatsApp Business Cloud API). Para ativar:
  1. Crie um app em https://developers.facebook.com com o produto WhatsApp e vincule o número do clube a uma conta Meta Business.
  2. Crie um **modelo de mensagem** (categoria Marketing ou Utilidade, idioma Português do Brasil) com 3 variáveis no corpo. Exemplo: `Olá, {{1}}! *{{2}}* — {{3}}`. Aguarde a aprovação.
  3. Configure `Comunicacao__WhatsApp__Token` (token permanente de um usuário do sistema), `Comunicacao__WhatsApp__PhoneNumberId` e, se o nome for diferente de `comunicado_clube`, `Comunicacao__WhatsApp__Modelo`.
  - Sem essas chaves, o WhatsApp aparece como "não configurado" e os envios ficam como "não enviado", sem erro.
- **E-mail**: em produção use `Email__Modo=Smtp` (veja a Fase 6). Em desenvolvimento, os e-mails ficam em `App_Data/emails`.

## Testes

```powershell
dotnet test tests/ClubeDaOnca.Domain.Tests
dotnet test tests/ClubeDaOnca.Api.Tests   # sobe um Postgres descartável via Docker
```

## Produção

Configure por variáveis de ambiente (nunca no repositório):
`ConnectionStrings__Default`, `Jwt__Chave` (32+ caracteres), `Carteirinha__ChaveMestra` (32+ caracteres; se for trocada, todos os QR Codes deixam de valer), as variáveis do Asaas (acima) e, no primeiro deploy, `AdminInicial__Email`/`AdminInicial__Senha`.

### App no Google Play

Veja [docs/PLAY_STORE.md](docs/PLAY_STORE.md): chave de upload, geração do `.aab` com `scripts/publicar-android.ps1`, teste interno e formulários do Play Console. A política de privacidade está em [docs/privacidade.html](docs/privacidade.html).
