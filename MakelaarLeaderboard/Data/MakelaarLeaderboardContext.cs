using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MakelaarLeaderboard.Models;

namespace MakelaarLeaderboard.Data
{
    public class MakelaarLeaderboardContext : DbContext
    {
        public MakelaarLeaderboardContext (DbContextOptions<MakelaarLeaderboardContext> options)
            : base(options)
        {
        }

        public DbSet<House> Houses { get; set; } = null!;
        public DbSet<Makelaar> Makelaars { get; set;} = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<House>()
                .HasOne(h => h.Makelaar)
                .WithMany(m => m.Houses)
                .HasForeignKey(h => h.MakelaarId);
        }
    }
}
