using System.Runtime.InteropServices;
using Steamworks;

namespace PugModUploader
{
    internal static class WorkshopAttachments
    {
        private const string NATIVE_LIBRARY_NAME = "workshop_steam";
        private const int IMAGE_PREVIEW_TYPE = 0;
        private const int UPDATE_RESULT_SIZE = 16;
        private const int UPDATE_RESULT_CALLBACK_ID = 3404;

        static WorkshopAttachments()
        {
            NativeLibrary.SetDllImportResolver(typeof(WorkshopAttachments).Assembly,
                (libraryName, assembly, searchPath) =>
                {
                    if (libraryName != NATIVE_LIBRARY_NAME)
                    {
                        return IntPtr.Zero;
                    }

                    string libraryFileName = OperatingSystem.IsWindows()
                        ? "steam_api64.dll"
                        : "libsteam_api.so";

                    string libraryPath = Path.Combine(AppContext.BaseDirectory, libraryFileName);
                    return NativeLibrary.Load(libraryPath);
                });
        }

        internal static async Task Add(uint appId, ulong fileId, string[] imagePaths, string[] youTubeVideoIds)
        {
            var workshopInterface = SteamAPI_SteamUGC_v016();
            var utilitiesInterface = SteamAPI_SteamUtils_v010();

            if (workshopInterface == IntPtr.Zero || utilitiesInterface == IntPtr.Zero)
            {
                throw new Exception("Steam Workshop interface unavailable.");
            }

            ulong updateHandle = SteamAPI_ISteamUGC_StartItemUpdate(workshopInterface, appId, fileId);

            if (updateHandle == ulong.MaxValue)
            {
                throw new Exception("Steam could not start the media upload.");
            }

            foreach (string imagePath in imagePaths)
            {
                string fullImagePath = Path.GetFullPath(imagePath);

                if (!SteamAPI_ISteamUGC_AddItemPreviewFile(workshopInterface, updateHandle, fullImagePath, IMAGE_PREVIEW_TYPE))
                {
                    throw new Exception("Steam rejected image: " + Path.GetFileName(imagePath));
                }
            }

            foreach (string videoId in youTubeVideoIds)
            {
                if (!SteamAPI_ISteamUGC_AddItemPreviewVideo(workshopInterface, updateHandle, videoId))
                {
                    throw new Exception("Steam rejected YouTube video: " + videoId);
                }
            }

            ulong apiCallHandle = SteamAPI_ISteamUGC_SubmitItemUpdate(workshopInterface, updateHandle, "");

            if (apiCallHandle == 0)
            {
                throw new Exception("Steam could not submit the additional media.");
            }

            // Clear the editor's pending media even if the result is uncertain.
            // Retrying the content upload must not append the same attachments again.
            Console.WriteLine("MEDIA_SUBMITTED");

            var uploadStartedAt = DateTime.UtcNow;
            bool apiCallFailed;

            while (!SteamAPI_ISteamUtils_IsAPICallCompleted(utilitiesInterface, apiCallHandle, out apiCallFailed))
            {
                if (DateTime.UtcNow - uploadStartedAt > TimeSpan.FromMinutes(5))
                {
                    throw new TimeoutException("Media upload timed out.");
                }

                SteamClient.RunCallbacks();
                await Task.Delay(33);
            }

            if (apiCallFailed)
            {
                throw new Exception("Steam did not confirm the media upload.");
            }

            bool receivedResult = SteamAPI_ISteamUtils_GetAPICallResult(
                utilitiesInterface,
                apiCallHandle,
                out var uploadResult,
                UPDATE_RESULT_SIZE,
                UPDATE_RESULT_CALLBACK_ID,
                out apiCallFailed);

            if (!receivedResult || apiCallFailed)
            {
                throw new Exception("Steam did not confirm the media upload.");
            }

            if (uploadResult.Result != Result.OK)
            {
                throw new Exception("Steam media result: " + uploadResult.Result);
            }
        }

        // SubmitItemUpdateResult_t uses these offsets on Windows x64 and Linux x64.
        [StructLayout(LayoutKind.Explicit, Size = UPDATE_RESULT_SIZE)]
        private struct UpdateResult
        {
            [FieldOffset(0)]
            public Result Result;

            [FieldOffset(4)]
            public byte NeedsAgreement;

            [FieldOffset(8)]
            public ulong FileId;
        }

        [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SteamAPI_SteamUGC_v016();

        [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SteamAPI_SteamUtils_v010();

        [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong SteamAPI_ISteamUGC_StartItemUpdate(IntPtr workshopInterface, uint appId, ulong fileId);

        [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamAPI_ISteamUGC_AddItemPreviewFile(
            IntPtr workshopInterface,
            ulong updateHandle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string imagePath,
            int previewType);

        [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamAPI_ISteamUGC_AddItemPreviewVideo(
            IntPtr workshopInterface,
            ulong updateHandle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string videoId);

        [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong SteamAPI_ISteamUGC_SubmitItemUpdate(
            IntPtr workshopInterface,
            ulong updateHandle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string changeNote);

        [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamAPI_ISteamUtils_IsAPICallCompleted(
            IntPtr utilitiesInterface,
            ulong apiCallHandle,
            [MarshalAs(UnmanagedType.I1)] out bool apiCallFailed);

        [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamAPI_ISteamUtils_GetAPICallResult(
            IntPtr utilitiesInterface,
            ulong apiCallHandle,
            out UpdateResult result,
            int resultSize,
            int callbackId,
            [MarshalAs(UnmanagedType.I1)] out bool apiCallFailed);
    }
}
