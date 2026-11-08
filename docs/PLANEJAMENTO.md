# Planejamento — gestão social de clubes

Escopo atualizado a partir das orientações de Vanderlei. Nome do sistema mantido: Clube360. O planejamento anterior está em [histórico 0.2](historico/PLANEJAMENTO_0_2.md).

## Plataforma e clubes

A plataforma cadastra novos clubes. Cada clube administra seus associados e operações. O super administrador gerencia todos os clubes; os demais perfis recebem vínculo e permissões por clube, verificados na API.

Clubes iniciais: **foz 2** e **guaira2**. Cada clube possui ID, nome, sigla, endereço, bairro, cidade, UF, telefone e e-mail. O cadastro inclui a mensalidade padrão definida pela direção. Contatos iniciais permanecem em branco para preenchimento, sem inventar endereços ou CPFs reais.

## Associados e dependentes

Cadastro: ID e clube, nome do associado, CPF, endereço, bairro, sexo, estado civil, cidade, UF, telefone, e-mail, data de nascimento e interesses múltiplos: pesca, caça, comida e bebida. Novos cadastros exigem nome, CPF válido e nascimento; CPF não se repete no mesmo clube. É possível editar e inativar.

| Categoria principal | Voto | Mensalidade nesta versão |
| --- | --- | --- |
| Associado | Sim, se ativo | Padrão do clube ou valor individual |
| Fundador | Sim, se ativo | Normal, com opção de isenção |
| Convidado | Não | Valor individual, se informado |
| Simpatizante | Não, provisoriamente | Valor individual, se informado |

Dependente é representado internamente como categoria vinculada ao titular, além das quatro categorias principais. Possui identificação própria e pode receber acesso ao app. Sem taxa fixa adicional; despesas ficam na conta do titular, identificando quem consumiu. Parentescos previstos: pai, mãe, filho, filha, avô, avó e cônjuge. O limite de 21 anos mencionado anteriormente ainda exige definição precisa antes de bloquear cadastros por idade. Não há regra automática de idade nesta entrega.

Inativar associado impede seu acesso posterior, inclusive com token já emitido. Não se permite converter um titular que tenha dependentes para categoria incompatível com esse vínculo.

## Acessos

| Perfil | Responsabilidade |
| --- | --- |
| SuperAdmin | Todos os clubes, novos clubes e presidentes |
| Presidente | Gestão do próprio clube e liberação de administrador, tesoureiro e acesso de associado |
| Administrador | Associados, agenda, eventos, solicitações, finanças e acessos de associados do próprio clube |
| Tesoureiro | Financeiro web do próprio clube; não gerencia associados, espaços ou reclamações |
| Socio | Próprio cadastro e despesas autorizadas, agenda, reservas, eventos e solicitações |

Perfil de acesso e categoria associativa são conceitos diferentes. Ser fundador não concede acesso administrativo automaticamente. A lista de clubes aparece conforme os vínculos autorizados. O super administrador pode alternar entre clubes. Uma única conta de associado em vários clubes ainda está pendente.

## Eventos

Eventos como almoço do Dia das Mães, jantar do Dia dos Pais, café da tarde e feijoada beneficente têm nome, descrição, início, fim, público, valor e espaço opcional. Períodos podem atravessar dias. A referência 31/10 a 01/11 não ganhou ano presumido nem evento real criado automaticamente.

O público pode ser somente sócios ou aberto ao público. O evento ocupa o espaço selecionado e impede locação sobreposta. Associado ou fundador pode propor convidado ou simpatizante; a inscrição só é efetivada após aprovação administrativa. Recusa e aprovação têm retorno visível. O CPF evita duplicidade entre inscrições de associado, convidado e visitante no mesmo evento.

Eventos públicos aceitam inscrição de visitante com nome, CPF e e-mail, por página pública. Eventos restritos não aceitam inscrição anônima. Não há publicação de dados pessoais na agenda.

Nesta entrega há preço único por evento. Os descontos por vínculo e por clube, pedidos no escopo anterior, continuam no planejamento e ainda não estão implementados.

## Áreas sociais e agendamentos

O Clube360 atende clubes sociais. A direção cadastra cada área pelo nome e pelo tipo no menu **Áreas sociais**.

| Área | Acesso no sistema |
| --- | --- |
| Piscina e academia | Consulta no catálogo |
| Churrasqueira, quadra de esportes, campo de futebol e salão de festas | Reserva por data e horário |

O associado consulta a agenda, escolhe a área, data e hora de início e término e informa a finalidade. A API valida o tipo da área e a disponibilidade ao gravar. Reservas e eventos que ocupem a mesma área em períodos sobrepostos impedem uma nova reserva, inclusive em tentativas concorrentes. Um período que termina exatamente quando outro começa é permitido.

O catálogo da instalação de demonstração inclui os seis tipos. Em clubes já cadastrados, inclua as áreas existentes pelo painel. O antigo “Salão social” é identificado como salão de festas e mantém a agenda. Áreas antigas com outros nomes podem ser classificadas pelo administrador.

Quando houver taxa, a reserva e sua cobrança são gravadas juntas, com o valor cadastrado no momento da reserva. Dependentes, provisoriamente autorizados a reservar, lançam o débito no titular. Convidado e simpatizante não podem reservar nesta etapa.

Cancelamento permitido até cinco dias antes, interpretados como **120 horas exatas**, inclusive o limite. Depois disso é recusado. Se a cobrança está aberta, é cancelada; se já foi paga, fica com reembolso pendente para tratamento pelo financeiro. Não há devolução bancária automática.

## Financeiro

Geração de mensalidades por ano/mês e vencimento escolhido, sem duplicar cobrança da mesma competência e titular. Fundador paga normalmente salvo isenção. Dependente não recebe mensalidade fixa. Cadastro admite valor individual.

Tesoureiro vê contas abertas, recebidas, vencidas e respectivas pessoas. A baixa manual registra usuário e data/hora. Associado acompanha sua situação financeira; titular vê os débitos da família identificados. Dependente vê seus próprios consumos, sem consultar toda a conta do titular.

Aluguéis e inscrições com valor positivo geram débito. O vencimento provisório destas operações é dez dias após o lançamento. Na aprovação de convite, o débito vai para o convidado; o pagador definitivo deve ser confirmado. Nenhuma dessas escolhas provisórias deve ser tratada como regra já aprovada pela diretoria.

A ação de boleto retorna integração indisponível. Banco/provedor, emissão, envio, liquidação e conciliação serão integrados em etapa própria.

## Solicitações e reclamações

O associado registra tipo, título e descrição e acompanha seu protocolo e histórico. A administração atualiza status e resposta. Associados não acessam protocolos de outras pessoas nem respondem como administração. Tesoureiro não recebe acesso a essas mensagens por seu perfil financeiro.

Comunicação interna em massa, pedida anteriormente, permanece no roadmap; solicitações não substituem esse módulo.

## Arquitetura reaproveitada

Domain concentra regras; Application coordena operações; Contracts contém DTOs; Infrastructure persiste; API aplica autenticação e autorização; Web atende gestão e acesso pelo navegador; App MAUI atende associado. A solução principal exclui MAUI para permitir compilar o servidor sem workload Android.

Persistência local JSON com escrita temporária e troca atômica, cópia isolada por operação e bloqueio dentro de um processo. Não é banco multiusuário de produção. Login, hashes, restrição por clube, proteção antiforgery web e logotipo corrigido foram preservados.
