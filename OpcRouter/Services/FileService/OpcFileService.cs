using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpcRouter.Models.Entities.DeviceEntity;

namespace OpcRouter.Services.FileService;

public class OpcFileService : IOpcFileService
{
    private readonly ILogger<OpcFileService> _logger;

    public OpcFileService(ILogger<OpcFileService> logger)
    {
        _logger = logger;
    }

    public List<string> GetFilePathsInDirectory(params string[] directoryNames)
    {
        var directoryPath = directoryNames.Aggregate(Directory.GetCurrentDirectory(), Path.Combine);
        var filePaths = Directory.GetFiles(directoryPath);
        return filePaths.ToList();
    }

    // True - Unique Key
    // False - Duplicate Key Found
    public bool UniqueKeysCheck<T>(List<string> filesPaths) where T : Device
    {
        var existingKeys = GetExistingKeysOf<T>(filesPaths);
        if (!existingKeys.GroupBy(x => x).Any(x => x.Count() > 1)) return true;
        _logger.LogError("Duplicate Key Name Found!!!");
        // this.hostApplicationLifetime.StopApplication();
        return false;
    }

    private IEnumerable<string> GetExistingKeysOf<T>(List<string> filePaths) where T : Device
    {
        var exitingKeys = new List<string>();
        filePaths.ForEach(x =>
        {
            var tObj = ConvertFileTo<T>(x);
            if (tObj?.DeviceInfo?.DeviceName != null) exitingKeys.Add(tObj.DeviceInfo?.DeviceName);
        });
        return exitingKeys;
    }

    public T? ConvertFileTo<T>(string filePath) where T : Device
    {
        if (string.IsNullOrEmpty(filePath)) return default(T);

        var tObj = JsonConvert.DeserializeObject<T>(File.ReadAllText(filePath));

        // Check tObj is null
        if (tObj is null) return default(T);

        // Check tObj.key is null
        if (!string.IsNullOrEmpty(tObj.DeviceInfo?.DeviceName)) return tObj;
        _logger.LogError("Device With DeviceName(null) is invalid. Please Changed the DeviceName!");
        return default(T);
    }
}