using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FollowMePeak.Utils
{
    public static class FileUtils
{
    public static Task WriteJsonFileInBackground(ModLogger logger, string filePath, object payload)
    {
        return Task.Run(SerializeAndSave);

        async Task SerializeAndSave()
        {
            try
            {
                string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                await WriteFileAtomically(filePath, json);
            }
            catch (Exception e)
            {
                BepInEx.ThreadingHelper.Instance.StartSyncInvoke(() => 
                    logger.Error($"Failed to save {filePath}: {e.Message}"));
            }
        }
    }

    private static async Task WriteFileAtomically(string filePath, string json)
    {
        string directory = Path.GetDirectoryName(filePath);
        Directory.CreateDirectory(directory);

        var newFileName = filePath + ".new";
        await File.WriteAllTextAsync(newFileName, json);
        ReplaceFileAtomically(newFileName, filePath);
    }

    private static void ReplaceFileAtomically(string tempFileName, string targetFileName)
    {
        try
        {
            File.Move(tempFileName, targetFileName);
        }
        catch (IOException)
        {
            File.Replace(tempFileName, targetFileName, null);
        }
    }
}
}