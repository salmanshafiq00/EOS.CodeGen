global using System;
global using System.Linq;
global using System.Text;
using EasyPOS.CodeGen.Helpers;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EasyPOS.CodeGen;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version, IconResourceID = 400)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[Guid(PackageGuids.CodeGenString)]
public sealed class CodeGenPackage : AsyncPackage
{

    protected async override Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync();

        Logger.Initialize(this, Vsix.Name);

        if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService mcs)
        {
            CommandID menuCommandID = new(PackageGuids.CodeGen, PackageIds.MyCommand);
            OleMenuCommand menuItem = new(Execute, menuCommandID);
            mcs.AddCommand(menuItem);
        }
    }

    private void Execute(object sender, EventArgs e)
    {
        var (entityName, clientPath, cqrsPath) = PromptForFileName();

        if (string.IsNullOrEmpty(entityName))
        {
            // Handle case where nothing is selected or entered
            return;
        }

        if (!string.IsNullOrWhiteSpace(clientPath))
        {
            clientPath = clientPath.Replace("\\", "/");
        }

        try
        {
            var nameofPlural = ProjectHelpers.Pluralize(entityName);
            var templatePath = Utility.GetTemplatesFolderPath();

            // Generated Client
            var cqrsRelativePaths = new List<string>()
            {
                    $"{nameofPlural}/Commands/Upsert{entityName}Command.cs",
                    $"{nameofPlural}/Queries/GetList/Get{nameofPlural}ListQuery.cs",
                    $"{nameofPlural}/Queries/GetDetail/Get{entityName}DetailQuery.cs",
                    $"{nameofPlural}/Models/{entityName}ViewModel.cs"
            };

            var cqrsAbsolutePaths = new List<string>()
            {
                    $"{cqrsPath}/{nameofPlural}/Commands/Upsert{entityName}Command.cs",
                    $"{cqrsPath}/{nameofPlural}/Queries/Get{nameofPlural}ListQuery.cs",
                    $"{cqrsPath}/{nameofPlural}/Queries/Get{entityName}DetailQuery.cs",
                    $"{cqrsPath}/{nameofPlural}/Models/{entityName}ViewModel.cs"
            };

            if (!string.IsNullOrWhiteSpace(cqrsPath))
            {
                foreach (var item in cqrsRelativePaths)
                {
                    string fileName = Path.GetFileName(item);
                    var cqursAbsolutePath = cqrsAbsolutePaths.FirstOrDefault(x => x.Contains(fileName));
                    AddFileAsync(entityName, cqursAbsolutePath, item).Forget();
                }
            }

            // Generated Client
            var generatedClientPages = new List<string>()
            {
                $"GeneratedClient/List/{entityName.GetLowerCaseHyphenatedName()}-list/{entityName.GetLowerCaseHyphenatedName()}-list.component.html",
                $"GeneratedClient/List/{entityName.GetLowerCaseHyphenatedName()}-list/{entityName.GetLowerCaseHyphenatedName()}-list.component.scss",
                $"GeneratedClient/List/{entityName.GetLowerCaseHyphenatedName()}-list/{entityName.GetLowerCaseHyphenatedName()}-list.component.ts",
                $"GeneratedClient/Detail/{entityName.GetLowerCaseHyphenatedName()}-detail/{entityName.GetLowerCaseHyphenatedName()}-detail.component.html",
                $"GeneratedClient/Detail/{entityName.GetLowerCaseHyphenatedName()}-detail/{entityName.GetLowerCaseHyphenatedName()}-detail.component.scss",
                $"GeneratedClient/Detail/{entityName.GetLowerCaseHyphenatedName()}-detail/{entityName.GetLowerCaseHyphenatedName()}-detail.component.ts",
            };

            List<string> clientAbsolutePaths =
            [
                $"{clientPath}/{entityName.GetLowerCaseHyphenatedName()}-list/{entityName.GetLowerCaseHyphenatedName()}-list.component.html",
                $"{clientPath}/{entityName.GetLowerCaseHyphenatedName()}-list/{entityName.GetLowerCaseHyphenatedName()}-list.component.scss",
                $"{clientPath}/{entityName.GetLowerCaseHyphenatedName()}-list/{entityName.GetLowerCaseHyphenatedName()}-list.component.ts",
                $"{clientPath}/{entityName.GetLowerCaseHyphenatedName()}-detail/{entityName.GetLowerCaseHyphenatedName()}-detail.component.html",
                $"{clientPath}/{entityName.GetLowerCaseHyphenatedName()}-detail/{entityName.GetLowerCaseHyphenatedName()}-detail.component.scss",
                $"{clientPath}/{entityName.GetLowerCaseHyphenatedName()}-detail/{entityName.GetLowerCaseHyphenatedName()}-detail.component.ts",
            ];

            if (!string.IsNullOrWhiteSpace(clientPath)) 
            {
                foreach (var item in generatedClientPages)
                {
                    string fileName = Path.GetFileName(item);
                    var clientAbsolutePath = clientAbsolutePaths.FirstOrDefault(x => x.Contains(fileName));
                    AddFileAsync(entityName, clientAbsolutePath, item).Forget();
                }
            }

        }
        catch (Exception ex) when (!ErrorHandler.IsCriticalException(ex))
        {
            Logger.Log(ex);
            MessageBox.Show(
                    $"Error creating file '{entityName}':{Environment.NewLine}{ex.Message}",
                    Vsix.Name,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
        }
        //}
    }

    private async Task AddFileAsync(
        string entityName,
        string clientAbsolutePath,
        string templatePath)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync();
        FileInfo file;

        Directory.CreateDirectory(Path.GetDirectoryName(clientAbsolutePath));

        file = new FileInfo(clientAbsolutePath);

        if (!file.Exists)
        {
            int position = await WriteFileAsync(entityName, clientAbsolutePath, templatePath);

            if (position > 0)
            {
                Microsoft.VisualStudio.Text.Editor.IWpfTextView view = ProjectHelpers.GetCurentTextView();

                view?.Caret.MoveTo(new SnapshotPoint(view.TextBuffer.CurrentSnapshot, position));
            }
        }
        else
        {
            Console.WriteLine($"The file '{file}' already exists.");
        }
    }

    private static async Task<int> WriteFileAsync(
        string entityName,
        string clientAbsolutePath,
        string templatePath)
    {
        string template = await TemplateMap.GetTemplateFilePathAsync(entityName, clientAbsolutePath, templatePath);

        if (!string.IsNullOrEmpty(template))
        {
            int index = template.IndexOf('$');
            string modifiedFile = RemoveFolderNameFromFile(clientAbsolutePath);
            await WriteToDiskAsync(modifiedFile, template, clientAbsolutePath);
            return index;
        }

        await WriteToDiskAsync(clientAbsolutePath, string.Empty, clientAbsolutePath);

        return 0;
    }

    public static string RemoveFolderNameFromFile(string file, int tailSlashRemove = 1)
    {
        string folderName = string.Empty;

        bool containsCommands = file.Contains("Commands");
        bool containsQueries = file.Contains("Queries");

        if (!containsCommands && !containsQueries)
        {
            return file;
        }

        if (file.Contains("Create"))
        {
            folderName = "Create";
        }
        if (file.Contains("Update"))
        {
            folderName = "Update";
        }
        if (file.Contains("Delete"))
        {
            folderName = "Delete";
        }
        if (file.Contains("MultipleDel"))
        {
            folderName = "MultipleDel";
        }
        if (file.Contains("GetAll"))
        {
            folderName = "GetAll";
        }
        if (file.Contains("GetById"))
        {
            folderName = "GetById";
        }
        if (file.Contains("Model"))
        {
            folderName = "Model";
        }
        int indexOfFolderName = file.IndexOf(folderName);
        StringBuilder modifiedFile = new(file.Substring(0, indexOfFolderName));
        modifiedFile.Append(file.Substring(indexOfFolderName + folderName.Length + tailSlashRemove));
        return modifiedFile.ToString();
    }

    private static async Task WriteToDiskAsync(
        string file,
        string content,
        string clientAbsolutePath = null)
    {
        using StreamWriter writer = new(clientAbsolutePath, false, GetFileEncoding(clientAbsolutePath));
        await writer.WriteAsync(content);

    }

    private static Encoding GetFileEncoding(string file)
    {
        string[] noBom = [".cmd", ".bat", ".json"];
        string ext = Path.GetExtension(file).ToLowerInvariant();
        return noBom.Contains(ext) ? new UTF8Encoding(false) : (Encoding)new UTF8Encoding(true);
    }

    private static string[] SplitPath(string path)
    {
        return path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string[] GetParsedInput(string input)
    {
        Regex pattern = new(@"[,]?([^(,]*)([\.\/\\]?)[(]?((?<=[^(])[^,]*|[^)]+)[)]?");
        List<string> results = [];
        Match match = pattern.Match(input);

        while (match.Success)
        {
            // Always 4 matches w. Group[3] being the extension, extension list, folder terminator ("/" or "\"), or empty string
            string path = match.Groups[1].Value.Trim() + match.Groups[2].Value;
            string[] extensions = match.Groups[3].Value.Split(',');

            foreach (string ext in extensions)
            {
                string value = path + ext.Trim();

                // ensure "file.(txt,,txt)" or "file.txt,,file.txt,File.TXT" returns as just ["file.txt"]
                if (value != "" && !value.EndsWith(".", StringComparison.Ordinal) && !results.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(value);
                }
            }
            match = match.NextMatch();
        }
        return [.. results];
    }

    private (string entityName, string clientPath, string cqrsPath) PromptForFileName()
    {
        FileNameDialog dialog = new()
        {
            Owner = Application.Current.MainWindow
        };

        bool? result = dialog.ShowDialog();

        // Return the tuple with both ComboBox and TextBox values if the dialog was confirmed, otherwise return empty strings
        return (result.HasValue && result.Value) ? dialog.GetInputValues() : (string.Empty, string.Empty, string.Empty);
    }


}
