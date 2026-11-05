# Clube360 — Projeto Clubes

Repositório oficial: https://github.com/vanderleifonseca/Clube360.

Versão 0.3.1 de desenvolvimento, em C#/.NET 10: API ASP.NET Core, painel Blazor e aplicativo .NET MAUI Android. Desenvolvido para uso no Visual Studio Code (azul).

A nova etapa adapta o projeto para gestão social de vários clubes. Os clubes iniciais são **foz 2** e **guaira2**. Reaproveita autenticação, isolamento por clube, camadas de domínio, extrato familiar e identidade visual.

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
- Super administrador, presidente, administrador do clube, tesoureiro e associado.
- Eventos públicos ou restritos, inscrição e aprovação de convidados/simpatizantes.
- Agenda compartilhada de eventos e locações, controle de conflito e cobrança automática da reserva.
- Mensalidades por competência, inadimplência e baixa manual auditada.
- Solicitações e reclamações com histórico e acompanhamento pelo associado.
- Dados locais persistidos em JSON, separados por clube nas autorizações.

**Boleto bancário não está integrado.** A opção informa essa pendência e não gera documentos ou códigos bancários fictícios. JSON é uma solução de desenvolvimento para uma única instância, ainda sem banco de produção. Os endpoints de negócio só ficam habilitados em Development.

## Verificação

```powershell
dotnet run --project tests/Clube360.Domain.Checks
dotnet run --project tests/Clube360.Access.Checks
```

São 59 verificações de regras e 64 de acesso/fluxo HTTP. Os testes usam dados e credenciais temporários. O Android compila separadamente e ainda precisa de validação local desta atualização.

## Documentação

- [Roteiro de teste e atualização](docs/TESTE_0_3.md)
- [Requisitos e regras](docs/PLANEJAMENTO.md)
- [Permissões e contas](docs/LOGIN_E_ACESSOS.md)
- [Execução no VS Code e Android](docs/EXECUCAO_VSCODE.md)
- [Estado e limitações](docs/STATUS_IMPLEMENTACAO.md)
- [Decisões pendentes](docs/DECISOES_PENDENTES.md)
- [Próximas etapas](docs/ROADMAP.md)
- [Histórico](CHANGELOG.md)

Responsável: Vanderlei Fonseca.
