namespace ClubeDaOnca.Domain.Seguranca;

/// <summary>Perfis de acesso (roles) do sistema.</summary>
public static class Perfis
{
    public const string Titular = "Titular";
    public const string Dependente = "Dependente";
    public const string Portaria = "Portaria";
    public const string Secretaria = "Secretaria";
    public const string Admin = "Admin";

    /// <summary>Administra a rede: cadastra associações (unidades) e seus administradores.</summary>
    public const string SuperAdmin = "SuperAdmin";

    public static readonly IReadOnlyList<string> Todos = [Titular, Dependente, Portaria, Secretaria, Admin, SuperAdmin];

    /// <summary>Perfis que enxergam a área de gestão.</summary>
    public static readonly IReadOnlyList<string> Gestao = [Portaria, Secretaria, Admin];
}
