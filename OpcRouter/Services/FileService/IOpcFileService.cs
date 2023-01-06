using System.Collections.Generic;
using OpcRouter.Models.Entities.DeviceEntity;

namespace OpcRouter.Services.FileService;

public interface IOpcFileService
{
    List<string> GetFilePathsInDirectory(params string[] directoryNames);
    bool UniqueKeysCheck<T>(List<string> filesPaths) where T : Device;
    T? ConvertFileTo<T>(string filePath) where T : Device;
}