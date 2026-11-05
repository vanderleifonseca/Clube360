using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Clube360.Infrastructure;

public sealed class ArmazenamentoPostgres(NpgsqlDataSource fonte)
{
    public const string Clubes = "clubes";
    public const string Contas = "contas";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Inicializar<TClubes, TContas>(Func<TClubes> lerClubes, Func<TContas> lerContas)
    {
        using var conexao = fonte.OpenConnection();
        using var transacao = conexao.BeginTransaction();

        // Serializa a criação do esquema e a primeira importação entre instâncias.
        using (var comando = new NpgsqlCommand("""
            SELECT pg_advisory_xact_lock(360031, 1);
            CREATE SCHEMA IF NOT EXISTS clube360_privado;
            REVOKE ALL ON SCHEMA clube360_privado FROM PUBLIC;
            CREATE TABLE IF NOT EXISTS clube360_privado.documentos (
                chave text PRIMARY KEY CHECK (chave IN ('clubes', 'contas')),
                conteudo jsonb NOT NULL
            );
            REVOKE ALL ON TABLE clube360_privado.documentos FROM PUBLIC;
            ALTER TABLE clube360_privado.documentos ENABLE ROW LEVEL SECURITY;
            """, conexao, transacao))
        {
            comando.ExecuteNonQuery();
        }

        using var contar = new NpgsqlCommand("SELECT count(*) FROM clube360_privado.documentos", conexao, transacao);
        switch ((long)contar.ExecuteScalar()!)
        {
            case 0:
                var clubes = lerClubes();
                var contas = lerContas();
                Inserir(conexao, transacao, Clubes, clubes);
                Inserir(conexao, transacao, Contas, contas);
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("O banco contém uma importação incompleta do Clube360.");
        }

        transacao.Commit();
    }

    public T Ler<T>(string chave)
    {
        using var conexao = fonte.OpenConnection();
        return LerDocumento<T>(conexao, chave, null);
    }

    public TResult Alterar<T, TResult>(string chave, Func<T, TResult> operacao)
    {
        using var conexao = fonte.OpenConnection();
        using var transacao = conexao.BeginTransaction();
        var documento = LerDocumento<T>(conexao, chave, transacao);
        var resultado = operacao(documento);

        using var comando = new NpgsqlCommand(
            "UPDATE clube360_privado.documentos SET conteudo = @conteudo WHERE chave = @chave",
            conexao, transacao);
        comando.Parameters.AddWithValue("chave", chave);
        comando.Parameters.AddWithValue("conteudo", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(documento, Json));
        comando.ExecuteNonQuery();
        transacao.Commit();
        return resultado;
    }

    private static T LerDocumento<T>(NpgsqlConnection conexao, string chave, NpgsqlTransaction? transacao)
    {
        var sql = "SELECT conteudo FROM clube360_privado.documentos WHERE chave = @chave";
        if (transacao is not null)
            sql += " FOR UPDATE";

        using var comando = new NpgsqlCommand(sql, conexao, transacao);
        comando.Parameters.AddWithValue("chave", chave);
        if (comando.ExecuteScalar() is not string conteudo)
            throw new InvalidDataException("Documento do Clube360 ausente no PostgreSQL.");

        return JsonSerializer.Deserialize<T>(conteudo, Json)
            ?? throw new InvalidDataException("Documento do Clube360 inválido no PostgreSQL.");
    }

    private static void Inserir<T>(NpgsqlConnection conexao, NpgsqlTransaction transacao, string chave, T documento)
    {
        using var comando = new NpgsqlCommand(
            "INSERT INTO clube360_privado.documentos (chave, conteudo) VALUES (@chave, @conteudo)",
            conexao, transacao);
        comando.Parameters.AddWithValue("chave", chave);
        comando.Parameters.AddWithValue("conteudo", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(documento, Json));
        comando.ExecuteNonQuery();
    }
}
