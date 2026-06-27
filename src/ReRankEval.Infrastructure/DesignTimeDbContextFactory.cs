using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ReRankEval.Infrastructure.Data;

namespace ReRankEval.Infrastructure;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ExperimentDbContext>
{
    public ExperimentDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<ExperimentDbContext>()
            .UseSqlite("Data Source=experiments.db")
            .Options;
        return new ExperimentDbContext(opts);
    }
}
