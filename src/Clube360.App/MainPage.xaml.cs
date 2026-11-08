using Clube360.Contracts;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace Clube360.App;
public partial class MainPage:ContentPage
{
    private HttpClient? _client;private bool _ocupado;private readonly CultureInfo _cultura=CultureInfo.GetCultureInfo("pt-BR");
    private readonly List<Button> _acoes=[];
    private string Base=>$"/api/v1/clubes/{((ClubeDto)ClubesPicker.SelectedItem).Id}";
    private static string Hora(DateTimeOffset d)=>TimeZoneInfo.ConvertTime(d,TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).ToString("dd/MM/yyyy HH:mm");
    private string Dinheiro(decimal v)=>v.ToString("C",_cultura);
    public MainPage(){InitializeComponent();ApiUrl.Text=Preferences.Default.Get("api_url","");}
    private async Task<T> Get<T>(string path){using var r=await _client!.GetAsync(path);await Validar(r);return(await r.Content.ReadFromJsonAsync<T>())!;}
    private async Task Post(string path,object? body=null){using var req=new HttpRequestMessage(HttpMethod.Post,path);if(body is not null)req.Content=JsonContent.Create(body);using var r=await _client!.SendAsync(req);await Validar(r);}
    private static async Task Validar(HttpResponseMessage r){
        if(r.IsSuccessStatusCode)return;string msg="Não foi possível executar. Confira a conexão e sua sessão.";
        try{var j=await r.Content.ReadFromJsonAsync<JsonElement>();if(j.TryGetProperty("mensagem",out var m))msg=m.GetString()??msg;}catch(JsonException){}
        throw new HttpRequestException(msg,null,r.StatusCode);
    }
    private async Task Executar(Func<Task> acao){
        if(_ocupado)return;_ocupado=true;EntrarBotao.IsEnabled=false;ClubesPicker.IsEnabled=false;SairBotao.IsEnabled=false;
        foreach(var b in _acoes)b.IsEnabled=false;Mensagem.Text="";
        try{await acao();}
        catch(Exception e)when(e is HttpRequestException or TaskCanceledException or JsonException or ArgumentException){Mensagem.Text=e.Message;}
        finally{_ocupado=false;EntrarBotao.IsEnabled=true;ClubesPicker.IsEnabled=true;SairBotao.IsEnabled=true;foreach(var b in _acoes)b.IsEnabled=true;}
    }
    private async void AoEntrar(object? sender,EventArgs e)=>await Executar(async()=>{
        if(!Uri.TryCreate(ApiUrl.Text?.Trim(),UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")||!string.IsNullOrEmpty(uri.UserInfo))throw new ArgumentException("Informe o endereço da API do computador.");
        _client?.Dispose();_client=new(){BaseAddress=new Uri(uri.GetLeftPart(UriPartial.Authority)),Timeout=TimeSpan.FromSeconds(20)};
        using var r=await _client.PostAsJsonAsync("/api/v1/login",new LoginRequest(Email.Text??"",Senha.Text??""));Senha.Text="";
        if(!r.IsSuccessStatusCode)throw new ArgumentException("Confira o e-mail e a senha ou aguarde um minuto.");
        var t=await r.Content.ReadFromJsonAsync<TokenResponse>();_client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",t!.AccessToken);
        var sessao=await Get<SessaoDto>("/api/v1/sessao");
        if(sessao.Perfil!="Socio"){Encerrar();throw new ArgumentException("Use o painel web para administrar o clube.");}
        Preferences.Default.Set("api_url",_client.BaseAddress!.ToString());Nome.Text=sessao.Nome;
        LoginPainel.IsVisible=false;ContaPainel.IsVisible=true;ClubesPicker.ItemsSource=sessao.Clubes.ToList();ClubesPicker.SelectedIndex=0;await MostrarConta();
    });
    private async void AoSelecionarClube(object? sender,EventArgs e){if(!_ocupado&&_client is not null&&ClubesPicker.SelectedItem is ClubeDto)await Executar(MostrarConta);}
    private async void AbrirConta(object? s,EventArgs e){if(_client is not null)await Executar(MostrarConta);}
    private async void AbrirFinanceiro(object? s,EventArgs e){if(_client is not null)await Executar(MostrarFinanceiro);}
    private async void AbrirAreas(object? sender, EventArgs e)
    {
        if (_client is not null)
            await Executar(MostrarAreas);
    }

    private async void AbrirAgenda(object? s,EventArgs e){if(_client is not null)await Executar(MostrarAgenda);}
    private async void AbrirEventos(object? s,EventArgs e){if(_client is not null)await Executar(MostrarEventos);}
    private async void AbrirSolicitacoes(object? s,EventArgs e){if(_client is not null)await Executar(MostrarSolicitacoes);}
    private void AoSair(object? s,EventArgs e){if(!_ocupado)Encerrar();}
    private void Encerrar(){_client?.Dispose();_client=null;Conteudo.Children.Clear();ClubesPicker.ItemsSource=null;Nome.Text="";Senha.Text="";ContaPainel.IsVisible=false;LoginPainel.IsVisible=true;}
    private void Limpar(string titulo){Conteudo.Children.Clear();_acoes.Clear();Texto(titulo,true);}
    private void Texto(string s,bool destaque=false)=>Conteudo.Children.Add(new Label{Text=s,FontAttributes=destaque?FontAttributes.Bold:FontAttributes.None});
    private Entry Campo(string titulo,Keyboard? teclado=null){Texto(titulo);var e=new Entry{Placeholder=titulo,Keyboard=teclado??Keyboard.Default};Conteudo.Children.Add(e);return e;}
    private Picker Escolha(string titulo,IEnumerable<string> itens){Texto(titulo);var p=new Picker{Title=titulo,ItemsSource=itens.ToList()};p.SelectedIndex=0;Conteudo.Children.Add(p);return p;}
    private void Botao(string titulo,Func<Task> acao){var b=new Button{Text=titulo};b.Clicked+=async(_,_)=>await Executar(acao);_acoes.Add(b);Conteudo.Children.Add(b);}
    private async Task MostrarConta(){
        var c=await Get<MeuCadastroDto>(Base+"/meu-cadastro");Limpar("Meu cadastro");
        Texto(c.Associado.Nome+" · "+c.Associado.Categoria);Texto("CPF: "+c.Associado.Cpf);Texto("Direito a voto: "+(c.Associado.DireitoVoto?"Sim":"Não"));
        Texto("Endereço: "+c.Associado.Endereco+" · "+c.Associado.Bairro+" · "+c.Associado.Cidade+"/"+c.Associado.Uf);
        Texto("Interesses: "+string.Join(", ",c.Associado.Interesses??[]));Texto("Dependentes",true);
        foreach(var d in c.Dependentes)Texto(d.Nome+" · sem taxa fixa adicional");
        var x=await Get<ExtratoDto>(Base+"/meu-extrato");Texto("Total em aberto: "+Dinheiro(x.Total),true);
    }
    private async Task MostrarFinanceiro(){
        var f=await Get<FinanceiroDto>(Base+"/financeiro");Limpar("Mensalidades e despesas");
        Texto("Em aberto: "+Dinheiro(f.Aberto)+" · Vencido: "+Dinheiro(f.Vencido),true);
        foreach(var c in f.Cobrancas){
            Texto(c.Descricao+" · "+c.Situacao+(c.Vencida?" (vencida)":""));
            Texto(c.Consumidor+" · "+Dinheiro(c.Valor)+" · vence "+c.Vencimento.ToString("dd/MM/yyyy"));
            if(c.Situacao=="Aberta")Botao("Consultar boleto",async()=>{var b=await Get<BoletoDto>(Base+$"/financeiro/{c.Id}/boleto");await DisplayAlertAsync("Cobrança",b.Mensagem,"Fechar");});
        }
    }
    private static DateTimeOffset Instante(DatePicker data,TimePicker hora){
        if((object?)data.Date is not DateTime d||(object?)hora.Time is not TimeSpan t)throw new ArgumentException("Informe data e horário.");
        var local=DateTime.SpecifyKind(d.Date.Add(t),DateTimeKind.Unspecified);var zona=TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return new(local,zona.GetUtcOffset(local));
    }
    private (DatePicker,TimePicker) DataHora(string nome,int hora){
        Texto(nome);var d=new DatePicker{Date=DateTime.Today.AddDays(7)};var t=new TimePicker{Time=TimeSpan.FromHours(hora)};Conteudo.Children.Add(d);Conteudo.Children.Add(t);return(d,t);
    }
    private async Task MostrarAreas()
    {
        var areas = await Get<List<EspacoDto>>(Base + "/espacos");
        Limpar("Áreas sociais");
        if (areas.Count == 0)
            Texto("Nenhuma área social cadastrada.");

        foreach (var area in areas.OrderBy(a => a.TipoEfetivo).ThenBy(a => a.Nome))
        {
            Texto(area.Nome, true);
            Texto((area.TipoEfetivo ?? "Sem classificação") +
                (area.Reservavel ? " · Com agendamento" : " · Sem agendamento"));
            if (area.Reservavel && area.TaxaAluguel > 0)
                Texto("Taxa de reserva " + Dinheiro(area.TaxaAluguel));
        }

        if (areas.Any(a => a.Reservavel))
            Botao("Abrir agenda", MostrarAgenda);
    }

    private async Task MostrarAgenda(){
        var agenda=await Get<List<AgendaDto>>(Base+"/agenda");var espacos=await Get<List<EspacoDto>>(Base+"/espacos");var reservas=await Get<List<ReservaDto>>(Base+"/reservas");
        Limpar("Agenda de locações e eventos");
        foreach(var a in agenda)Texto(a.Tipo+" · "+a.Nome+" · "+Hora(a.Inicio)+" até "+Hora(a.Fim));
        var reservaveis = espacos.Where(e => e.Reservavel).ToList();
        if (reservaveis.Count > 0)
            MostrarFormularioReserva(reservaveis);
        else
            Texto("O clube ainda não tem áreas para agendamento.");

        Texto("Cancelamento até 120 horas antes do início. Valores pagos exigem análise de reembolso.");
        Texto("Minhas reservas",true);
        foreach(var r in reservas){Texto(Hora(r.Inicio)+" · "+r.Finalidade+" · "+r.Situacao);
            if(r.Situacao=="Confirmada")Botao("Cancelar esta reserva",async()=>{
                if(!await DisplayAlertAsync("Cancelar reserva","Deseja solicitar o cancelamento?","Sim","Não"))return;
                await Post(Base+$"/reservas/{r.Id}/cancelar");await MostrarAgenda();Mensagem.Text="Reserva cancelada.";
            });
        }
    }

    private void MostrarFormularioReserva(List<EspacoDto> espacos)
    {
        Texto("Nova reserva", true);
        var p=new Picker{Title="Espaço",ItemDisplayBinding=new Binding(nameof(EspacoDto.Nome)),ItemsSource=espacos};Conteudo.Children.Add(p);if(espacos.Count>0)p.SelectedIndex=0;
        var finalidade=Campo("Finalidade: festa, churrasco, feijoada...");
        var(di,hi)=DataHora("Início",10);var(df,hf)=DataHora("Término",14);
        Botao("Verificar disponibilidade",async()=>{
            if(p.SelectedItem is not EspacoDto e)throw new ArgumentException("Selecione o espaço.");
            var inicio=Instante(di,hi);var fim=Instante(df,hf);if(fim<=inicio||inicio<=DateTimeOffset.UtcNow)throw new ArgumentException("Informe um período futuro, com término posterior ao início.");
            var atual=await Get<List<AgendaDto>>(Base+"/agenda");
            var ocupado=atual.Any(a=>a.EspacoId==e.Id&&inicio<a.Fim&&a.Inicio<fim);
            await DisplayAlertAsync("Disponibilidade",ocupado?"Período ocupado.":"Sem conflito na agenda consultada. A confirmação final ocorre no servidor.","Fechar");
        });
        Botao("Confirmar reserva", async () =>
        {
            if (p.SelectedItem is not EspacoDto area)
                throw new ArgumentException("Selecione a área.");

            var mensagem = area.TaxaAluguel > 0
                ? "Taxa de " + Dinheiro(area.TaxaAluguel) + " será lançada na conta do titular."
                : "Reserva sem taxa.";
            if (!await DisplayAlertAsync("Confirmar reserva", mensagem, "Confirmar", "Voltar"))
                return;

            await Post(Base + "/reservas", new ReservaRequest(area.Id, Instante(di, hi), Instante(df, hf), finalidade.Text ?? ""));
            await MostrarAgenda();
            Mensagem.Text = "Reserva confirmada.";
        });
    }

    private async Task MostrarEventos(){
        var eventos=await Get<List<EventoDto>>(Base+"/eventos");var convites=await Get<List<ConviteDto>>(Base+"/convites");var me=await Get<MeuCadastroDto>(Base+"/meu-cadastro");
        Limpar("Eventos");
        foreach(var e in eventos){Texto(e.Nome,true);Texto(Hora(e.Inicio)+" até "+Hora(e.Fim)+" · "+e.Publico+" · "+Dinheiro(e.ValorInscricao));
            Botao("Inscrever-me em "+e.Nome,async()=>{await Post(Base+$"/eventos/{e.Id}/inscricoes");Mensagem.Text="Inscrição confirmada.";});
        }
        if(me.Associado.Categoria is "Associado" or "Fundador"){
            Texto("Indicar convidado ou simpatizante",true);
            var ep=new Picker{Title="Evento",ItemDisplayBinding=new Binding(nameof(EventoDto.Nome)),ItemsSource=eventos};Conteudo.Children.Add(ep);if(eventos.Count>0)ep.SelectedIndex=0;
            var nome=Campo("Nome");var cpf=Campo("CPF");var tipo=Escolha("Tipo",["Convidado","Simpatizante"]);
            var endereco=Campo("Endereço");var bairro=Campo("Bairro");var cidade=Campo("Cidade");var uf=Campo("UF");
            var sexo=Escolha("Sexo",["Não informado","Masculino","Feminino","Outro"]);var civil=Campo("Estado civil");var fone=Campo("Telefone",Keyboard.Telephone);var email=Campo("E-mail",Keyboard.Email);
            Texto("Data de nascimento");var nasc=new DatePicker{Date=DateTime.Today.AddYears(-18)};Conteudo.Children.Add(nasc);
            var interesses=new Dictionary<string,CheckBox>();foreach(var i in new[]{"pesca","caça","comida","bebida"}){Texto("Interesse: "+i);var c=new CheckBox();Conteudo.Children.Add(c);interesses[i]=c;}
            Botao("Enviar convite para aprovação",async()=>{
                if(ep.SelectedItem is not EventoDto e)throw new ArgumentException("Selecione o evento.");
                var pessoa=new AssociadoRequest{Nome=nome.Text??"",Cpf=cpf.Text??"",Categoria=tipo.SelectedItem?.ToString()??"Convidado",Endereco=endereco.Text??"",Bairro=bairro.Text??"",Cidade=cidade.Text??"",Uf=uf.Text??"",Sexo=sexo.SelectedItem?.ToString()??"",EstadoCivil=civil.Text??"",Fone=fone.Text??"",Email=email.Text??"",DataNascimento=Convert.ToDateTime(nasc.Date),Interesses=interesses.Where(x=>x.Value.IsChecked).Select(x=>x.Key).ToArray()};
                await Post(Base+"/convites",new ConviteRequest(e.Id,pessoa));await MostrarEventos();Mensagem.Text="Convite enviado. Aguarde aprovação.";
            });
        }
        Texto("Acompanhamento dos convites",true);
        foreach(var c in convites)Texto(c.Pessoa.Nome+" · "+c.Status+" · "+c.Observacao);
    }
    private async Task MostrarSolicitacoes(){
        var itens=await Get<List<SolicitacaoDto>>(Base+"/solicitacoes");Limpar("Solicitações e reclamações");
        var tipo=Escolha("Tipo",["Solicitacao","Reclamacao"]);var titulo=Campo("Título");Texto("Descrição");var descricao=new Editor{AutoSize=EditorAutoSizeOption.TextChanges,HeightRequest=100,MaxLength=5000};Conteudo.Children.Add(descricao);
        Botao("Enviar à administração",async()=>{await Post(Base+"/solicitacoes",new SolicitacaoRequest(tipo.SelectedItem?.ToString()??"Solicitacao",titulo.Text??"",descricao.Text??""));await MostrarSolicitacoes();Mensagem.Text="Enviado à administração.";});
        Texto("Meus protocolos",true);
        foreach(var s in itens){Texto(s.Titulo+" · "+s.Status,true);Texto(s.Descricao);foreach(var h in s.Historico)Texto(Hora(h.Data)+" · "+h.Status+" · "+h.Mensagem);}
    }
}
