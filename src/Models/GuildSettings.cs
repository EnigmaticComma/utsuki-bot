namespace App.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("GuildSettings")]
public class GuildSettings {

	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	/// <summary>
	/// Text channel to notify when new user joins.
	/// </summary>
	public ulong? JoinChannelId { get; set; }

	/// <summary>
	/// Text channel to notify when new user leaves (or gets banned).
	/// </summary>
	public ulong? LeaveChannelId { get; set; }

	/// <summary>
	/// Text channelId to backup messages.
	/// </summary>
	public ulong? AttachmentsBackupChannelId { get; set; }

	/// <summary>
	/// Array of dynamic renamed voice channels.
	/// </summary>
	public string? DynamicVoiceChannels { get; private set; }

	public ulong?[] GetDynamicVoiceChannels() =>
		string.IsNullOrEmpty(DynamicVoiceChannels)
			? []
			: DynamicVoiceChannels.Split(',').Select(ulong.Parse).Cast<ulong?>().ToArray();

	public void SetDynamicVoiceChannels(ulong?[] channels) =>
		DynamicVoiceChannels = string.Join(",", channels);


	/// <summary>
	/// Enables new user anti-spam in guild.
	/// </summary>
	public bool EnableNewUserAntiSpam { get; set; } = true;

	/// <summary>
	/// Channel to send hourly notification.
	/// </summary>
	public ulong? HourlyMessageChannelId { get; set; }

	/// <summary>
	/// Channel to send bot logs.
	/// </summary>
	public ulong? BotLogsTextChannelId { get; set; }

}