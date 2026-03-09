using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Mes.Opc.Platform.Data.Db;

public partial class OpcDbContext : DbContext
{
    public OpcDbContext(DbContextOptions<OpcDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Machine> Machines { get; set; }

    public virtual DbSet<MachineTagMapping> MachineTagMappings { get; set; }

    public virtual DbSet<MachineTagValue> MachineTagValues { get; set; }

    public virtual DbSet<OpcEndpoint> OpcEndpoints { get; set; }

    public virtual DbSet<UiDashboard> UiDashboards { get; set; }

    public virtual DbSet<UiWidget> UiWidgets { get; set; }

    public virtual DbSet<UiWidgetBinding> UiWidgetBindings { get; set; }

    public virtual DbSet<UiZone> UiZones { get; set; }

    public virtual DbSet<MachineCycleRule> MachineCycleRules { get; set; }

    public virtual DbSet<AnalyticsSummaryDefinition> AnalyticsSummaryDefinitions { get; set; }

    public virtual DbSet<AnalyticsSummaryItem> AnalyticsSummaryItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.OpcEndpoint).WithMany(p => p.Machines)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Machine_Endpoint");
        });

        modelBuilder.Entity<MachineTagMapping>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__MachineT__3214EC07EE395EE0");

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.MachineCodeNavigation).WithMany(p => p.MachineTagMappings)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MTM_Machine");
        });

        modelBuilder.Entity<MachineTagValue>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<OpcEndpoint>(entity =>
        {
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<UiDashboard>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UiDashbo__3214EC0757618C95");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<UiWidget>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UiWidget__3214EC07CDAF3B2D");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Zone).WithMany(p => p.UiWidgets)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UiWidget_Zone");
        });

        modelBuilder.Entity<UiWidgetBinding>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UiWidget__3214EC0743F9EB95");

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Widget).WithMany(p => p.UiWidgetBindings)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UiWidgetBinding_Widget");
        });

        modelBuilder.Entity<UiZone>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UiZone__3214EC076D74D0EA");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Dashboard).WithMany(p => p.UiZones)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UiZone_Dashboard");
        });

        modelBuilder.Entity<MachineCycleRule>(entity =>
        {
            entity.HasIndex(e => new { e.MachineCode, e.ScopeKey })
                  .IsUnique()
                  .HasDatabaseName("UX_MachineCycleRule_Machine_Scope");
        });

        modelBuilder.Entity<AnalyticsSummaryDefinition>(entity =>
        {
            entity.HasKey(e => e.SummaryId);
            entity.HasMany(d => d.Items)
                  .WithOne(i => i.Summary)
                  .HasForeignKey(i => i.SummaryId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_AnalyticsSummaryItem_Definition");
        });

        modelBuilder.Entity<AnalyticsSummaryItem>(entity =>
        {
            entity.HasKey(e => e.ItemId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MachineTagMapping>(entity =>
        {
            entity.HasIndex(e => new { e.MachineCode, e.OpcNodeId })
                  .IsUnique()
                  .HasDatabaseName("UX_MachineTagMapping_MachineCode_OpcNodeId");
        });

        modelBuilder.Entity<MachineTagValue>(entity =>
        {
            entity.HasIndex(e => new { e.MachineCode, e.OpcNodeId, e.SourceTimestamp })
                  .HasDatabaseName("IX_MachineTagValue_MachineCode_OpcNodeId_SourceTimestamp");
        });
    }
}
