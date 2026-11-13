using ClubeDaOnca.Domain.Associacoes;
using ClubeDaOnca.Domain.Auditoria;
using ClubeDaOnca.Domain.Comunicacao;
using ClubeDaOnca.Domain.Contratos;
using ClubeDaOnca.Domain.Eventos;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ClubeDaOnca.Domain.Financeiro;
using ClubeDaOnca.Domain.Reservas;
using ClubeDaOnca.Domain.Socios;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubeDaOnca.Api.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, TenantAtual tenant, UsuarioAtual usuarioAtual)
    : IdentityDbContext<Usuario>(options), IDataProtectionKeyContext
{
    /// <summary>Chaves da proteção de dados (criptografia das chaves do Asaas), compartilhadas entre reinícios e servidores.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // Lidos a cada consulta pelos filtros globais (o EF parametriza membros do próprio DbContext).
    private Guid? AssociacaoAtualId => tenant.AssociacaoId;
    private bool FiltroDesligado => tenant.SemFiltro;

    public TenantAtual Tenant => tenant;

    public DbSet<Associacao> Associacoes => Set<Associacao>();
    public DbSet<MandatoPresidente> Mandatos => Set<MandatoPresidente>();
    public DbSet<Diretor> Diretores => Set<Diretor>();
    public DbSet<HistoricoAlteracao> Historico => Set<HistoricoAlteracao>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<EventoLocal> EventoLocais => Set<EventoLocal>();
    public DbSet<AmbienteEvento> AmbientesEvento => Set<AmbienteEvento>();
    public DbSet<ReservaEvento> ReservasEvento => Set<ReservaEvento>();
    public DbSet<Aviso> Avisos => Set<Aviso>();
    public DbSet<Divulgacao> Divulgacoes => Set<Divulgacao>();
    public DbSet<EnvioDivulgacao> EnviosDivulgacao => Set<EnvioDivulgacao>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Socio> Socios => Set<Socio>();
    public DbSet<Dependente> Dependentes => Set<Dependente>();
    public DbSet<RegistroAcesso> RegistrosAcesso => Set<RegistroAcesso>();
    public DbSet<Espaco> Espacos => Set<Espaco>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<Cobranca> Cobrancas => Set<Cobranca>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
    public DbSet<ConfiguracaoFinanceira> ConfiguracoesFinanceiras => Set<ConfiguracaoFinanceira>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Usuario>(u =>
        {
            u.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            u.Property(x => x.Cpf).HasMaxLength(11);
            u.HasIndex(x => x.Cpf).IsUnique();
            u.HasOne<Associacao>().WithMany().HasForeignKey(x => x.AssociacaoId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Associacao>(a =>
        {
            a.ToTable("Associacoes");
            a.HasKey(x => x.Id);
            a.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            a.HasIndex(x => x.Nome).IsUnique();
            a.Property(x => x.Cnpj).HasMaxLength(14);
            a.HasIndex(x => x.Cnpj).IsUnique().HasFilter("\"Cnpj\" IS NOT NULL");
            a.Property(x => x.Email).HasMaxLength(256);
            a.Property(x => x.Telefone).HasMaxLength(20);
            a.Property(x => x.AsaasApiKeyProtegida).HasMaxLength(2000);
            a.Property(x => x.AsaasWebhookTokenProtegido).HasMaxLength(2000);
            a.OwnsOne(x => x.Endereco, ConfigurarEndereco);
            a.Navigation(x => x.Endereco).IsRequired();
            a.HasMany(x => x.Mandatos).WithOne().HasForeignKey(m => m.AssociacaoId).OnDelete(DeleteBehavior.Cascade);
            a.HasMany(x => x.Diretores).WithOne().HasForeignKey(d => d.AssociacaoId).OnDelete(DeleteBehavior.Cascade);
            a.HasData(new { Id = Associacao.PadraoId, Nome = "Clube da Onça", Ativa = true,
                CriadoEm = new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero) });
            a.OwnsOne(x => x.Endereco).HasData(new { AssociacaoId = Associacao.PadraoId });
        });

        builder.Entity<MandatoPresidente>(m =>
        {
            m.ToTable("MandatosPresidente");
            m.HasKey(x => x.Id);
            m.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            m.Property(x => x.Cpf).HasMaxLength(11);
            m.Property(x => x.Email).HasMaxLength(256);
            m.HasIndex(x => new { x.AssociacaoId, x.Inicio });
        });

        builder.Entity<Diretor>(d =>
        {
            d.ToTable("Diretores");
            d.HasKey(x => x.Id);
            d.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            d.Property(x => x.Cargo).HasMaxLength(80).IsRequired();
            d.Property(x => x.Email).HasMaxLength(256);
            d.Property(x => x.Telefone).HasMaxLength(20);
        });

        builder.Entity<RefreshToken>(r =>
        {
            r.HasKey(x => x.Id);
            r.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            r.HasIndex(x => x.TokenHash).IsUnique();
            r.Ignore(x => x.Ativo);
            r.HasOne(x => x.Usuario).WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        // Número do título: sequencial gerado pelo banco, começando em 1000.
        builder.HasSequence<int>("numero_titulo").StartsAt(1000);

        builder.Entity<Socio>(s =>
        {
            s.ToTable("Socios");
            s.HasKey(x => x.Id);
            s.Property(x => x.NumeroTitulo).HasDefaultValueSql("nextval('numero_titulo')");
            s.HasIndex(x => x.NumeroTitulo).IsUnique();
            s.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            s.Property(x => x.Cpf).HasMaxLength(11).IsRequired();
            s.HasIndex(x => new { x.AssociacaoId, x.Cpf }).IsUnique();
            s.Property(x => x.Email).HasMaxLength(256);
            s.Property(x => x.Telefone).HasMaxLength(20);
            s.Property(x => x.Categoria).HasConversion<string>().HasMaxLength(20);
            s.Property(x => x.Genero).HasConversion<string>().HasMaxLength(20);
            s.Property(x => x.Profissao).HasMaxLength(100);
            s.Property(x => x.Nacionalidade).HasMaxLength(60);
            s.Property(x => x.EstadoCivil).HasConversion<string>().HasMaxLength(20);
            s.Property(x => x.Rg).HasMaxLength(30);
            s.OwnsOne(x => x.Endereco, ConfigurarEndereco);
            s.Navigation(x => x.Endereco).IsRequired();
            s.Property(x => x.Situacao).HasConversion<string>().HasMaxLength(20);
            s.HasIndex(x => x.UsuarioId).IsUnique();
            s.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.SetNull);
            s.HasMany(x => x.Dependentes).WithOne(d => d.Socio!)
                .HasForeignKey(d => d.SocioId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Dependente>(d =>
        {
            d.ToTable("Dependentes");
            d.HasKey(x => x.Id);
            d.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            d.Property(x => x.Cpf).HasMaxLength(11);
            d.HasIndex(x => new { x.AssociacaoId, x.Cpf }).IsUnique();
            d.Property(x => x.Parentesco).HasConversion<string>().HasMaxLength(20);
            d.Property(x => x.Genero).HasConversion<string>().HasMaxLength(20);
            d.Property(x => x.Email).HasMaxLength(256);
            d.Property(x => x.Telefone).HasMaxLength(20);
            d.Property(x => x.Profissao).HasMaxLength(100);
        });

        builder.Entity<HistoricoAlteracao>(h =>
        {
            h.ToTable("HistoricoAlteracoes");
            h.HasKey(x => x.Id);
            h.Property(x => x.Entidade).HasMaxLength(30).IsRequired();
            h.Property(x => x.Descricao).HasMaxLength(200);
            h.Property(x => x.Acao).HasConversion<string>().HasMaxLength(20);
            h.Property(x => x.Campo).HasMaxLength(60);
            h.Property(x => x.ValorAnterior).HasMaxLength(500);
            h.Property(x => x.ValorNovo).HasMaxLength(500);
            h.Property(x => x.UsuarioId).HasMaxLength(450);
            h.Property(x => x.UsuarioNome).HasMaxLength(150).IsRequired();
            h.HasIndex(x => new { x.AssociacaoId, x.RaizId, x.Em });
        });

        builder.Entity<Contrato>(c =>
        {
            c.ToTable("Contratos");
            c.HasKey(x => x.Id);
            c.Property(x => x.Numero).HasMaxLength(20).IsRequired();
            c.HasIndex(x => new { x.AssociacaoId, x.Ano, x.Sequencial }).IsUnique();
            c.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            c.Property(x => x.Categoria).HasConversion<string>().HasMaxLength(20);
            c.Property(x => x.ValorMensalidade).HasPrecision(10, 2);
            c.Property(x => x.EnviadoPara).HasMaxLength(256);
            c.Property(x => x.TokenAceiteHash).HasMaxLength(64);
            c.HasIndex(x => x.TokenAceiteHash).IsUnique().HasFilter("\"TokenAceiteHash\" IS NOT NULL");
            c.Property(x => x.AceiteIp).HasMaxLength(45);
            c.Property(x => x.AprovadoPor).HasMaxLength(150);
            c.Property(x => x.AprovadoPorUsuarioId).HasMaxLength(450);
            c.Property(x => x.MotivoReprovacao).HasMaxLength(500);
            c.Property(x => x.MotivoDesligamento).HasMaxLength(500);
            c.Property(x => x.DocumentoTipo).HasMaxLength(10);
            c.Ignore(x => x.EmAberto);
            c.HasIndex(x => new { x.SocioId, x.CriadoEm });
            c.HasIndex(x => new { x.Status, x.VigenciaFim });
            c.HasOne(x => x.Socio).WithMany().HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Restrict);
            c.HasOne<Contrato>().WithMany().HasForeignKey(x => x.ContratoAnteriorId).OnDelete(DeleteBehavior.Restrict);

            // Dependentes copiados no envio: guardados em JSON (retrato do momento da assinatura).
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            c.Property(x => x.Dependentes)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, json),
                    v => JsonSerializer.Deserialize<List<DependenteContrato>>(v, json) ?? new List<DependenteContrato>(),
                    new ValueComparer<List<DependenteContrato>>(
                        (a, b) => JsonSerializer.Serialize(a, json) == JsonSerializer.Serialize(b, json),
                        v => JsonSerializer.Serialize(v, json).GetHashCode(),
                        v => v.ToList()));
        });

        builder.Entity<RegistroAcesso>(r =>
        {
            r.ToTable("RegistrosAcesso");
            r.HasKey(x => x.Id);
            r.Property(x => x.Nome).HasMaxLength(150);
            r.Property(x => x.Motivo).HasMaxLength(100).IsRequired();
            r.Property(x => x.OperadorId).HasMaxLength(450).IsRequired();
            r.HasIndex(x => x.Em);
        });

        builder.Entity<Espaco>(e =>
        {
            e.ToTable("Espacos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.AssociacaoId, x.Nome }).IsUnique();
            e.Property(x => x.Descricao).HasMaxLength(500);
            e.Property(x => x.Taxa).HasPrecision(10, 2);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
        });

        builder.Entity<Evento>(e =>
        {
            e.ToTable("Eventos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Titulo).HasMaxLength(150).IsRequired();
            e.Property(x => x.Descricao).HasMaxLength(2000);
            // Data e hora no fuso do clube (sem conversão para UTC), como as reservas.
            e.Property(x => x.Inicio).HasColumnType("timestamp without time zone");
            e.Property(x => x.Termino).HasColumnType("timestamp without time zone");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.MotivoCancelamento).HasMaxLength(500);
            e.Ignore(x => x.OcupaAgenda);
            e.HasIndex(x => new { x.Status, x.Inicio });
            e.HasMany(x => x.Locais).WithOne().HasForeignKey(l => l.EventoId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Ambientes).WithOne().HasForeignKey(a => a.EventoId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Aviso>(a =>
        {
            a.ToTable("Avisos");
            a.HasKey(x => x.Id);
            a.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            a.Property(x => x.Titulo).HasMaxLength(150).IsRequired();
            a.Property(x => x.Mensagem).HasMaxLength(2000).IsRequired();
            a.HasIndex(x => new { x.SocioId, x.CriadoEm });
            a.HasIndex(x => x.ReferenciaId);
            a.HasOne<Socio>().WithMany().HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Divulgacao>(d =>
        {
            d.ToTable("Divulgacoes");
            d.HasKey(x => x.Id);
            d.Property(x => x.Titulo).HasMaxLength(150).IsRequired();
            d.Property(x => x.Mensagem).HasMaxLength(2000).IsRequired();
            d.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            d.Property(x => x.CriadoPor).HasMaxLength(150);
            // Canais como texto separado por vírgula ("App,Email,WhatsApp").
            d.Property(x => x.Canais).HasMaxLength(60).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<CanalComunicacao>).ToList(),
                new ValueComparer<List<CanalComunicacao>>((a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, c) => h * 31 + (int)c), v => v.ToList()));
            d.HasMany(x => x.Envios).WithOne().HasForeignKey(e => e.DivulgacaoId).OnDelete(DeleteBehavior.Cascade);
            d.HasIndex(x => x.CriadoEm);
        });

        builder.Entity<EnvioDivulgacao>(e =>
        {
            e.ToTable("EnviosDivulgacao");
            e.HasKey(x => x.Id);
            e.Property(x => x.Canal).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Detalhe).HasMaxLength(300);
            e.HasIndex(x => new { x.DivulgacaoId, x.Status });
        });

        builder.Entity<EventoLocal>(l =>
        {
            l.ToTable("EventoLocais");
            l.HasKey(x => new { x.EventoId, x.EspacoId });
            l.HasOne(x => x.Espaco).WithMany().HasForeignKey(x => x.EspacoId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AmbienteEvento>(a =>
        {
            a.ToTable("AmbientesEvento");
            a.HasKey(x => x.Id);
            a.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            a.Property(x => x.Valor).HasPrecision(10, 2);
        });

        builder.Entity<ReservaEvento>(r =>
        {
            r.ToTable("ReservasEvento");
            r.HasKey(x => x.Id);
            r.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            r.Property(x => x.Valor).HasPrecision(10, 2);
            r.Property(x => x.Observacao).HasMaxLength(300);
            r.HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
            r.HasOne(x => x.Ambiente).WithMany().HasForeignKey(x => x.AmbienteId).OnDelete(DeleteBehavior.Restrict);
            r.HasOne(x => x.Socio).WithMany().HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Restrict);
            r.HasIndex(x => new { x.EventoId, x.Status });
            r.HasIndex(x => new { x.SocioId, x.Status });
        });

        builder.Entity<Reserva>(r =>
        {
            r.ToTable("Reservas");
            r.HasKey(x => x.Id);
            r.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            r.Property(x => x.Taxa).HasPrecision(10, 2);
            r.Property(x => x.Observacao).HasMaxLength(500);
            r.Property(x => x.MotivoRecusa).HasMaxLength(300);
            r.Ignore(x => x.Ativa);
            r.Ignore(x => x.InicioLocal);
            r.HasOne(x => x.Espaco).WithMany().HasForeignKey(x => x.EspacoId).OnDelete(DeleteBehavior.Restrict);
            r.HasOne(x => x.Socio).WithMany().HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Restrict);
            r.HasIndex(x => new { x.SocioId, x.Data });

            // Garante no banco que duas reservas ativas não ocupem o mesmo horário (corrida entre dois sócios).
            r.HasIndex(x => new { x.EspacoId, x.Data, x.Inicio })
                .IsUnique()
                .HasDatabaseName("IX_Reservas_HorarioAtivo")
                .HasFilter("\"Status\" IN ('Pendente', 'Confirmada')");
        });

        builder.Entity<Cobranca>(c =>
        {
            c.ToTable("Cobrancas");
            c.HasKey(x => x.Id);
            c.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            c.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            c.Property(x => x.FormaPagamento).HasConversion<string>().HasMaxLength(20);
            c.Property(x => x.Descricao).HasMaxLength(200).IsRequired();
            c.Property(x => x.Observacao).HasMaxLength(300);
            c.Property(x => x.Valor).HasPrecision(10, 2);
            c.HasOne(x => x.Socio).WithMany().HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Restrict);
            c.HasOne(x => x.Reserva).WithMany().HasForeignKey(x => x.ReservaId).OnDelete(DeleteBehavior.Restrict);
            c.HasOne(x => x.Contrato).WithMany().HasForeignKey(x => x.ContratoId).OnDelete(DeleteBehavior.Restrict);
            c.HasIndex(x => new { x.ContratoId, x.Parcela });
            c.HasOne<ReservaEvento>().WithMany().HasForeignKey(x => x.ReservaEventoId).OnDelete(DeleteBehavior.Restrict);
            c.HasIndex(x => x.ReservaEventoId).IsUnique().HasFilter("\"ReservaEventoId\" IS NOT NULL");
            c.HasIndex(x => new { x.Status, x.Vencimento });

            // Uma mensalidade por título e mês; uma taxa por reserva (mesmo com job e secretaria gerando juntos).
            c.HasIndex(x => new { x.SocioId, x.Competencia }).IsUnique().HasFilter("\"Competencia\" IS NOT NULL");
            c.HasIndex(x => x.ReservaId).IsUnique().HasFilter("\"ReservaId\" IS NOT NULL");
        });

        builder.Entity<Pagamento>(p =>
        {
            p.ToTable("Pagamentos");
            p.HasKey(x => x.Id);
            p.Property(x => x.Gateway).HasMaxLength(30).IsRequired();
            p.Property(x => x.IdExterno).HasMaxLength(100).IsRequired();
            p.HasIndex(x => new { x.Gateway, x.IdExterno }).IsUnique();
            p.Property(x => x.Forma).HasConversion<string>().HasMaxLength(20);
            p.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            p.Property(x => x.Valor).HasPrecision(10, 2);
            p.Property(x => x.BoletoUrl).HasMaxLength(500);
            p.Property(x => x.LinhaDigitavel).HasMaxLength(100);
            p.HasOne(x => x.Cobranca).WithMany(c => c.Pagamentos).HasForeignKey(x => x.CobrancaId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ConfiguracaoFinanceira>(c =>
        {
            c.ToTable("ConfiguracaoFinanceira");
            c.HasKey(x => x.AssociacaoId);
            c.Property(x => x.AssociacaoId).ValueGeneratedNever();
            c.Property(x => x.ValorIndividual).HasPrecision(10, 2);
            c.Property(x => x.ValorFamiliar).HasPrecision(10, 2);
            c.Property(x => x.ValorRemido).HasPrecision(10, 2);
        });

        // Toda tabela da associação: FK para Associacoes, índice e filtro global pela associação do usuário.
        ConfigurarTenant<Socio>(builder);
        ConfigurarTenant<Dependente>(builder);
        ConfigurarTenant<RegistroAcesso>(builder);
        ConfigurarTenant<Espaco>(builder);
        ConfigurarTenant<Reserva>(builder);
        ConfigurarTenant<Cobranca>(builder);
        ConfigurarTenant<Pagamento>(builder);
        ConfigurarTenant<ConfiguracaoFinanceira>(builder);
        ConfigurarTenant<HistoricoAlteracao>(builder);
        ConfigurarTenant<Contrato>(builder);
        ConfigurarTenant<Evento>(builder);
        ConfigurarTenant<ReservaEvento>(builder);
        ConfigurarTenant<Aviso>(builder);
        ConfigurarTenant<Divulgacao>(builder);
    }

    private void ConfigurarTenant<T>(ModelBuilder builder) where T : class, IPertenceAssociacao
    {
        builder.Entity<T>(e =>
        {
            e.HasOne<Associacao>().WithMany().HasForeignKey(x => x.AssociacaoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.AssociacaoId);
            e.HasQueryFilter(x => FiltroDesligado || x.AssociacaoId == AssociacaoAtualId);
        });
    }

    private static void ConfigurarEndereco<T>(OwnedNavigationBuilder<T, Endereco> e) where T : class
    {
        e.Property(x => x.Cep).HasColumnName("Cep").HasMaxLength(8);
        e.Property(x => x.Logradouro).HasColumnName("Logradouro").HasMaxLength(150);
        e.Property(x => x.Numero).HasColumnName("Numero").HasMaxLength(20);
        e.Property(x => x.Complemento).HasColumnName("Complemento").HasMaxLength(100);
        e.Property(x => x.Bairro).HasColumnName("Bairro").HasMaxLength(100);
        e.Property(x => x.Cidade).HasColumnName("Cidade").HasMaxLength(100);
        e.Property(x => x.Uf).HasColumnName("Uf").HasMaxLength(2);
    }

    /// <summary>Registros novos entram na associação atual (o chamador não precisa informar).</summary>
    private void PreencherAssociacao()
    {
        foreach (var entry in ChangeTracker.Entries<IPertenceAssociacao>())
        {
            if (entry.State != EntityState.Added || entry.Entity.AssociacaoId != Guid.Empty) continue;
            entry.Entity.AssociacaoId = AssociacaoAtualId
                ?? throw new InvalidOperationException($"{entry.Entity.GetType().Name} sem associação definida.");
        }
    }

    /// <summary>
    /// Grava no histórico cada campo alterado de sócios, dependentes e associações, com quem fez e quando.
    /// O endereço (tipo owned) é registrado em nome do dono.
    /// </summary>
    private void RegistrarHistorico()
    {
        var agora = DateTimeOffset.UtcNow;
        var novos = new List<HistoricoAlteracao>();

        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            object? dono;
            var ehEndereco = entry.Entity is Endereco;
            if (ehEndereco)
            {
                var ownership = entry.Metadata.FindOwnership();
                if (ownership is null || entry.State == EntityState.Deleted) continue;
                var chave = entry.Property(ownership.Properties[0].Name).CurrentValue;
                dono = ChangeTracker.Entries()
                    .FirstOrDefault(e => e.Metadata == ownership.PrincipalEntityType && Equals(e.Property("Id").CurrentValue, chave))
                    ?.Entity;
                // Endereço de um cadastro novo já faz parte da linha "Cadastro criado".
                if (dono is null || ChangeTracker.Entries().Any(e => e.Entity == dono && e.State == EntityState.Added)) continue;
            }
            else
            {
                dono = entry.Entity;
            }

            if (Alvo(dono) is not { } alvo) continue;

            HistoricoAlteracao Linha(AcaoHistorico acao, string? campo = null, string? anterior = null, string? novo = null) => new()
            {
                AssociacaoId = alvo.AssociacaoId, Entidade = alvo.Entidade, EntidadeId = alvo.Id, RaizId = alvo.RaizId,
                Descricao = alvo.Descricao, Acao = acao, Campo = campo, ValorAnterior = anterior, ValorNovo = novo,
                UsuarioId = usuarioAtual.Id, UsuarioNome = usuarioAtual.Nome, Em = agora,
            };

            if (entry.State == EntityState.Added && !ehEndereco)
            {
                novos.Add(Linha(AcaoHistorico.Criado, novo: alvo.Entidade switch
                {
                    "Dependente" => "Dependente adicionado",
                    "Contrato" => "Contrato criado",
                    _ => "Cadastro criado",
                }));
                continue;
            }
            if (entry.State == EntityState.Deleted)
            {
                novos.Add(Linha(AcaoHistorico.Removido));
                continue;
            }

            var props = entry.Properties
                .Where(p => !p.Metadata.IsShadowProperty() && (entry.State == EntityState.Added || p.IsModified))
                .Select(p => (p.Metadata.Name, entry.State == EntityState.Added ? null : p.OriginalValue, p.CurrentValue));
            foreach (var (campo, anterior, novo) in FormatadorHistorico.Diferencas(props))
                novos.Add(Linha(AcaoHistorico.Alterado, campo, anterior, novo));
        }

        if (novos.Count > 0) Historico.AddRange(novos);
    }

    private sealed record AlvoHistorico(Guid AssociacaoId, string Entidade, Guid Id, Guid RaizId, string? Descricao);

    private static AlvoHistorico? Alvo(object? entidade) => entidade switch
    {
        Socio s => new(s.AssociacaoId, "Socio", s.Id, s.Id, null),
        Dependente d => new(d.AssociacaoId, "Dependente", d.Id, d.SocioId, $"Dependente {d.Nome}"),
        Associacao a => new(a.Id, "Associacao", a.Id, a.Id, null),
        Contrato c => new(c.AssociacaoId, "Contrato", c.Id, c.SocioId, $"Contrato {c.Numero}"),
        _ => null,
    };

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PreencherAssociacao();
        RegistrarHistorico();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PreencherAssociacao();
        RegistrarHistorico();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
