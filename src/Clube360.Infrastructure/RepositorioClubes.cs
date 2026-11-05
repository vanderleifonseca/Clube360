using System.Text.Json;
using Clube360.Domain;
using Clube360.Application;
namespace Clube360.Infrastructure;

// Persistência local de desenvolvimento: uma única instância da API, escrita atômica.
public sealed class RepositorioClubes : IRepositorioClubes, IDadosDemonstracao
{
    private readonly object _gate=new();
    private readonly string _arquivo;
    private EstadoClubes _estado;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){WriteIndented=true};
    public RepositorioClubes(string arquivo) {
        _arquivo=Path.GetFullPath(arquivo);Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
        _estado=CarregarEstado(_arquivo);
        if(!File.Exists(_arquivo))Alterar(s=>true);
    }

    public static EstadoClubes CarregarEstado(string arquivo)
    {
        var estado = File.Exists(arquivo)
            ? JsonSerializer.Deserialize<EstadoClubes>(File.ReadAllText(arquivo), Json)
                ?? throw new InvalidDataException("Dados locais inválidos.")
            : DadosDemonstracao.CriarEstado();
        if (estado.Versao != 1)
            throw new InvalidDataException("Versão de dados não suportada.");
        return estado;
    }
    private EstadoClubes Copia()=>JsonSerializer.Deserialize<EstadoClubes>(JsonSerializer.SerializeToUtf8Bytes(_estado,Json),Json)!;
    public T Ler<T>(Func<EstadoClubes,T> consulta){lock(_gate)return consulta(Copia());}
    public T Alterar<T>(Func<EstadoClubes,T> operacao) {
        lock(_gate) {
            var novo=Copia();var resultado=operacao(novo);
            var temporario=_arquivo+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                using(var f=new FileStream(temporario,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                    JsonSerializer.Serialize(f,novo,Json);f.Flush(true);
                }
                File.Move(temporario,_arquivo,true);_estado=novo;
            }finally{if(File.Exists(temporario))File.Delete(temporario);}
            return resultado;
        }
    }
    public IReadOnlyList<Clube> Clubes=>Ler(s=>s.Clubes.ToArray());
    public IReadOnlyList<Associado> Associados=>Ler(s=>s.Associados.ToArray());
    public IReadOnlyList<Despesa> Despesas=>Ler(s=>s.Despesas.ToArray());
    public TabelaMensalidade Tabela(Guid clubeId)=>Ler(s=>new TabelaMensalidade(GestaoClubes.ExigirClube(s,clubeId).ValorMensalPadrao));
}
