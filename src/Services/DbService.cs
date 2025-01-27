using App.Attributes;
using App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App;

[Service(ServiceLifetime.Transient)]
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