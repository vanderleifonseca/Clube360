# Teste guiado — Clube360 0.3

## 1. Atualizar e abrir

Pare os processos anteriores com Ctrl+C e faça backup de dados existentes. No terminal do VS Code:

```powershell
cd C:\maui\FisgadaPro
git pull --ff-only
dotnet build Clube360.slnx
```

Inicie a API e o Web em terminais separados conforme [Execução](EXECUCAO_VSCODE.md). Abra http://localhost:5216/entrar e entre novamente com suas credenciais existentes. Não execute o configurador se já possui contas.

## 2. Plataforma e presidentes

Entre com sua conta de plataforma, agora SuperAdmin. Confirme os clubes **foz 2** e **guaira2**. Preencha os dados reais de contato e ajuste os valores de exemplo antes de qualquer uso efetivo. Em Acessos crie um presidente para cada clube, com e-mails diferentes. É possível cadastrar outro clube na aba Clubes.

Entre como presidente e libere administrador e tesoureiro do próprio clube. Confirme que não gerencia o outro clube. A conta antiga de administrador continua em foz 2.

## 3. Associados

Cadastre pessoas com todos os campos pedidos. CPF e nascimento são obrigatórios para novos registros; os antigos exemplos vazios precisam ser completados quando editados. Teste associado, fundador com mensalidade normal e fundador com isenção. Confirme a informação de voto apenas para associado/fundador ativos.

Cadastre um dependente ligado a titular do mesmo clube e, em Acessos, seu usuário Socio. Dependente não tem mensalidade fixa; seus consumos ficam no titular, identificando o consumidor. Se inativar uma pessoa, sua conta Socio deixa de acessar.

## 4. Áreas sociais e agendamentos

No menu **Áreas sociais**, cadastre ou edite o nome e o tipo. Piscina e academia aparecem no catálogo. Churrasqueira, quadra de esportes, campo de futebol e salão de festas também aparecem na agenda de reservas. Os salões iniciais têm taxas fictícias de R$ 250 e R$ 300. Entre como associado e escolha um período futuro, com mais de cinco dias de antecedência. Reserve e confira o débito no Financeiro se a área tiver taxa.

Tente reservar o mesmo espaço no mesmo período: a API deve recusar. Eventos vinculados ao espaço também bloqueiam a agenda. Cancele dentro do prazo e confira a cobrança cancelada. Para locação paga, o sistema indica reembolso pendente; não devolve dinheiro automaticamente. O limite atual é 120 horas antes do início.

## 5. Eventos e convidados

Cadastre evento SomenteSocios e outro Publico, com início/fim futuros, valor e espaço opcional. É permitido atravessar dias, por exemplo 31/10 a 01/11, escolhendo o ano explicitamente.

Associado/fundador pode cadastrar convidado ou simpatizante para evento. A participação fica pendente; a administração aprova ou recusa, e o associado acompanha. Aprovação gera inscrição sem duplicar CPF. Neste momento o débito é do convidado; confirmar regra definitiva de pagador.

Em http://localhost:5216/eventos-publicos um visitante pode se inscrever em evento público. Evento restrito não aceita essa inscrição. Não há descontos por clube implementados nesta versão.

## 6. Financeiro e solicitações

Como tesoureiro, gere mensalidades por competência e vencimento. Repetir a mesma competência não deve duplicar. Consulte vencidos e faça baixa manual de teste, que registra usuário e data. Como associado, confira suas cobranças e identificação dos gastos de dependentes.

O botão Boleto informa que a integração bancária está pendente. Não existe boleto válido para pagar nesta entrega.

Como associado, envie solicitação/reclamação. Como administração, registre andamento; volte ao associado e confira status e histórico. Outra pessoa não deve acessar esse protocolo.

## 7. Android

Compile e execute pelo comando do [guia Android](EXECUCAO_VSCODE.md#android). Informe o IP do computador na URL da API, na porta 5187; não use o IP/porta ADB. Teste cadastro/extrato, eventos, agenda/reserva/cancelamento e protocolos. Esta etapa de validação nativa ainda precisa ser feita no seu ambiente.

## Dados e limites

Contas e operações locais ficam em arquivos separados na pasta App_Data da API, fora do Git. Preserve e faça backup dos dois com a API parada. Os arquivos são adequados apenas ao teste de uma instância. Banco de produção, cobrança bancária, envio de mensagens e regras ainda provisórias estão em [Pendências](DECISOES_PENDENTES.md).
