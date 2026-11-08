using Microsoft.AspNetCore.Components;
using System.Text.Json;
using Clube360.Contracts;
namespace Clube360.Web.Components.Pages;
public partial class Home
{
    private SessaoDto? _sessao;private Guid _clube;private bool _ocupado;private string? _mensagem;private string _aba="Resumo";
    private List<ClubeDto> _clubes=[];private List<PessoaDto> _pessoas=[];private List<EspacoDto> _espacos=[];private List<EventoDto> _eventos=[];
    private List<AgendaDto> _agenda=[];private List<ReservaDto> _reservas=[];private List<SolicitacaoDto> _solicitacoes=[];private List<ConviteDto> _convites=[];private List<UsuarioDto> _usuarios=[];
    private FinanceiroDto? _financeiro;private MeuCadastroDto? _meuCadastro;
    private Guid? _editarPessoa;
    private ClubeRequest _novoClube=new();private Guid? _editarClube;private AssociadoRequest _novaPessoa=new();private AssociadoRequest _convidado=new(){Categoria="Convidado"};
    private string _nomeEspaco="",_nomeEvento="",_descricaoEvento="",_publico="SomenteSocios",_finalidade="",_resposta="Em análise pela administração.";
    private string _tipoEspaco = "";
    private Guid? _editarEspaco;private decimal _taxa,_valorEvento;private Guid? _espacoReserva,_espacoEvento,_eventoConvite,_pessoaUsuario;
    private DateTime _inicioReserva=DateTime.Today.AddDays(7).AddHours(10),_fimReserva=DateTime.Today.AddDays(7).AddHours(14);
    private DateTime _inicioEvento=DateTime.Today.AddDays(7).AddHours(10),_fimEvento=DateTime.Today.AddDays(7).AddHours(14);
    private string _tipoSolicitacao="Solicitacao",_tituloSolicitacao="",_descricaoSolicitacao="",_nomeUsuario="",_emailUsuario="",_senhaUsuario="",_perfilUsuario="Socio";
    private int _ano=DateTime.Today.Year,_mes=DateTime.Today.Month;private DateTime _vencimento=DateTime.Today.AddDays(10);
    private bool Socio=>_sessao?.Perfil=="Socio";
    private bool Gestor=>_sessao?.Perfil is "Administrador" or "Presidente" or "SuperAdmin";
    private bool Financeiro=>Gestor||_sessao?.Perfil=="Tesoureiro";
    private string Titulo=>_sessao?.Perfil switch{"SuperAdmin"=>"Super administração","Presidente"=>"Presidência do clube","Tesoureiro"=>"Tesouraria","Socio"=>"Área do associado",_=>"Administração do clube"};
    private IEnumerable<string> Abas=>new[]{"Resumo"}.Concat(_sessao?.Perfil is "SuperAdmin" or "Presidente"?new[]{"Clubes"}:[]).Concat(Gestor?new[]{"Associados"}:[])
        .Concat(new[]{"Áreas sociais","Agenda","Eventos"}).Concat(Financeiro||Socio?new[]{"Financeiro"}:[]).Concat(Gestor||Socio?new[]{"Solicitações"}:[]).Concat(Gestor?new[]{"Acessos"}:[]);
    private IEnumerable<string> PerfisPermitidos=>_sessao?.Perfil=="SuperAdmin"?["Presidente","Administrador","Tesoureiro","Socio"]:_sessao?.Perfil=="Presidente"?["Administrador","Tesoureiro","Socio"]:["Socio"];
    private string Base=>$"/api/v1/clubes/{_clube}";
    private static TimeZoneInfo Zona=>TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static DateTimeOffset Instante(DateTime d){d=DateTime.SpecifyKind(d,DateTimeKind.Unspecified);return new(d,Zona.GetUtcOffset(d));}
    private static string Hora(DateTimeOffset d)=>TimeZoneInfo.ConvertTime(d,Zona).ToString("dd/MM/yyyy HH:mm");
    private string NomeEspaco(Guid? id)=>_espacos.FirstOrDefault(e=>e.Id==id)?.Nome??"-";
    protected override async Task OnInitializedAsync()=>await Acao(async()=>{_sessao=await Api.Get<SessaoDto>("/api/v1/sessao");_clubes=await Api.Get<List<ClubeDto>>("/api/v1/clubes");if(_clubes.Count>0)_clube=_clubes[0].Id;await Carregar();});
    private async Task Acao(Func<Task> acao,string? sucesso=null){if(_ocupado)return;_ocupado=true;_mensagem=null;try{await acao();if(sucesso is not null)_mensagem=sucesso;}catch(Exception e)when(e is HttpRequestException or TaskCanceledException or ArgumentException){_mensagem=e.Message;}finally{_ocupado=false;}}
    private async Task Carregar(){
        _pessoas=[];_espacos=[];_eventos=[];_agenda=[];_reservas=[];_solicitacoes=[];_convites=[];_usuarios=[];_financeiro=null;_meuCadastro=null;
        if(_clube==Guid.Empty)return;
        _espacos=await Api.Get<List<EspacoDto>>(Base+"/espacos");_eventos=await Api.Get<List<EventoDto>>(Base+"/eventos");
        _agenda=await Api.Get<List<AgendaDto>>(Base+"/agenda");_reservas=await Api.Get<List<ReservaDto>>(Base+"/reservas");
        if(Gestor){_pessoas=await Api.Get<List<PessoaDto>>(Base+"/associados");_usuarios=await Api.Get<List<UsuarioDto>>(Base+"/usuarios");}
        if(Gestor||Socio){_solicitacoes=await Api.Get<List<SolicitacaoDto>>(Base+"/solicitacoes");_convites=await Api.Get<List<ConviteDto>>(Base+"/convites");}
        if(Financeiro||Socio)_financeiro=await Api.Get<FinanceiroDto>(Base+"/financeiro");
        if(Socio)_meuCadastro=await Api.Get<MeuCadastroDto>(Base+"/meu-cadastro");
    }
    private Task TrocarClube(ChangeEventArgs e)=>Acao(async()=>{if(Guid.TryParse(e.Value?.ToString(),out var id)&&_clubes.Any(c=>c.Id==id)){_clube=id;_editarPessoa=null;_editarClube=null;NovaArea();_novoClube=new();_novaPessoa=new();_convidado=new(){Categoria="Convidado"};_senhaUsuario="";_espacoReserva=null;_espacoEvento=null;_eventoConvite=null;_pessoaUsuario=null;_aba="Resumo";await Carregar();}});
    private void EditarClube(){var c=_clubes.First(x=>x.Id==_clube);_editarClube=c.Id;_novoClube=new(){Nome=c.Nome,Sigla=c.Sigla,Endereco=c.Endereco,Bairro=c.Bairro,Cidade=c.Cidade,Uf=c.Uf,Fone=c.Fone,Email=c.Email,ValorMensalPadrao=c.ValorMensalPadrao};}
    private Task SalvarClube()=>Acao(async()=>{if(_editarClube is {} id)await Api.Put<JsonElement>($"/api/v1/clubes/{id}",_novoClube);else{var c=await Api.Post<ClubeDto>("/api/v1/clubes",_novoClube);_clube=c.Id;}_clubes=await Api.Get<List<ClubeDto>>("/api/v1/clubes");await Carregar();},"Clube salvo.");
    private void EditarPessoa(PessoaDto p){_editarPessoa=p.Id;_novaPessoa=new(){Nome=p.Nome,Cpf=p.Cpf,Categoria=p.Categoria,Isento=p.Isento,TitularId=p.TitularId,Parentesco=p.Parentesco,Endereco=p.Endereco,Bairro=p.Bairro,Cidade=p.Cidade,Uf=p.Uf,Sexo=p.Sexo,EstadoCivil=p.EstadoCivil,Fone=p.Fone,Email=p.Email,DataNascimento=p.DataNascimento?.ToDateTime(TimeOnly.MinValue),Interesses=p.Interesses??[],ValorMensal=p.ValorMensal,Ativo=p.Ativo};}
    private Task CriarPessoa()=>Acao(async()=>{if(_editarPessoa is {} id)await Api.Put<JsonElement>(Base+$"/associados/{id}",_novaPessoa);else await Api.Post<JsonElement>(Base+"/associados",_novaPessoa);_editarPessoa=null;_novaPessoa=new();await Carregar();},"Cadastro salvo.");
    private void NovaArea()
    {
        _editarEspaco = null;
        _nomeEspaco = "";
        _tipoEspaco = "";
        _taxa = 0;
    }

    private void EditarArea(EspacoDto area)
    {
        _editarEspaco = area.Id;
        _nomeEspaco = area.Nome;
        _tipoEspaco = area.TipoEfetivo ?? "";
        _taxa = area.TaxaAluguel;
    }

    private Task SalvarArea() => Acao(async () =>
    {
        if (string.IsNullOrEmpty(_tipoEspaco))
            throw new ArgumentException("Selecione o tipo de área social.");

        var taxa = TiposAreaSocial.Reservavel(_tipoEspaco) ? _taxa : 0;
        var area = new EspacoRequest(_nomeEspaco, taxa, _tipoEspaco);
        if (_editarEspaco is { } id)
            await Api.Put<JsonElement>(Base + $"/espacos/{id}", area);
        else
            await Api.Post<JsonElement>(Base + "/espacos", area);

        NovaArea();
        await Carregar();
    }, "Área social salva.");

    private Task CriarEvento()=>Acao(async()=>{await Api.Post<JsonElement>(Base+"/eventos",new EventoRequest(_nomeEvento,_descricaoEvento,Instante(_inicioEvento),Instante(_fimEvento),_publico,_valorEvento,_espacoEvento));await Carregar();},"Evento cadastrado.");
    private Task VerificarDisponibilidade() => Acao(async () =>
    {
        if (!_espacos.Any(e => e.Id == _espacoReserva && e.Reservavel))
            throw new ArgumentException("Selecione uma área com agendamento.");

        var inicio = Instante(_inicioReserva);
        var fim = Instante(_fimReserva);
        if (fim <= inicio || inicio <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Informe um período futuro, com término posterior ao início.");

        _agenda = await Api.Get<List<AgendaDto>>(Base + "/agenda");
        var ocupado = _agenda.Any(a => a.EspacoId == _espacoReserva && inicio < a.Fim && a.Inicio < fim);
        _mensagem = ocupado
            ? "Período ocupado."
            : "Horário livre na consulta. A disponibilidade será conferida ao confirmar.";
    });

    private Task Reservar()=>Acao(async()=>{if(_espacoReserva is null)throw new ArgumentException("Selecione o espaço.");await Api.Post<JsonElement>(Base+"/reservas",new ReservaRequest(_espacoReserva.Value,Instante(_inicioReserva),Instante(_fimReserva),_finalidade));await Carregar();},"Reserva confirmada.");
    private Task Cancelar(Guid id)=>Acao(async()=>{await Api.Post<JsonElement>(Base+$"/reservas/{id}/cancelar");await Carregar();},"Reserva cancelada. Cobrança cancelada ou encaminhada para reembolso, conforme situação.");
    private Task Inscrever(Guid id)=>Acao(async()=>{await Api.Post<JsonElement>(Base+$"/eventos/{id}/inscricoes");await Carregar();},"Inscrição confirmada.");
    private Task Convidar()=>Acao(async()=>{if(_eventoConvite is null)throw new ArgumentException("Selecione o evento.");await Api.Post<JsonElement>(Base+"/convites",new ConviteRequest(_eventoConvite.Value,_convidado));_convidado=new(){Categoria="Convidado"};await Carregar();},"Convite enviado à administração.");
    private Task Decidir(Guid id,bool aprovar)=>Acao(async()=>{await Api.Post<JsonElement>(Base+$"/convites/{id}/decisao",new DecisaoRequest(aprovar,aprovar?"Participação aprovada.":"Participação não aprovada."));await Carregar();},"Decisão registrada.");
    private Task Solicitar()=>Acao(async()=>{await Api.Post<JsonElement>(Base+"/solicitacoes",new SolicitacaoRequest(_tipoSolicitacao,_tituloSolicitacao,_descricaoSolicitacao));_tituloSolicitacao="";_descricaoSolicitacao="";await Carregar();},"Enviado à administração.");
    private Task Andamento(Guid id,string status)=>Acao(async()=>{await Api.Post<JsonElement>(Base+$"/solicitacoes/{id}/andamentos",new AndamentoRequest(status,_resposta));await Carregar();},"Status atualizado.");
    private Task GerarMensalidades()=>Acao(async()=>{await Api.Post<JsonElement>(Base+"/financeiro/mensalidades",new MensalidadesRequest(_ano,_mes,DateOnly.FromDateTime(_vencimento)));await Carregar();},"Competência processada sem duplicar mensalidades existentes.");
    private Task Receber(Guid id)=>Acao(async()=>{await Api.Post<JsonElement>(Base+$"/financeiro/{id}/receber");await Carregar();},"Baixa manual registrada.");
    private Task Boleto(Guid id)=>Acao(async()=>{var b=await Api.Get<BoletoDto>(Base+$"/financeiro/{id}/boleto");_mensagem=b.Mensagem;});
    private Task CriarUsuario()=>Acao(async()=>{try{await Api.Post<JsonElement>(Base+"/usuarios",new NovoAcessoRequest(_nomeUsuario,_emailUsuario,_senhaUsuario,_perfilUsuario,_pessoaUsuario));await Carregar();}finally{_senhaUsuario="";}},"Acesso criado e liberado.");
}
