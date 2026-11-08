using Clube360.Domain;
using Clube360.Contracts;
namespace Clube360.Application;
public interface IRepositorioClubes {
    T Ler<T>(Func<EstadoClubes,T> consulta);
    T Alterar<T>(Func<EstadoClubes,T> operacao);
}
public sealed class GestaoClubes(IRepositorioClubes repo, TimeProvider relogio)
{
    public DateTimeOffset Agora => relogio.GetUtcNow();
    public T Ler<T>(Func<EstadoClubes,T> consulta) => repo.Ler(consulta);
    public Clube SalvarClube(Guid? id, ClubeRequest r) => repo.Alterar(s => {
        var c = new Clube(id ?? Guid.NewGuid(), (r.Nome??"").Trim(), (r.Sigla??"").Trim().ToUpperInvariant(),
            (r.Endereco??"").Trim(), (r.Bairro??"").Trim(), (r.Cidade??"").Trim(), (r.Uf??"").Trim().ToUpperInvariant(),
            (r.Fone??"").Trim(), (r.Email??"").Trim(), r.ValorMensalPadrao);
        c.Validar();
        if (s.Clubes.Any(x => x.Id != c.Id && x.Sigla.Equals(c.Sigla,StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Sigla de clube já cadastrada.");
        if (id is not null && s.Clubes.All(x=>x.Id!=id)) throw new ArgumentException("Clube inexistente.");
        s.Clubes.RemoveAll(x=>x.Id==c.Id); s.Clubes.Add(c); return c;
    });
    public Associado CriarPessoa(Guid clube, AssociadoRequest r) => repo.Alterar(s => {
        ExigirClube(s,clube); var a=MontarPessoa(clube,r); ValidarPessoa(s,a); s.Associados.Add(a); return a;
    });
    public Associado AtualizarPessoa(Guid clube,Guid id,AssociadoRequest r) => repo.Alterar(s=>{
        var atual=s.Associados.SingleOrDefault(a=>a.ClubeId==clube&&a.Id==id)??throw new ArgumentException("Cadastro inexistente.");
        var novo=MontarPessoa(clube,r) with {Id=id,Ativo=r.Ativo};
        if(novo.Categoria is not (Categoria.Associado or Categoria.Fundador)&&s.Associados.Any(a=>a.ClubeId==clube&&a.TitularId==id))
            throw new ArgumentException("Este titular possui dependentes. Reorganize os vínculos antes de mudar sua categoria.");
        ValidarPessoa(s,novo);s.Associados[s.Associados.IndexOf(atual)]=novo;return novo;
    });
    public static Associado MontarPessoa(Guid clube, AssociadoRequest r) {
        if (!Enum.TryParse<Categoria>(r.Categoria,out var categoria) || !Enum.IsDefined(categoria))
            throw new ArgumentException("Categoria inválida.");
        Parentesco? parentesco = null;
        if (!string.IsNullOrEmpty(r.Parentesco)) {
            if (!Enum.TryParse<Parentesco>(r.Parentesco,out var p) || !Enum.IsDefined(p)) throw new ArgumentException("Parentesco inválido.");
            parentesco=p;
        }
        return new(Guid.NewGuid(),clube,(r.Nome??"").Trim(),categoria,r.Isento,r.TitularId,parentesco,
            DocumentoCpf.Normalizar(r.Cpf),(r.Endereco??"").Trim(),(r.Bairro??"").Trim(),(r.Sexo??"").Trim(),(r.EstadoCivil??"").Trim(),
            (r.Cidade??"").Trim(),(r.Uf??"").Trim().ToUpperInvariant(),(r.Fone??"").Trim(),(r.Email??"").Trim(),
            r.DataNascimento is { } d ? DateOnly.FromDateTime(d) : null,r.Interesses??[],r.ValorMensal,r.Ativo);
    }
    public void ValidarPessoa(EstadoClubes s, Associado a) {
        a.Validar();
        if (!DocumentoCpf.Valido(a.Cpf)) throw new ArgumentException("CPF inválido.");
        if (a.DataNascimento is null || a.DataNascimento > DateOnly.FromDateTime(Agora.UtcDateTime))
            throw new ArgumentException("Informe uma data de nascimento válida.");
        if (s.Associados.Any(x=>x.ClubeId==a.ClubeId && x.Cpf==a.Cpf && x.Id!=a.Id))
            throw new ArgumentException("CPF já cadastrado neste clube.");
        if (!string.IsNullOrEmpty(a.Email)&&!System.Net.Mail.MailAddress.TryCreate(a.Email,out _)) throw new ArgumentException("E-mail inválido.");
        if (a.Uf.Length!=0 && a.Uf.Length!=2) throw new ArgumentException("UF inválida.");
        if ((a.Interesses ?? []).Any(x=>!new[]{"pesca","caça","comida","bebida"}.Contains((x??"").ToLowerInvariant())))
            throw new ArgumentException("Interesses permitidos: pesca, caça, comida e bebida.");
        if (a.Categoria==Categoria.Dependente) {
            var titular=Pessoa(s,a.ClubeId,a.TitularId!.Value);
            if (titular.Categoria is not (Categoria.Associado or Categoria.Fundador))
                throw new ArgumentException("Dependente exige titular associado ou fundador.");
        }
    }
    public Espaco CriarEspaco(Guid clube, EspacoRequest r) => repo.Alterar(s =>
    {
        ExigirClube(s, clube);
        var e = PrepararEspaco(new Espaco(Guid.NewGuid(), clube, r.Nome, r.TaxaAluguel), r);
        s.Espacos.Add(e);
        return e;
    });

    public Espaco AtualizarEspaco(Guid clube, Guid id, EspacoRequest r) => repo.Alterar(s =>
    {
        var e = s.Espacos.SingleOrDefault(x => x.ClubeId == clube && x.Id == id)
            ?? throw new ArgumentException("Área social inexistente.");
        var nova = PrepararEspaco(e, r);
        s.Espacos[s.Espacos.IndexOf(e)] = nova;
        return nova;
    });

    private static Espaco PrepararEspaco(Espaco area, EspacoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Nome) || r.TaxaAluguel < 0)
            throw new ArgumentException("Informe o nome da área e uma taxa válida.");

        var tipo = r.Tipo?.Trim() ?? TiposAreaSocial.Identificar(area.Tipo, area.Nome);
        if (tipo is null || !TiposAreaSocial.Todos.Contains(tipo))
            throw new ArgumentException("Selecione um dos seis tipos de área social.");

        return area with
        {
            Nome = r.Nome.Trim(),
            Tipo = tipo,
            TaxaAluguel = TiposAreaSocial.Reservavel(tipo) ? r.TaxaAluguel : 0
        };
    }

    public Evento CriarEvento(Guid clube, EventoRequest r) => repo.Alterar(s=>{
        ExigirClube(s,clube); Periodos.Validar(r.Inicio,r.Fim,Agora);
        if (string.IsNullOrWhiteSpace(r.Nome)||r.ValorInscricao<0 ||
            !Enum.TryParse<PublicoEvento>(r.Publico,out var publico)||!Enum.IsDefined(publico))
            throw new ArgumentException("Nome, público ou valor inválido.");
        if (r.EspacoId is { } espaco) ExigirLivre(s,clube,espaco,r.Inicio,r.Fim);
        var e=new Evento(Guid.NewGuid(),clube,(r.Nome??"").Trim(),(r.Descricao??"").Trim(),r.Inicio.ToUniversalTime(),r.Fim.ToUniversalTime(),publico,r.ValorInscricao,r.EspacoId);
        s.Eventos.Add(e);return e;
    });
    public AgendaItem[] Agenda(Guid clube) => repo.Ler(s=> s.Eventos.Where(e=>e.ClubeId==clube&&e.Ativo)
        .Select(e=>new AgendaItem(e.Id,"Evento",e.Nome,e.EspacoId,e.Inicio,e.Fim))
        .Concat(s.Reservas.Where(r=>r.ClubeId==clube&&r.Situacao==SituacaoReserva.Confirmada)
            .Select(r=>new AgendaItem(r.Id,"Locação","Espaço reservado",r.EspacoId,r.Inicio,r.Fim)))
        .OrderBy(x=>x.Inicio).ToArray());
    public Reserva Reservar(Guid clube, Guid associado, ReservaRequest r) => repo.Alterar(s=>{
        var a=Pessoa(s,clube,associado); Periodos.Validar(r.Inicio,r.Fim,Agora);
        if (a.Categoria is Categoria.Convidado or Categoria.Simpatizante) throw new ArgumentException("Locação disponível aos associados, fundadores e dependentes.");
        if (string.IsNullOrWhiteSpace(r.Finalidade)) throw new ArgumentException("Informe a finalidade.");
        var espaco=ExigirLivre(s,clube,r.EspacoId,r.Inicio,r.Fim);
        if (!TiposAreaSocial.Reservavel(TiposAreaSocial.Identificar(espaco.Tipo, espaco.Nome)))
            throw new ArgumentException("Esta área social não está disponível para reservas.");
        var titular=a.TitularId??a.Id;
        var reserva=new Reserva(Guid.NewGuid(),clube,espaco.Id,a.Id,titular,r.Inicio.ToUniversalTime(),r.Fim.ToUniversalTime(),(r.Finalidade??"").Trim(),espaco.TaxaAluguel);
        s.Reservas.Add(reserva);
        Cobrar(s,clube,titular,a.Id,"Aluguel",reserva.Id,"Locação: "+espaco.Nome,espaco.TaxaAluguel);
        return reserva;
    });
    public Reserva CancelarReserva(Guid clube,Guid id,Guid? solicitante,bool gestor) => repo.Alterar(s=>{
        var r=s.Reservas.SingleOrDefault(x=>x.ClubeId==clube&&x.Id==id)??throw new ArgumentException("Reserva inexistente.");
        if (!gestor&&r.SolicitanteId!=solicitante) throw new UnauthorizedAccessException();
        if (r.Situacao==SituacaoReserva.Cancelada) return r;
        if (!r.PodeCancelar(Agora)) throw new ArgumentException("Cancelamento permitido até 120 horas antes do início.");
        var nova=r with {Situacao=SituacaoReserva.Cancelada};s.Reservas[s.Reservas.IndexOf(r)]=nova;
        foreach(var c in s.Cobrancas.Where(x=>x.ClubeId==clube&&x.Origem=="Aluguel"&&x.OrigemId==id).ToArray())
            s.Cobrancas[s.Cobrancas.IndexOf(c)]=c with {Situacao=c.Situacao==SituacaoCobranca.Paga?SituacaoCobranca.ReembolsoPendente:SituacaoCobranca.Cancelada};
        return nova;
    });
    public Solicitacao CriarSolicitacao(Guid clube,Guid associado,SolicitacaoRequest r) => repo.Alterar(s=>{
        Pessoa(s,clube,associado);
        if (!Enum.TryParse<TipoSolicitacao>(r.Tipo,out var tipo)||!Enum.IsDefined(tipo)||
            string.IsNullOrWhiteSpace(r.Titulo)||string.IsNullOrWhiteSpace(r.Descricao)||(r.Descricao?.Length??0)>5000)
            throw new ArgumentException("Informe tipo, título e descrição com até 5.000 caracteres.");
        var x=new Solicitacao(Guid.NewGuid(),clube,associado,tipo,(r.Titulo??"").Trim(),(r.Descricao??"").Trim(),StatusSolicitacao.Aberta,
            [new(Agora,StatusSolicitacao.Aberta,"Enviada à administração.")]);s.Solicitacoes.Add(x);return x;
    });
    public Solicitacao AtualizarSolicitacao(Guid clube,Guid id,AndamentoRequest r) => repo.Alterar(s=>{
        var x=s.Solicitacoes.SingleOrDefault(x=>x.ClubeId==clube&&x.Id==id)??throw new ArgumentException("Solicitação inexistente.");
        if (!Enum.TryParse<StatusSolicitacao>(r.Status,out var status)||!Enum.IsDefined(status)||
            status==StatusSolicitacao.Aberta||x.Status is StatusSolicitacao.Concluida or StatusSolicitacao.Cancelada||
            string.IsNullOrWhiteSpace(r.Mensagem)) throw new ArgumentException("Transição inválida ou resposta vazia.");
        var historico=x.Historico.ToList();historico.Add(new(Agora,status,(r.Mensagem??"").Trim()));
        var novo=x with {Status=status,Historico=historico};s.Solicitacoes[s.Solicitacoes.IndexOf(x)]=novo;return novo;
    });
    public Convite Convidar(Guid clube,Guid associado,ConviteRequest r) => repo.Alterar(s=>{
        var autor=Pessoa(s,clube,associado);
        if(autor.Categoria is not (Categoria.Associado or Categoria.Fundador))throw new UnauthorizedAccessException();
        var e=EventoAtivo(s,clube,r.EventoId);
        if(e.Fim<=Agora)throw new ArgumentException("Evento encerrado.");
        var pessoa=MontarPessoa(clube,r.Pessoa);
        if(pessoa.Categoria is not (Categoria.Convidado or Categoria.Simpatizante))throw new ArgumentException("Convide como convidado ou simpatizante.");
        var existente=s.Associados.SingleOrDefault(x=>x.ClubeId==clube&&x.Cpf==pessoa.Cpf);
        if(existente is null)ValidarPessoa(s,pessoa);
        else {
            if(!existente.Ativo||existente.Categoria is not (Categoria.Convidado or Categoria.Simpatizante))throw new ArgumentException("Este CPF já possui outra categoria no clube.");
            pessoa=existente;
        }
        if(s.Visitantes.Any(x=>x.EventoId==e.Id&&x.Cpf==pessoa.Cpf))throw new ArgumentException("CPF já inscrito publicamente.");
        if(s.Inscricoes.Any(x=>x.EventoId==e.Id&&x.AssociadoId==pessoa.Id))throw new ArgumentException("Participante já inscrito.");
        if(s.Convites.Any(c=>c.ClubeId==clube&&c.EventoId==e.Id&&c.Pessoa.Cpf==pessoa.Cpf&&c.Status!=StatusConvite.Rejeitado))
            throw new ArgumentException("Já existe convite para este CPF e evento.");
        var c=new Convite(Guid.NewGuid(),clube,e.Id,autor.Id,pessoa);s.Convites.Add(c);return c;
    });
    public Convite DecidirConvite(Guid clube,Guid id,DecisaoRequest r) => repo.Alterar(s=>{
        var c=s.Convites.SingleOrDefault(x=>x.ClubeId==clube&&x.Id==id)??throw new ArgumentException("Convite inexistente.");
        if(c.Status!=StatusConvite.Pendente)throw new ArgumentException("Convite já analisado.");
        if(r.Aprovar) {
            var pessoa=s.Associados.SingleOrDefault(x=>x.ClubeId==clube&&x.Cpf==c.Pessoa.Cpf);
            if(pessoa is null){ValidarPessoa(s,c.Pessoa);pessoa=c.Pessoa;s.Associados.Add(pessoa);}
            else if(!pessoa.Ativo||pessoa.Categoria is not (Categoria.Convidado or Categoria.Simpatizante))throw new ArgumentException("Participante não elegível.");
            if(s.Visitantes.Any(x=>x.EventoId==c.EventoId&&x.Cpf==pessoa.Cpf))throw new ArgumentException("CPF já inscrito publicamente.");
            if(s.Inscricoes.Any(x=>x.EventoId==c.EventoId&&x.AssociadoId==pessoa.Id))throw new ArgumentException("Participante já inscrito.");
            c=c with {Pessoa=pessoa};
            var evento=EventoAtivo(s,clube,c.EventoId);
            if(evento.Fim<=Agora)throw new ArgumentException("Evento encerrado.");
            s.Inscricoes.Add(new(Guid.NewGuid(),clube,evento.Id,c.Pessoa.Id));
            Cobrar(s,clube,c.Pessoa.Id,c.Pessoa.Id,"Evento",evento.Id,"Convite: "+evento.Nome,evento.ValorInscricao);
        }
        var novo=c with {Status=r.Aprovar?StatusConvite.Aprovado:StatusConvite.Rejeitado,Observacao=(r.Observacao??"").Trim()};
        s.Convites[s.Convites.FindIndex(x=>x.Id==c.Id)]=novo;return novo;
    });
    public Inscricao Inscrever(Guid clube,Guid associado,Guid eventoId) => repo.Alterar(s=>{
        var a=Pessoa(s,clube,associado); var e=EventoAtivo(s,clube,eventoId);
        if(e.Fim<=Agora)throw new ArgumentException("Evento encerrado.");
        if(a.Categoria is Categoria.Convidado or Categoria.Simpatizante)throw new ArgumentException("Participação de convidado ou simpatizante exige convite aprovado pela administração.");
        if(!string.IsNullOrEmpty(a.Cpf)&&s.Visitantes.Any(x=>x.EventoId==eventoId&&x.Cpf==a.Cpf))throw new ArgumentException("CPF já inscrito publicamente neste evento.");
        if(s.Inscricoes.Any(x=>x.EventoId==eventoId&&x.AssociadoId==associado))throw new ArgumentException("Inscrição já existente.");
        var i=new Inscricao(Guid.NewGuid(),clube,eventoId,associado);s.Inscricoes.Add(i);
        Cobrar(s,clube,a.TitularId??a.Id,a.Id,"Evento",e.Id,"Inscrição: "+e.Nome,e.ValorInscricao);return i;
    });
    public Visitante InscreverVisitante(Guid eventoId,VisitanteRequest r) => repo.Alterar(s=>{
        var e=s.Eventos.SingleOrDefault(x=>x.Id==eventoId&&x.Ativo&&x.Publico==PublicoEvento.Publico&&x.Fim>Agora)
            ??throw new ArgumentException("Evento não disponível para inscrição pública.");
        if(string.IsNullOrWhiteSpace(r.Nome)||!DocumentoCpf.Valido(r.Cpf)||!System.Net.Mail.MailAddress.TryCreate(r.Email,out _))
            throw new ArgumentException("Informe nome, CPF válido e e-mail.");
        var cpf=DocumentoCpf.Normalizar(r.Cpf);
        if(s.Inscricoes.Any(i=>i.EventoId==eventoId&&s.Associados.Any(a=>a.Id==i.AssociadoId&&a.Cpf==cpf)))throw new ArgumentException("CPF já inscrito neste evento.");
        if(s.Visitantes.Any(v=>v.EventoId==eventoId&&v.Cpf==cpf))throw new ArgumentException("CPF já inscrito neste evento.");
        var v=new Visitante(Guid.NewGuid(),e.ClubeId,e.Id,(r.Nome??"").Trim(),cpf,(r.Email??"").Trim());s.Visitantes.Add(v);
        Cobrar(s,e.ClubeId,v.Id,v.Id,"Evento",e.Id,"Inscrição pública: "+e.Nome,e.ValorInscricao);return v;
    });
    public int GerarMensalidades(Guid clube,MensalidadesRequest r) => repo.Alterar(s=>{
        var c=ExigirClube(s,clube); var competencia=new DateOnly(r.Ano,r.Mes,1).ToString("yyyy-MM");int quantidade=0;
        foreach(var a in s.Associados.Where(x=>x.ClubeId==clube&&x.Ativo&&x.Categoria!=Categoria.Dependente)) {
            var valor=new TabelaMensalidade(c.ValorMensalPadrao).Calcular(a);
            if(valor==0||s.Cobrancas.Any(x=>x.ClubeId==clube&&x.TitularId==a.Id&&x.Origem=="Mensalidade"&&x.Competencia==competencia))continue;
            s.Cobrancas.Add(new(Guid.NewGuid(),clube,a.Id,a.Id,"Mensalidade",a.Id,"Mensalidade "+competencia,valor,r.Vencimento,Competencia:competencia));quantidade++;
        }
        return quantidade;
    });
    public Cobranca Receber(Guid clube,Guid id,string usuario="teste") => repo.Alterar(s=>{
        var c=s.Cobrancas.SingleOrDefault(x=>x.ClubeId==clube&&x.Id==id)??throw new ArgumentException("Cobrança inexistente.");
        if(c.Situacao!=SituacaoCobranca.Aberta)throw new ArgumentException("Somente cobrança aberta pode receber baixa.");
        var novo=c with {Situacao=SituacaoCobranca.Paga,PagoEm=Agora,BaixadoPor=usuario};s.Cobrancas[s.Cobrancas.IndexOf(c)]=novo;return novo;
    });
    private void Cobrar(EstadoClubes s,Guid clube,Guid titular,Guid consumidor,string origem,Guid origemId,string descricao,decimal valor) {
        if(valor>0)s.Cobrancas.Add(new(Guid.NewGuid(),clube,titular,consumidor,origem,origemId,descricao,valor,DateOnly.FromDateTime(Agora.UtcDateTime).AddDays(10)));
    }
    public static Clube ExigirClube(EstadoClubes s,Guid clube) => s.Clubes.SingleOrDefault(x=>x.Id==clube)??throw new ArgumentException("Clube inexistente.");
    public static Associado Pessoa(EstadoClubes s,Guid clube,Guid id) =>
        s.Associados.SingleOrDefault(x=>x.ClubeId==clube&&x.Id==id&&x.Ativo)??throw new ArgumentException("Associado não pertence ao clube ou está inativo.");
    private static Evento EventoAtivo(EstadoClubes s,Guid clube,Guid id) =>
        s.Eventos.SingleOrDefault(x=>x.ClubeId==clube&&x.Id==id&&x.Ativo)??throw new ArgumentException("Evento inexistente.");
    private static Espaco ExigirLivre(EstadoClubes s,Guid clube,Guid espaco,DateTimeOffset inicio,DateTimeOffset fim) {
        var e=s.Espacos.SingleOrDefault(x=>x.ClubeId==clube&&x.Id==espaco&&x.Ativo)??throw new ArgumentException("Espaço inexistente.");
        if(s.Reservas.Any(r=>r.ClubeId==clube&&r.EspacoId==espaco&&r.Situacao==SituacaoReserva.Confirmada&&Periodos.Sobrepoe(inicio,fim,r.Inicio,r.Fim))||
            s.Eventos.Any(r=>r.ClubeId==clube&&r.EspacoId==espaco&&r.Ativo&&Periodos.Sobrepoe(inicio,fim,r.Inicio,r.Fim)))
            throw new ArgumentException("Espaço ocupado no período escolhido.");
        return e;
    }
}
