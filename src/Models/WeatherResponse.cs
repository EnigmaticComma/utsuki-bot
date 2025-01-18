using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace App.Models;

[Table("WeatherResponse")]
public class WeatherResponse
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public uint Id { get; set; }

    /// <summary>
    /// The time when this data was cached.
    /// </summary>
    public string CacheTime { get; set; }

    /// <summary>
    /// Error code from the weather API.
    /// </summary>
    public int ErrorCode { get; set; }

    /// <summary>
    /// Name of the location for this weather data.
    /// </summary>
    public string LocalName { get; set; }

    /// <summary>
    /// Main temperature details, serialized as JSON.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public WeatherTemperature MainWeatherTemperature { get; set; }

    /// <summary>
    /// Weather information, serialized as JSON.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public WeatherInfoModel[] WeatherInfoModel { get; set; }

    /// <summary>
    /// Wind information, serialized as JSON.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public WeatherWindModel Wind { get; set; }
}

public struct WeatherTemperature
{
    /// <summary>
    /// Temperature in Kelvin.
    /// </summary>
    public float TemperatureKelvin { get; set; }

    /// <summary>
    /// Temperature converted to Celsius.
    /// </summary>
    [NotMapped]
    public float TemperatureCelsius => TemperatureKelvin - 273.15f;

    /// <summary>
    /// Humidity percentage.
    /// </summary>
    public string Humidity { get; set; }

    /// <summary>
    /// "Feels like" temperature in Kelvin.
    /// </summary>
    public float FeelsLikeKevin { get; set; }

    /// <summary>
    /// "Feels like" temperature converted to Celsius.
    /// </summary>
    [NotMapped]
    public float FeelsLikeCelsius => FeelsLikeKevin - 273.15f;
}

public struct WeatherInfoModel
{
    /// <summary>
    /// Icon representing the weather condition.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Main weather description (e.g., "Rain").
    /// </summary>
    public string Main { get; set; }

    /// <summary>
    /// Detailed weather description (e.g., "Light rain").
    /// </summary>
    public string Description { get; set; }
}

public struct WeatherWindModel
{
    /// <summary>
    /// Wind speed in meters per second.
    /// </summary>
    public float Speed { get; set; }
}
