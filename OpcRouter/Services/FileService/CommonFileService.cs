using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OpcRouter.Services.FileService;

public class CommonFileService:ICommonFileService
{
    public Task<Stream> GetFileAsync(string fileName)
    {
        return Task.FromResult<Stream>(File.OpenRead(fileName));
    }

    public Task SaveFileAsync(string fileName, Stream fileStream)
    {
        using (var file = File.Create(fileName))
        {
            fileStream.CopyTo(file);
        }

        return Task.CompletedTask;
    }

    public Task<T> GetFileAsync<T>(string fileName)
    {
        var json = File.ReadAllText(fileName);
        return Task.FromResult(JsonConvert.DeserializeObject<T>(json));
    }

    public Task SaveFileAsync<T>(string fileName, T file)
    {
        var json = JsonConvert.SerializeObject(file);
        File.WriteAllText(fileName, json);
        return Task.CompletedTask;
    }

    public T LoadJsonFile<T>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<T>(json);
    }

    public void SaveJsonFile<T>(string path, T data)
    {
        var json = JsonConvert.SerializeObject(data);
        File.WriteAllText(path, json);
    }

    public string SelectFolder(string selectedPath)
    {
        // var dialog = new VistaFolderBrowserDialog
        // {
        //     SelectedPath = selectedPath,
        //     Description = "Select a folder",
        //     UseDescriptionForTitle = true,
        //     ShowNewFolderButton = true,
        //     Multiselect = false,
        // };
        //
        // return dialog.ShowDialog() == true ? dialog.SelectedPath : string.Empty;

        return string.Empty;
    }


    public string SelectFolder()
    {
        var selectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return SelectFolder(selectedPath);
    }
}