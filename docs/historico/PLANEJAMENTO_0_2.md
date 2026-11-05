# Clube360 — Plataforma de Clubes de Pesca

Responsável: Vanderlei Fonseca  
Data de consolidação: 26/10/2026  
Versão: 1.5 — login e separação de acessos  
Atualização: 27/10/2026  
Nome comercial: Clube360  
Status: implementação inicial autorizada; versão 0.2.0 com login sobre demonstração local.

## 1. Objetivo e orientação para continuidade

Criar um sistema em C#, com aplicativo .NET MAUI, para atender vários clubes de pesca. Novos clubes poderão aderir à mesma plataforma. O associado acessará o aplicativo e selecionará o clube ao qual pertence.

Este documento consolida a conversa de planejamento. Requisitos explicitamente solicitados e confirmados estão separados das propostas técnicas, que ainda dependem de validação. Vanderlei autorizou o início da implementação em 27/10/2026 e definiu o Visual Studio Code (azul) como IDE.

## 2. Requisitos confirmados

### 2.1 Plataforma e clubes

- A administração da plataforma é responsável por cadastrar novos clubes.
- A administração de cada clube é responsável pela gestão de seus próprios sócios.
- A solução deve permitir a adesão de outros clubes ao longo do tempo.
- Os associados terão acesso pelo aplicativo e selecionarão seu clube.
- A operação contará com usuários administrativos, operadores, financeiro, manutenção e diretoria.

### 2.2 Categorias de associados

Categorias previstas: sócios fundadores, mensalistas, atletas e familiares.

Regras confirmadas:

- Fundadores pagam mensalidade normalmente; o sistema deve oferecer opção de isenção.
- Atletas têm mensalidade diferenciada, cujo valor é definido pela diretoria do clube.
- Um atleta pode também ser fundador ou mensalista. A condição de atleta deve coexistir com a condição associativa de base.
- Os débitos dos familiares são incluídos na cobrança mensal do titular.
- Gastos dos familiares em cantina e eventos são lançados para o titular, identificando o familiar que realizou cada despesa.
- A responsabilidade financeira do titular e a identificação de quem consumiu são informações distintas.

Não haverá taxa fixa adicional por familiar. Os gastos realizados pelos familiares continuam sendo lançados na cobrança mensal do titular, com identificação de quem fez cada despesa.

Os familiares permitidos, conforme confirmação do usuário, são:

| Parentesco com o titular | Limite de idade |
| --- | --- |
| Pais | Sem limite |
| Filhos | Até 21 anos |
| Avós | Sem limite |
| Cônjuges | Sem limite |

Não tratar todos esses vínculos como parentes de primeiro grau: usar explicitamente a lista acima. Irmãos, netos e outros parentes não foram incluídos. Companheiros não foram expressamente discutidos.

Na resposta anterior, o assistente propôs interpretar o limite como até a véspera do aniversário de 22 anos, com aviso prévio e preservação do histórico. O usuário confirmou o limite de 21 anos, mas ainda não confirmou especificamente essa interpretação operacional. Validar antes da implementação.

### 2.3 Financeiro

- Emissão de cobranças de mensalidades.
- Relatório de inadimplência.
- Envio de cobranças.
- Cobrança de valor predeterminado pela direção para colocar embarcações dos sócios na água.
- Mensalidade de estaleiro para guardar embarcações.
- Oferta de manutenção, reparos e oficina.
- Lançamentos de despesas de cantina, com identificação de quem consumiu.
- Consolidação de débitos dos familiares na cobrança mensal do titular, mantendo a identificação de cada despesa e do familiar responsável pelo consumo.

### 2.4 Eventos compartilhados

- Cada clube poderá promover eventos.
- Associados de outros clubes poderão participar.
- Haverá participação de não associados, com preço sem desconto.
- Sócios do clube organizador terão desconto maior.
- Sócios de outros clubes terão desconto menor.
- Percentuais e valores ainda não foram definidos. Os exemplos anteriores de 30%, 15% e R$ 200,00 eram apenas ilustrativos.

### 2.5 Comunicação

- Comunicação interna entre associados.
- Envio de mensagens em massa para todos os associados ou para os associados de determinado clube.
- O alcance de cada diretoria entre clubes ainda precisa ser definido: não presumir que uma diretoria tenha autorização para acessar ou contatar toda a base de outros clubes.

## 3. Propostas funcionais ainda sujeitas à validação

### Identidade e acesso

Uma conta por pessoa, com vínculos independentes em vários clubes. Seleção apenas entre clubes autorizados. A pessoa poderá exercer funções diferentes em cada clube. Permissões serão separadas da categoria associativa: um fundador poderá também ser diretor ou operador.

Confirmado: a condição de atleta pode coexistir com fundador ou mensalista. Proposta de modelagem: categoria associativa de base e condição adicional de atleta. A prioridade entre isenção de fundador e mensalidade de atleta ainda precisa ser definida.

Separar situação associativa (em análise, ativo, suspenso, desligado) da situação financeira. Não suspender automaticamente um vínculo apenas por existir cobrança vencida sem regra aprovada.

### Administração da plataforma

Além do cadastro confirmado de clubes, propõe-se cadastro do administrador inicial, ativação, acompanhamento de adesões e configurações gerais. O acesso de suporte a dados internos deverá ser restrito e auditado.

### Familiares

Cada familiar terá cadastro individual, data de nascimento quando necessária, parentesco e vínculo com titular no clube. Aprovação e eventual comprovação de parentesco serão definidas pelo clube.

Confirmado: os débitos familiares compõem a cobrança mensal do titular, incluindo cantina e eventos, com identificação de quem realizou a despesa. Não há taxa fixa adicional por familiar. Pendências: acesso ao app por menores e procedimento ao atingir o limite etário. Não presumir exclusão automática do familiar.

### Embarcações e estaleiro

Prever várias embarcações por associado, identificação, características, motor, documentos, fotos e pessoas autorizadas. Administrar vagas, ocupação, contratos de guarda e histórico.

Mensalidade associativa e mensalidade de guarda devem ser obrigações separadas. Definir se o preço varia por porte, vaga, categoria ou embarcação.

### Movimentação de embarcações

Fluxo proposto: solicitação → agendamento, se aplicável → confirmação → execução → cobrança. Registrar embarcação, operador, horários e preço aplicado.

Somente a cobrança por colocação na água foi solicitada expressamente. Cobrança pela retirada, pacotes, gratuidades e limites de atendimento permanecem em aberto.

### Oficina e manutenção

Fluxo proposto: solicitação → avaliação → orçamento → aprovação → execução → conclusão → cobrança. Incluir peças, materiais, mão de obra, responsáveis, fotos e histórico. Validar aprovação pelo aplicativo e autorização de serviços adicionais.

### Financeiro

Prever cobranças recorrentes, recebimentos, pagamentos parciais, cancelamentos, estornos, renegociação e demonstrativo por associado. Juros, multas, descontos e vencimentos dependem das regras aprovadas.

Proposta: cada clube receber diretamente em sua própria conta. Provedores de Pix, boleto, cartão e canais de envio ainda não foram escolhidos. Integrações deverão confirmar pagamentos e evitar duplicação de cobranças ou baixas em reprocessamentos.

### Cantina e conta do titular

Confirmado: registrar gastos de cantina e atribuir ao titular os gastos de seus familiares. Uma gestão completa de cantina, estoque, compras, caixa ou emissão fiscal não foi solicitada; o escopo atual contempla o lançamento da despesa.

Proposta de detalhamento: cada lançamento registra clube, titular devedor, pessoa que consumiu, origem (cantina, evento ou outro serviço), data, descrição e valor. O demonstrativo mensal discrimina mensalidade e demais despesas, preservando seus tipos e a pessoa que as originou. A consolidação não deve transformar consumo em valor de mensalidade associativa.

Ainda definir: corte de faturamento, vencimento dos consumos, tratamento de cancelamentos e despesas realizadas em eventos de outros clubes. Não presumir transferência automática de débitos entre clubes.

### Eventos

Prever regulamento, organizador, local, data, capacidade, período de inscrição, preços, categorias de participante e situação do pagamento.

Propostas: verificar vínculo no momento da inscrição; preservar o preço e a elegibilidade usados; conceder o maior benefício válido a quem possuir vários vínculos; compartilhar somente os dados necessários à validação do desconto. Validar descontos para familiares e inadimplentes, inscrição por equipe ou barco, lista de espera e reembolso.

### Comunicação

Separar comunicados oficiais de interações entre associados. Prever segmentação por clube, categoria e participantes de evento. Confirmar leitura, notificações, anexos, grupos, mensagens privadas e mural são possibilidades ainda não aprovadas individualmente.

Definir moderação, denúncias, bloqueios, participação de menores e autorização para contato entre clubes. Canais externos, como WhatsApp e e-mail, dependem de escolha e integração.

## 4. Matriz inicial de responsabilidades proposta

| Perfil | Responsabilidades |
| --- | --- |
| Administrador da plataforma | Cadastrar clubes; demais funções gerais a validar |
| Administrador do clube | Gerir sócios; configurar usuários e operação do clube |
| Operador | Atendimento e movimentações autorizadas |
| Financeiro | Cobranças, recebimentos e inadimplência |
| Manutenção | Orçamentos e ordens de serviço |
| Diretoria | Indicadores, aprovação de preços e comunicados |
| Associado | Consultas e solicitações próprias, inscrições e comunicação |
| Não associado | Inscrições e pagamentos de eventos; acesso limitado |

Uma pessoa poderá acumular permissões. Descontos excepcionais, cancelamentos, baixas manuais e envios em massa exigirão permissões específicas e histórico.

## 5. Arquitetura técnica proposta

Confirmados: C#, .NET MAUI e Visual Studio Code (azul). A implementação inicial usa .NET 10 e Blazor Interactive Server para demonstração. PostgreSQL, persistência e integrações ainda não foram implementados. A versão 0.2.0 autentica três contas locais de teste, com autorização por perfil/clube e clientes conectados à API.

| Componente | Proposta |
| --- | --- |
| Aplicativo | .NET MAUI; plataformas iniciais a definir |
| Painel administrativo | Blazor para uso pelo navegador |
| API central | ASP.NET Core |
| Persistência | PostgreSQL com Entity Framework Core |
| Organização | Monólito modular |
| Processamento assíncrono | Mensalidades, notificações e integrações |
| Arquivos | Armazenamento privado com autorização por clube |

Separação lógica: domínio, aplicação, infraestrutura, contratos, API, painel web e aplicativo. IDE definida: Visual Studio Code (azul). Base .NET 10, com global.json permitindo SDKs 10.0 de bandas posteriores. Hospedagem e fornecedores serão definidos posteriormente. Autenticação inicial por tokens do ASP.NET Core e cookie web; contas locais com hashes, sem banco de usuários nesta etapa.

### Isolamento entre clubes

Proposta inicial: banco compartilhado com identificação do clube em todos os registros que lhe pertencem. A API deve validar o vínculo e a permissão em cada operação, inclusive relatórios, arquivos e tarefas em segundo plano. A seleção visual do clube não é suficiente como controle de segurança.

Contas de pessoas são globais; vínculos, permissões, mensalidades, embarcações e serviços são administrados no contexto de cada clube. Eventos publicados serão compartilhados conforme visibilidade; cadastros financeiros permanecerão restritos.

Prever auditoria, cópias de segurança, recuperação testada, proteção de dados e controle de acesso de suporte. Detalhes legais e políticas deverão ser analisados na etapa própria, com fontes atuais.

## 6. Entidades conceituais iniciais

Clube; usuário; vínculo com clube; perfil; permissão; categoria associativa; condição de atleta; vínculo familiar; lançamento de consumo com titular e consumidor; embarcação; autorização de uso; vaga; contrato de guarda; movimentação; serviço; orçamento; ordem de serviço; peça/material; cobrança; item de cobrança; pagamento; alocação de pagamento; evento; regra de preço; inscrição; comunicado; mensagem; grupo; registro de leitura; auditoria.

Este é um mapa conceitual, não um esquema de banco aprovado. Definir propriedade e isolamento por clube antes do modelo físico.

## 7. Telas sugeridas

Aplicativo: acesso; seleção de clube; início; cadastro; familiares; embarcações; cobranças e pagamentos; solicitação de movimentação; serviços/orçamentos; eventos; inscrições; comunicação.

Painel: visão geral; clubes no nível da plataforma; associados; familiares; usuários/permissões; preços; estaleiro; movimentações; oficina; financeiro; inadimplência; eventos; comunicação; relatórios; configurações.

## 8. Etapas propostas

1. Fechar regras pendentes e escopo da primeira versão. Nome definido: Clube360.
2. Detalhar permissões, fluxos e telas.
3. Aprovar modelo conceitual, arquitetura e integrações.
4. Mediante autorização para código, implementar fundação multiclube, associados, categorias, familiares, financeiro e comunicados.
5. Implementar embarcações, estaleiro e movimentações.
6. Implementar eventos compartilhados e preços por vínculo.
7. Implementar oficina e ampliar comunicação.

A priorização é uma proposta. O isolamento entre clubes deve existir desde a primeira versão.

## 9. Decisões pendentes para a próxima conversa

- Modelo de adesão dos clubes.
- Preços, benefícios, autorização e vigência de isenções; prioridade entre isenção de fundador e mensalidade de atleta.
- Regra operacional exata do limite de 21 anos e tratamento após o prazo.
- Aceitação de companheiros e documentação familiar; não há taxa fixa adicional, e os débitos são atribuídos ao titular.
- Permissões de menores e responsabilidade do titular.
- Meios de pagamento, bancos, vencimentos, juros e multas; corte dos consumos e tratamento de despesas em outros clubes.
- Efeitos da inadimplência sobre serviços e descontos.
- Preços de estaleiro e movimentação; agendamento e retirada da água.
- Formato dos eventos, percentuais de desconto, cancelamentos e reembolsos.
- Alcance de mensagens em massa e comunicação entre clubes.
- Plataformas móveis além do Android inicial, hospedagem e operação sem conexão. IDE definida: Visual Studio Code (azul); base .NET 10.

## 10. Registro das decisões da conversa

1. Vanderlei solicitou planejamento de sistema multiclube em C# e MAUI, sem código, com associados, perfis internos, financeiro, eventos, embarcações, estaleiro, oficina e comunicação.
2. Foi proposta arquitetura com aplicativo MAUI, painel Blazor e API central, ainda sujeita a aprovação técnica.
3. Vanderlei definiu que a plataforma cadastra os clubes, enquanto cada clube administra seus sócios.
4. Vanderlei definiu categorias: fundador, mensalista, atleta e familiar.
5. Vanderlei incluiu pais, filhos, avós e cônjuges como familiares.
6. Vanderlei confirmou que o limite de 21 anos se aplica aos filhos; os demais vínculos listados não têm limite etário.
7. Vanderlei solicitou guardar o projeto para continuidade e futura criação do código.
8. Em 27/10/2026, Vanderlei definiu o nome do sistema como Clube360.
9. Fundadores pagam mensalidade normalmente, com opção de isenção.
10. Atletas têm mensalidade diferenciada definida pela diretoria e podem também ser fundadores ou mensalistas.
11. Débitos dos familiares, incluindo cantina e eventos, são lançados na cobrança mensal do titular, identificando quem fez a despesa.

12. Confirmado: não haverá taxa fixa adicional por familiar.

13. Em 27/10/2026, Vanderlei autorizou iniciar e escolheu o Visual Studio Code (azul).

14. Em 27/10/2026 foi autorizada a etapa de login e separação entre associado e administração. A implementação inicial usa contas locais de teste, sem mudar as regras associativas.

## 11. Instrução pronta para retomar

“Vamos continuar o projeto Clube360 — Plataforma de Clubes de Pesca. Leia docs/PLANEJAMENTO.md. Preserve as regras confirmadas e diferencie-as das propostas ainda abertas. Vamos primeiro fechar os requisitos e detalhar os fluxos e as telas. Consulte docs/EXECUCAO_VSCODE.md e preserve as regras confirmadas ao continuar a implementação.”

Repositório oficial: https://github.com/vanderleifonseca/Clube360. A documentação inicial foi publicada em 27/10/2026. A versão 0.2.0 adiciona login e autorização à demonstração local; veja docs/STATUS_IMPLEMENTACAO.md.
