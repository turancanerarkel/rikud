using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rikud.WatchDog
{
    public class RikudDbContext : DbContext
    {
        public RikudDbContext(DbContextOptions<RikudDbContext> options) : base(options) { }

        public DbSet<Exchanges> Exchanges => Set<Exchanges>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            // Döviz
            b.Entity<Exchanges>(e =>
            {
                e.ToTable("Dovizler");
                e.Property(p => p.Alis).HasMaxLength(50);
                e.Property(p => p.Satis).HasMaxLength(50);
            });
        }

    }
    public class Exchanges
    {
        public int Id { get; set; }
        public int AltinTipId { get; set; }
        public decimal Alis { get; set; }
        public decimal Satis { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
