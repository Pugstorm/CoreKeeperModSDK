using System.Text.Json;
using Steamworks;
using System.Text.RegularExpressions;

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
        public string[] AdditionalImages { get; set; } = Array.Empty<string>();
        public string[] YouTubeVideos { get; set; } = Array.Empty<string>();
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

                var imagesToValidate = new List<string>(config.AdditionalImages);

                if (!string.IsNullOrEmpty(config.PreviewPath))
                {
                    imagesToValidate.Add(config.PreviewPath);
                }

                foreach (string imagePath in imagesToValidate)
                {
                    string? validationError = GetImageValidationError(imagePath);

                    if (validationError != null)
                    {
                        throw new Exception(validationError);
                    }
                }

                var youTubeVideoIds = new List<string>();

                foreach (string videoUrl in config.YouTubeVideos)
                {
                    string? videoId = GetYouTubeVideoId(videoUrl);

                    if (videoId == null)
                    {
                        throw new Exception("Enter a valid YouTube URL or video ID: " + videoUrl);
                    }

                    if (!youTubeVideoIds.Contains(videoId))
                    {
                        youTubeVideoIds.Add(videoId);
                    }
                }

                config.YouTubeVideos = youTubeVideoIds.ToArray();

                File.WriteAllText("steam_appid.txt", config.AppId.ToString());

                Console.WriteLine($"Initializing Steam for App ID {config.AppId}...");
                try
                {
                    SteamClient.Init(config.AppId, false);
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

                if (result.Success)
                {
                    Console.WriteLine($"SUCCESS_FILE_ID:{result.FileId}");
                    if (config.AdditionalImages.Length > 0 || config.YouTubeVideos.Length > 0)
                    {
                        try
                        {
                            await WorkshopAttachments.Add(
                                config.AppId,
                                result.FileId.Value,
                                config.AdditionalImages.Distinct().ToArray(),
                                config.YouTubeVideos);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("ERROR:Mod content uploaded, but additional media failed or could not be confirmed. Check the Workshop page before adding it again. " + ex.Message);
                            return 1;
                        }
                    }
                    return 0;
                }

                Console.WriteLine(result.Result == Result.LimitExceeded
                    ? "ERROR:Steam rejected the upload: a preview image is too large or your Steam Cloud quota is full. Preview images must be under 1 MiB."
                    : $"ERROR: Upload failed with Steam result: {result.Result}");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }
            finally
            {
                if (SteamClient.IsValid)
                {
                    SteamClient.Shutdown();
                }
            }
        }

        private const long MAX_WORKSHOP_IMAGE_BYTES = 1024 * 1024;

        private static string? GetImageValidationError(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    return "Preview image not found: " + imagePath;
                }

                var imageFile = new FileInfo(imagePath);

                if (imageFile.Length >= MAX_WORKSHOP_IMAGE_BYTES)
                {
                    return $"\"{imageFile.Name}\" is too large ({imageFile.Length:N0} bytes). " +
                        "Steam Workshop images must be smaller than 1 MiB (1,048,576 bytes). " +
                        "Resize or compress the image.";
                }

                return null;
            }
            catch (Exception exception)
            {
                return "Cannot read preview image: " + exception.Message;
            }
        }

        private static string? GetYouTubeVideoId(string videoUrlOrId)
        {
            const string videoIdPattern = @"^[A-Za-z0-9_-]{11}$";

            videoUrlOrId = (videoUrlOrId ?? string.Empty).Trim();

            if (Regex.IsMatch(videoUrlOrId, videoIdPattern))
            {
                return videoUrlOrId;
            }

            if (!Uri.TryCreate(videoUrlOrId, UriKind.Absolute, out var videoUri))
            {
                return null;
            }

            if (videoUri.Scheme != Uri.UriSchemeHttps && videoUri.Scheme != Uri.UriSchemeHttp)
            {
                return null;
            }

            string? videoId = null;

            if (videoUri.Host == "youtu.be")
            {
                videoId = videoUri.AbsolutePath.Trim('/');
            }
            else if (videoUri.Host == "youtube.com" ||
                     videoUri.Host == "www.youtube.com" ||
                     videoUri.Host == "m.youtube.com")
            {
                string[] pathParts = videoUri.AbsolutePath.Trim('/').Split('/');

                if (pathParts.Length == 2)
                {
                    string videoPageType = pathParts[0];

                    if (videoPageType == "shorts" || videoPageType == "embed" || videoPageType == "live")
                    {
                        videoId = pathParts[1];
                    }
                }

                if (videoUri.AbsolutePath == "/watch")
                {
                    string[] queryParameters = videoUri.Query.TrimStart('?').Split('&');

                    foreach (string queryParameter in queryParameters)
                    {
                        if (queryParameter.StartsWith("v="))
                        {
                            videoId = Uri.UnescapeDataString(queryParameter.Substring(2));
                        }
                    }
                }
            }

            if (videoId == null || !Regex.IsMatch(videoId, videoIdPattern))
            {
                return null;
            }

            return videoId;
        }

    }
}
