using Clube360.Domain;
using Clube360.Infrastructure;
using Clube360.Application;
using Clube360.Contracts;
var checks=0;
void Verificar(bool ok,string mensagem){if(!ok)throw new Exception(mensagem);checks++;}
void Falha(Action acao){try{acao();}catch(ArgumentException){checks++;return;}catch(UnauthorizedAccessException){checks++;return;}throw new Exception("Operação inválida foi aceita.");}
var dados=new DadosDemonstracao();var foz=DadosDemonstracao.ClubeLago;var guaira=DadosDemonstracao.ClubeRio;
Verificar(dados.Clubes.Select(c=>c.Nome).SequenceEqual(new[]{"foz 2","guaira2"}),"Nomes dos clubes.");
var titular=dados.Associados.First();var dependente=dados.Associados.Single(x=>x.Categoria==Categoria.Dependente);
var tabela=new TabelaMensalidade(150);
Verificar(titular.DireitoVoto,"Fundador sem voto.");
Verificar((titular with{Categoria=Categoria.Associado}).DireitoVoto,"Associado sem voto.");
Verificar(!(titular with{Categoria=Categoria.Convidado}).DireitoVoto,"Convidado com voto.");
Verificar(!(titular with{Categoria=Categoria.Simpatizante}).DireitoVoto,"Simpatizante com voto.");
Verificar(!dependente.DireitoVoto,"Dependente com voto.");
Verificar(tabela.Calcular(titular)==150,"Mensalidade do fundador.");
Verificar(tabela.Calcular(titular with{Isento=true})==0,"Isenção.");
Verificar(tabela.Calcular(dependente)==0,"Taxa fixa de dependente.");
Verificar(tabela.Calcular(titular with{ValorMensal=175})==175,"Valor individual.");
Verificar(DocumentoCpf.Valido("111.444.777-35")&&!DocumentoCpf.Valido("11111111111"),"Validação CPF.");
var previa=ComposicaoExtrato.Calcular(foz,titular.Id,dados.Associados.Where(a=>a.ClubeId==foz).ToArray(),dados.Despesas.ToArray(),tabela);
Verificar(previa.Total==255&&previa.Despesas.All(x=>x.ConsumidorId==dependente.Id),"Gastos familiares.");
Falha(()=>ComposicaoExtrato.Calcular(foz,titular.Id,dados.Associados.ToArray(),dados.Despesas.ToArray(),tabela));
var pasta=Path.Combine(Path.GetTempPath(),"Clube360-Regras-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(pasta);
try{
    var arquivo=Path.Combine(pasta,"clubes.json");var repo=new RepositorioClubes(arquivo);
    var clock=new Relogio(new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero));var g=new GestaoClubes(repo,clock);
    var estadoInicial=repo.Ler(s=>s.Clubes.Count);Falha(()=>repo.Alterar<int>(s=>{s.Clubes.Clear();throw new ArgumentException("Falha intencional.");}));
    Verificar(repo.Ler(s=>s.Clubes.Count)==estadoInicial,"Rollback em memória.");
    Verificar(new RepositorioClubes(arquivo).Clubes.Count==estadoInicial,"Rollback em disco.");
    var pessoa=g.CriarPessoa(foz,new(){Nome="Pessoa teste",Cpf="11144477735",Categoria="Associado",DataNascimento=new(1980,1,1),Interesses=["pesca","caça"]});
    Verificar(pessoa.Cpf=="11144477735"&&pessoa.DireitoVoto,"Cadastro completo.");
    Falha(()=>g.CriarPessoa(foz,new(){Nome="Duplicado",Cpf=pessoa.Cpf,DataNascimento=new(1980,1,1)}));
    var dep=g.CriarPessoa(foz,new(){Nome="Dependente teste",Cpf="52998224725",Categoria="Dependente",TitularId=pessoa.Id,Parentesco="Mae",DataNascimento=new(1960,1,1)});
    Verificar(dep.TitularId==pessoa.Id,"Vínculo dependente.");
    Falha(()=>g.CriarPessoa(guaira,new(){Nome="Dependente outro clube",Cpf="12345678909",Categoria="Dependente",TitularId=pessoa.Id,Parentesco="Mae",DataNascimento=new(1960,1,1)}));
    var inicio=clock.GetUtcNow().AddDays(10);var fim=inicio.AddHours(4);
    var areasSociais = repo.Ler(s => s.Espacos.Where(e => e.ClubeId == foz).ToArray());
    Verificar(areasSociais.Select(e => e.Tipo).ToHashSet().SetEquals(TiposAreaSocial.Todos),
        "O clube de demonstração deve ter os seis tipos de área.");
    foreach (var tipo in new[] { TiposAreaSocial.Piscina, TiposAreaSocial.Academia })
    {
        var area = areasSociais.Single(e => e.Tipo == tipo);
        Falha(() => g.Reservar(foz, pessoa.Id, new(area.Id, inicio, fim, "Uso social")));
    }
    foreach (var tipo in new[] { TiposAreaSocial.Churrasqueira, TiposAreaSocial.Quadra, TiposAreaSocial.Campo })
    {
        var area = areasSociais.Single(e => e.Tipo == tipo);
        var agendamento = g.Reservar(foz, pessoa.Id, new(area.Id, inicio, fim, "Uso social"));
        Verificar(agendamento.EspacoId == area.Id && agendamento.Valor == 0, "Agendamento gratuito não confirmado.");
        Falha(() => g.Reservar(foz, pessoa.Id, new(area.Id, inicio.AddHours(1), fim, "Horário ocupado")));
    }

    // Simula um salão gravado antes de o cadastro ter o campo Tipo.
    repo.Alterar(s =>
    {
        var indice = s.Espacos.FindIndex(e => e.Id == DadosDemonstracao.SalaoFoz);
        s.Espacos[indice] = s.Espacos[indice] with { Nome = "Salão social", Tipo = null };
        return true;
    });
    var reserva=g.Reservar(foz,dep.Id,new(DadosDemonstracao.SalaoFoz,inicio,fim,"Churrasco"));
    Verificar(reserva.TitularId==pessoa.Id&&reserva.Valor==250,"Responsável da reserva.");
    var cobranca=repo.Ler(s=>s.Cobrancas.Single(c=>c.OrigemId==reserva.Id));
    Verificar(cobranca.TitularId==pessoa.Id&&cobranca.ConsumidorId==dep.Id&&cobranca.Valor==250,"Cobrança automática.");
    Falha(()=>g.Reservar(foz,pessoa.Id,new(DadosDemonstracao.SalaoFoz,inicio.AddHours(1),fim.AddHours(1),"Conflito")));
    Falha(()=>g.Reservar(guaira,dados.Associados.Last().Id,new(DadosDemonstracao.SalaoFoz,inicio,fim,"Outro clube")));
    var adj=g.Reservar(foz,pessoa.Id,new(DadosDemonstracao.SalaoFoz,fim,fim.AddHours(2),"Adjacente"));
    Verificar(adj.Inicio==fim,"Períodos adjacentes devem ser aceitos.");
    g.Receber(foz,cobranca.Id,"usuario-teste");
    Verificar(repo.Ler(s=>s.Cobrancas.Single(x=>x.Id==cobranca.Id).BaixadoPor)=="usuario-teste","Auditoria da baixa.");
    clock.Agora=inicio.AddDays(-5);
    var cancelada=g.CancelarReserva(foz,reserva.Id,dep.Id,false);
    Verificar(cancelada.Situacao==SituacaoReserva.Cancelada,"Cancelamento no limite de 120h.");
    Verificar(repo.Ler(s=>s.Cobrancas.Single(x=>x.Id==cobranca.Id).Situacao)==SituacaoCobranca.ReembolsoPendente,"Reembolso pendente.");
    var nova=g.Reservar(foz,dep.Id,new(DadosDemonstracao.SalaoFoz,inicio,fim,"Nova reserva"));
    clock.Agora=inicio.AddDays(-5).AddTicks(1);
    Falha(()=>g.CancelarReserva(foz,nova.Id,dep.Id,false));
    Falha(()=>g.CancelarReserva(foz,nova.Id,titular.Id,false));
    clock.Agora=new(2026,1,1,0,0,0,TimeSpan.Zero);
    var concorrente=clock.Agora.AddDays(30);
    var resultados=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>{
        try{g.Reservar(foz,pessoa.Id,new(DadosDemonstracao.SalaoFoz,concorrente,concorrente.AddHours(2),"Concorrência"));return true;}
        catch(ArgumentException){return false;}
    })));
    Verificar(resultados.Count(x=>x)==1,"Reserva concorrente duplicada.");
    Verificar(repo.Ler(s=>s.Reservas.Count(x=>x.Inicio==concorrente))==1,"Persistência de reserva concorrente.");
    Falha(()=>g.CriarEvento(foz,new("Conflito evento","",concorrente,concorrente.AddHours(1),"Publico",20,DadosDemonstracao.SalaoFoz)));
    var evento=g.CriarEvento(foz,new("Almoço dia das mães","Teste",clock.Agora.AddDays(40),clock.Agora.AddDays(40).AddHours(3),"SomenteSocios",50));
    var publico=g.CriarEvento(foz,new("Feijoada beneficente","Teste",new(2026,10,31,10,0,0,TimeSpan.FromHours(-3)),new(2026,11,1,15,0,0,TimeSpan.FromHours(-3)),"Publico",30));
    Verificar(publico.Fim.Date>publico.Inicio.Date,"Evento atravessa datas.");
    g.Inscrever(foz,dep.Id,evento.Id);
    Verificar(repo.Ler(s=>s.Cobrancas.Any(c=>c.OrigemId==evento.Id&&c.ConsumidorId==dep.Id&&c.TitularId==pessoa.Id)),"Inscrição do dependente para o titular.");
    Falha(()=>g.Inscrever(foz,dep.Id,evento.Id));
    Falha(()=>g.Convidar(foz,dep.Id,new(evento.Id,new(){Nome="Sem permissão"})));
    var req=new AssociadoRequest{Nome="Convidado teste",Cpf="12345678909",Categoria="Convidado",DataNascimento=new(1990,1,1)};
    var convite=g.Convidar(foz,titular.Id,new(evento.Id,req));
    Verificar(convite.Status==StatusConvite.Pendente,"Convite sem aprovação.");
    Verificar(repo.Ler(s=>s.Associados.All(x=>x.Cpf==""||x.Cpf!=req.Cpf)),"Convidado cadastrado antes da aprovação.");
    var aprovado=g.DecidirConvite(foz,convite.Id,new(true,"Aprovado pela administração."));
    Verificar(aprovado.Status==StatusConvite.Aprovado&&repo.Ler(s=>s.Inscricoes.Any(x=>x.AssociadoId==aprovado.Pessoa.Id)),"Aprovação não inscreveu.");
    Falha(()=>g.DecidirConvite(foz,convite.Id,new(true,"Duplicado")));
    var repetido=g.Convidar(foz,titular.Id,new(publico.Id,req));g.DecidirConvite(foz,repetido.Id,new(true,"Outro evento"));
    Verificar(repo.Ler(s=>s.Associados.Count(x=>x.ClubeId==foz&&x.Cpf==req.Cpf))==1,"Convidado reutilizado duplicou pessoa.");
    Falha(()=>g.InscreverVisitante(evento.Id,new("Visitante","11144477735","teste@example.test")));
    var visitante=g.InscreverVisitante(publico.Id,new("Visitante público","11144477735","teste@example.test"));
    Verificar(visitante.ClubeId==foz,"Inscrição pública no clube errado.");
    Falha(()=>g.Inscrever(foz,pessoa.Id,publico.Id));
    Falha(()=>g.InscreverVisitante(publico.Id,new("Convidado duplicado","12345678909","teste@example.test")));
    Falha(()=>g.InscreverVisitante(publico.Id,new("Duplicado","11144477735","teste@example.test")));
    var ticket=g.CriarSolicitacao(foz,pessoa.Id,new("Reclamacao","Melhoria","Preciso de retorno."));
    Verificar(ticket.Status==StatusSolicitacao.Aberta&&ticket.Historico.Count==1,"Protocolo não aberto.");
    g.AtualizarSolicitacao(foz,ticket.Id,new("EmAnalise","Estamos analisando."));
    var concluida=g.AtualizarSolicitacao(foz,ticket.Id,new("Concluida","Resolvido."));
    Verificar(concluida.Historico.Count==3&&concluida.Status==StatusSolicitacao.Concluida,"Histórico incompleto.");
    Falha(()=>g.AtualizarSolicitacao(foz,ticket.Id,new("EmAnalise","Reabrir")));
    Falha(()=>g.AtualizarSolicitacao(guaira,ticket.Id,new("Concluida","Outro clube")));
    var n=g.GerarMensalidades(foz,new(2027,1,new(2027,1,10)));
    Verificar(n>0,"Mensalidades não geradas.");
    Verificar(g.GerarMensalidades(foz,new(2027,1,new(2027,1,10)))==0,"Mensalidade duplicada.");
    Verificar(repo.Ler(s=>s.Cobrancas.All(c=>c.Origem!="Mensalidade"||c.ConsumidorId!=dep.Id)),"Mensalidade fixa de dependente.");
    Verificar(new RepositorioClubes(arquivo).Ler(s=>s.Reservas.Count)==repo.Ler(s=>s.Reservas.Count),"Dados não persistiram.");
    var raizNova=Path.Combine(pasta,"migracao","src","Clube360.Api");
    var legado=Path.Combine(pasta,"migracao","src","FisgadaPro.Api","App_Data");
    Directory.CreateDirectory(raizNova);Directory.CreateDirectory(legado);
    File.WriteAllText(Path.Combine(legado,"clubes.json"),"dados originais");
    File.WriteAllText(Path.Combine(legado,"contas.json"),"hashes originais");
    var destino=CaminhosDadosLocais.Preparar(raizNova);
    Verificar(File.ReadAllText(Path.Combine(destino,"clubes.json"))=="dados originais","Migração perdeu cadastros.");
    Verificar(File.ReadAllText(Path.Combine(destino,"contas.json"))=="hashes originais","Migração perdeu contas.");
    Verificar(File.Exists(Path.Combine(legado,"contas.json")),"Migração removeu cópia original.");
    File.WriteAllText(Path.Combine(destino,"clubes.json"),"dados atualizados");
    CaminhosDadosLocais.Preparar(raizNova);
    Verificar(File.ReadAllText(Path.Combine(destino,"clubes.json"))=="dados atualizados","Reinício sobrescreveu dados atuais.");
    Console.WriteLine($"{checks} verificações de regras sociais e financeiras concluídas com sucesso.");
}finally{Directory.Delete(pasta,true);}
sealed class Relogio(DateTimeOffset agora):TimeProvider { public DateTimeOffset Agora=agora;public override DateTimeOffset GetUtcNow()=>Agora; }
