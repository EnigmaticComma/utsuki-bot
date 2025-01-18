using App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace App;

public class DbService : DbContext, IDisposable {

	public DbSet<GuildSettings> GuildSettings { get; set; }

	readonly IConfigurationRoot _config;

	public DbService(IConfigurationRoot config)
	{
		_config = config;
	}

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder.UseNpgsql($"Host=localhost;Database=botdb;Username=postgres;Password={_config["DB_PASSWORD"]}");
	}

	public override void Dispose() { }

}