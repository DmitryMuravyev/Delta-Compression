using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeltaComp
{
    // Application settings read from appsettings.json next to the executable.
    public sealed class AppSettings
    {
        public const string FileName = "appsettings.json";

        // Max buffer size for the file data source
        public int MaxBufferSize { get; set; } = 104857600;

        // Target platforms offered for the .h export (see Header_Source.xml)
        public List<string> Platforms { get; set; } = [ "AVR", "STM32", "ESP32" ];


        // Read settings; any problem falls back to the defaults.
        public static AppSettings Load()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, FileName);
                if (File.Exists(path))
                {
                    AppSettings? settings = JsonSerializer.Deserialize(File.ReadAllText(path), AppSettingsJsonContext.Default.AppSettings);
                    if (settings != null)
                    {
                        if (settings.MaxBufferSize <= 0) settings.MaxBufferSize = new AppSettings().MaxBufferSize;
                        settings.Platforms = settings.Platforms.Where(p => !String.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Settings were not loaded: " + ex.Message);
            }

            return new AppSettings();
        }
    }


    // Source-generated serializer (keeps the trimmed build reflection-free)
    [JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
    [JsonSerializable(typeof(AppSettings))]
    internal partial class AppSettingsJsonContext : JsonSerializerContext
    {
    }
}
