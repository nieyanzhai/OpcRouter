using System.IO;
using System.Threading.Tasks;

namespace OpcRouter.Services.FileService;

public interface ICommonFileService
{
    Task<Stream> GetFileAsync(string fileName);
    Task SaveFileAsync(string fileName, Stream fileStream);
    
    Task<T> GetFileAsync<T>(string fileName);
    Task SaveFileAsync<T>(string fileName, T file);
    T LoadJsonFile<T>(string path);
    void SaveJsonFile<T>(string path, T data);
    string SelectFolder();
    string SelectFolder(string selectedPath);
    
}