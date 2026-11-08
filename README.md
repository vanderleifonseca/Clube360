# Clube360 — Projeto Clubes

Repositório oficial: https://github.com/vanderleifonseca/Clube360.

Versão 0.3.1 de desenvolvimento, em C#/.NET 10: API ASP.NET Core, painel Blazor e aplicativo .NET MAUI Android. Desenvolvido para uso no Visual Studio Code (azul).

A nova etapa adapta o projeto para gestão de clubes sociais. Os clubes iniciais são **foz 2** e **guaira2**. Reaproveita autenticação, isolamento por clube, camadas de domínio, extrato familiar e identidade visual.

Veja também [a mudança para Clube360](docs/RENOMEACAO_CLUBE360.md), incluindo novos comandos e preservação dos dados.

## Atualizar e executar

Siga [Teste da versão 0.3](docs/TESTE_0_3.md). Para quem já configurou suas contas, preserve os user-secrets e não execute novamente o configurador.

```powershell
git pull --ff-only
dotnet build Clube360.slnx
```

Em dois terminais separados:

```powershell
dotnet run --project src/Clube360.Api --launch-profile http
```

```powershell
dotnet run --project src/Clube360.Web --launch-profile http
```

Painel: http://localhost:5216/entrar. Eventos públicos: http://localhost:5216/eventos-publicos. API: http://localhost:5187/api/v1/status.

Somente para uma instalação sem contas configuradas:

```powershell
dotnet run --project tools/Clube360.Setup
```

Não há senha padrão; a senha mínima de quatro caracteres permanece apenas nesta etapa local, conforme solicitado.

## O que esta etapa oferece

- Cadastro e edição de clubes, associados e dependentes; categorias e direito a voto.
- Super administrador, presidente, administrador do clube, tesoureiro, porteiro e associado.
- Portaria web para registrar entradas e saídas de associados, fundadores e dependentes ativos, com histórico diário e identificação dos responsáveis.
- Eventos públicos ou restritos, inscrição e aprovação de convidados/simpatizantes.
- Catálogo com piscina, academia, churrasqueira, quadra de esportes, campo de futebol e salão de festas.
- Agendamento de churrasqueira, quadra, campo e salão por data e horário, com controle de conflitos e cobrança quando houver taxa.
- Mensalidades por competência, inadimplência e baixa manual auditada.
- Solicitações e reclamações com histórico e acompanhamento pelo associado.
- Persistência local em JSON ou PostgreSQL, com isolamento por clube nas autorizações.

**Boleto bancário não está integrado.** A opção informa essa pendência e não gera documentos ou códigos bancários fictícios. JSON é o padrão de desenvolvimento. A API também pode usar PostgreSQL, com importação inicial dos arquivos locais. Os endpoints de negócio só ficam habilitados em Development.

## Portaria e importação de associados

No painel web, a administração do clube pode abrir **Portaria** ou criar uma conta com o perfil **Porteiro** em **Acessos**. O porteiro só opera no clube da sua conta. A consulta lista entradas em aberto e permite selecionar o dia do histórico no fuso de Brasília. A elegibilidade usa a situação do cadastro e o vínculo do dependente. Débitos financeiros são tratados no módulo Financeiro.

Com a API local rodando na porta 5187, uma conta de gestão pode importar um CSV de associados e criar seus acessos. Use a conta de super administrador para um lote com vários clubes. Guarde o CSV em uma pasta fora do projeto.

```powershell
dotnet run --project tools/Clube360.Setup -- --importar-associados "C:\caminho\associados.csv"
```

O arquivo UTF-8 usa o cabeçalho `Clube;Nome;Cpf;DataNascimento;Email` e datas no formato `AAAA-MM-DD`. O comando pede as credenciais da gestão no terminal e grava as senhas iniciais geradas em um arquivo `.acessos.csv` ao lado do lote. Guarde esse arquivo em local privado. Se a execução for interrompida, execute novamente com o mesmo CSV e mantenha o arquivo de acessos junto dele.

## Verificação

```powershell
dotnet run --project tests/Clube360.Domain.Checks
dotnet run --project tests/Clube360.Access.Checks
```

São 68 verificações de regras e 91 de acesso/fluxo HTTP. Os testes usam dados e credenciais temporários. O Android compila separadamente e ainda precisa de validação local desta atualização.

## Documentação

- [Roteiro de teste e atualização](docs/TESTE_0_3.md)
- [Requisitos e regras](docs/PLANEJAMENTO.md)
- [Permissões e contas](docs/LOGIN_E_ACESSOS.md)
- [PostgreSQL no Supabase](docs/POSTGRESQL.md)
- [Execução no VS Code e Android](docs/EXECUCAO_VSCODE.md)
- [Estado e limitações](docs/STATUS_IMPLEMENTACAO.md)
- [Decisões pendentes](docs/DECISOES_PENDENTES.md)
- [Próximas etapas](docs/ROADMAP.md)
- [Histórico](CHANGELOG.md)

Responsável: Vanderlei Fonseca.
