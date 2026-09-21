using System.Text.Json;
using Steamworks;

namespace PugModUploader
{
    public class UploadConfig
    {
        public uint AppId { get; set; }
        public ulong FileId { get; set; }
        public string? ContentPath { get; set; }
        public string? PreviewPath { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Visibility { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    internal static class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length == 0 || !File.Exists(args[0]))
            {
                Console.WriteLine("ERROR: Invalid or missing configuration file path argument.");
                return 1;
            }

            try
            {
                var config = JsonSerializer.Deserialize<UploadConfig>(
                    File.ReadAllText(args[0]),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (config == null)
                {
                    Console.WriteLine("ERROR: Failed to parse configuration file.");
                    return 1;
                }

                if (string.IsNullOrEmpty(config.ContentPath) || !Directory.Exists(config.ContentPath))
                {
                    Console.WriteLine($"ERROR: Content path does not exist: {config.ContentPath}");
                    return 1;
                }

                File.WriteAllText("steam_appid.txt", config.AppId.ToString());

                Console.WriteLine($"Initializing Steam for App ID {config.AppId}...");
                try
                {
                    SteamClient.Init(config.AppId, true);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR: Steam init failed ({e.Message}). Make sure Steam is running.");
                    return 1;
                }

                if (!SteamClient.IsValid)
                {
                    Console.WriteLine("ERROR: Steam client is not valid. Make sure Steam is running.");
                    return 1;
                }

                Console.WriteLine($"OWNER:{SteamClient.SteamId.Value}");

                var editor = config.FileId == 0
                    ? Steamworks.Ugc.Editor.NewCommunityFile
                    : new Steamworks.Ugc.Editor(config.FileId);

                editor = editor.WithContent(config.ContentPath);

                if (!string.IsNullOrEmpty(config.PreviewPath) && File.Exists(config.PreviewPath))
                    editor = editor.WithPreviewFile(config.PreviewPath);

                if (!string.IsNullOrEmpty(config.Title))
                    editor = editor.WithTitle(config.Title);

                if (!string.IsNullOrEmpty(config.Description))
                    editor = editor.WithDescription(config.Description);

                foreach (var tag in config.Tags)
                    editor = editor.WithTag(tag);

                editor = config.Visibility switch
                {
                    "Public" => editor.WithPublicVisibility(),
                    "Friends Only" => editor.WithFriendsOnlyVisibility(),
                    _ => editor.WithPrivateVisibility()
                };

                Console.WriteLine(config.FileId == 0
                    ? "Submitting new item to Steam Workshop..."
                    : $"Updating Steam Workshop item {config.FileId}...");

                var progress = new Progress<float>(p => Console.WriteLine($"PROGRESS:{p:F3}"));

                var uploadTask = editor.SubmitAsync(progress);
                while (!uploadTask.IsCompleted)
                {
                    SteamClient.RunCallbacks();
                    await Task.Delay(33);
                }

                var result = await uploadTask;
                SteamClient.Shutdown();

                if (result.Success)
                {
                    Console.WriteLine($"SUCCESS_FILE_ID:{result.FileId}");
                    return 0;
                }

                Console.WriteLine($"ERROR: Upload failed with Steam result: {result.Result}");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }
        }
    }
}
