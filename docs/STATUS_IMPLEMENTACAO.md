# Estado da implementação — 0.3.1

## Implementado para validação local

- Clubes iniciais foz 2 e guaira2 e inclusão de novos clubes por super administrador.
- Cadastro e edição de dados de clube, associado, dependente e espaço/taxa.
- Categorias principais associado, fundador, convidado e simpatizante; dependente vinculado ao titular; direito a voto como informação derivada.
- Perfis super administrador, presidente, administrador, tesoureiro e associado, com autorização por clube na API.
- Criação de contas pelo perfil competente, senhas com hash, login web e app e bloqueio do associado inativo.
- Eventos públicos/restritos, inscrição, convite com aprovação e prevenção de duplicidade por CPF.
- Catálogo de áreas sociais com piscina, academia, churrasqueira, quadra de esportes, campo de futebol e salão de festas.
- Agenda para churrasqueira, quadra, campo e salão com data e horário, bloqueio de sobreposição, cobrança quando houver taxa e cancelamento até 120 horas antes.
- Mensalidades por competência, contas vencidas, baixa manual com usuário/data e cancelamento/reembolso pendente.
- Solicitações/reclamações com retorno e histórico do próprio associado.
- Persistência de cadastros, operações e contas criadas em JSON local ou PostgreSQL. O modo PostgreSQL importa os arquivos locais na primeira ativação.

## Verificado neste ambiente

Compilação da solução de servidor/web e ferramentas, sem avisos nem erros. **68 verificações de domínio/aplicação/persistência** e **67 verificações de autorização e fluxo HTTP**, incluindo isolamento entre clubes, perfis, dependentes, cobrança da reserva, conflitos, prazo de cancelamento, aprovações, antiforgery e inativação de acesso. Testes trabalham em pastas temporárias e não modificam contas locais do usuário.

O aplicativo MAUI foi atualizado no código, mas **esta versão Android ainda não foi compilada nem executada neste ambiente**, que não dispõe do SDK Android/workload MAUI. Exige validação no computador e celular do usuário. Os testes HTTP não substituem revisão visual e uso das telas em navegador e aparelho.

## Limitações materiais

Não há emissão/envio de boleto, liquidação automática nem devolução bancária. O botão de boleto mostra a pendência. Baixa de pagamento é manual; não constitui confirmação bancária.

A persistência JSON atende desenvolvimento com uma instância da API. Proteja e faça backup dos dois arquivos juntos. O modo PostgreSQL usa documentos JSONB e gravações transacionais, com importação testada em PostgreSQL local via PGlite. A conexão do Clube360 ao Supabase precisa ser validada na instalação do usuário. Veja [configuração do PostgreSQL](POSTGRESQL.md). Os endpoints de negócio são habilitados somente em Development.

As contas iniciais de teste mantêm credenciais anteriores e vínculos locais. Novas contas têm um clube por vínculo; múltiplos clubes de um associado ainda não estão concluídos. Dados de CPF e contatos dos exemplos históricos permanecem vazios, devendo ser preenchidos ao editar.

Ainda faltam descontos entre clubes, comunicação em massa, módulo de cantina e decisões do [registro de pendências](DECISOES_PENDENTES.md). As taxas de exemplo não foram aprovadas como valores oficiais.
