using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace PuzzlesBot.Context;

public partial class PuzzlesBotContext : DbContext
{
    public PuzzlesBotContext(DbContextOptions<PuzzlesBotContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Puzzles> Puzzles { get; set; }

    public virtual DbSet<Servers> Servers { get; set; }

    public virtual DbSet<Userdata> Userdata { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Puzzles>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("puzzles");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.EndAt)
                .HasColumnType("timestamp")
                .HasColumnName("end_at");
            entity.Property(e => e.Fen)
                .HasMaxLength(255)
                .HasColumnName("fen");
            entity.Property(e => e.Mesid)
                .HasColumnType("bigint(20) unsigned")
                .HasColumnName("mesid");
            entity.Property(e => e.Moves)
                .HasMaxLength(255)
                .HasColumnName("moves");
            entity.Property(e => e.PuzzleId)
                .HasMaxLength(255)
                .HasColumnName("puzzle_id");
            entity.Property(e => e.Rating)
                .HasColumnType("int(11)")
                .HasColumnName("rating");
            entity.Property(e => e.Url)
                .HasMaxLength(255)
                .HasColumnName("url");
        });

        modelBuilder.Entity<Servers>(entity =>
        {
            entity.HasKey(e => e.ServerId).HasName("PRIMARY");

            entity.ToTable("servers");

            entity.Property(e => e.ServerId)
                .HasColumnType("bigint(20)")
                .HasColumnName("server_id");
            entity.Property(e => e.PuzzlesChannel)
                .HasColumnType("bigint(20)")
                .HasColumnName("puzzles_channel");
            entity.Property(e => e.Theme)
                .HasMaxLength(255)
                .HasDefaultValueSql("'''default'''")
                .HasColumnName("theme");
        });

        modelBuilder.Entity<Userdata>(entity =>
        {
            entity.HasKey(e => new { e.ServerId, e.UserId }).HasName("PRIMARY");

            entity.ToTable("userdata");

            entity.Property(e => e.ServerId)
                .HasColumnType("bigint(20)")
                .HasColumnName("server_id");
            entity.Property(e => e.UserId)
                .HasColumnType("bigint(20)")
                .HasColumnName("user_id");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
