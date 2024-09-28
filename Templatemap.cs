using EOS.CodeGen.Helpers;
using EOS.CodeGen.Models;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EOS.CodeGen;

internal static class TemplateMap
{
    private static readonly string _folder;
    private static readonly List<string> _templateFiles = [];
    private const string _defaultExt = ".txt";
    private const string _templateDir = ".templates";
    private const string _defaultNamespace = "EOS";
    static TemplateMap()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userProfile = Path.Combine(folder, ".vs", _templateDir);

        if (Directory.Exists(userProfile))
        {
            _templateFiles.AddRange(Directory.GetFiles(userProfile, "*" + _defaultExt, SearchOption.AllDirectories));
        }

        var assembly = Assembly.GetExecutingAssembly().Location;
        _folder = Path.Combine(Path.GetDirectoryName(assembly), "Templates");
        _templateFiles.AddRange(Directory.GetFiles(_folder, "*" + _defaultExt, SearchOption.AllDirectories));
    }

    public static async Task<string> GetTemplateFilePathAsync(
        string entityName,
        string clientAbsolutePath,
        string templatePath)
    {
        // Define possible template folders (relative structure)
        var templateFolders = new string[] {
            "GeneratedClient/List",
            "GeneratedClient/Detail",
            "Commands",
            "Queries/GetDetail",
            "Queries/GetList",
            "Models",

        };

        // Get the file extension from clientAbsolutePath (e.g., .html)
        var extension = Path.GetExtension(clientAbsolutePath).ToLowerInvariant();

        // Get the list of all available template files
        var list = _templateFiles.Select(x => x.Replace("\\", "/")).ToList();

        var templateFolder = templateFolders.FirstOrDefault(x => templatePath.Contains(x));

        if (string.IsNullOrEmpty(templateFolder))
        {
            return string.Empty;
        }

        var templateAbsolutePath = list.FirstOrDefault(x => x.Contains(templateFolder) && x.Contains(extension));

        if (!string.IsNullOrEmpty(templateAbsolutePath))
        {
            // Replace tokens (e.g., {{EntityName}}) in the template content
            var template = await ReplaceTokensAsync(entityName, clientAbsolutePath, templateAbsolutePath);
            return NormalizeLineEndings(template);
        }

        // If no match found, handle the error (return empty string or log)
        return string.Empty;
    }

    private static async Task<string> ReplaceTokensAsync(
        string entityName,
        string clientAbsolutePath,
        string templateFile)
    {
        if (string.IsNullOrEmpty(templateFile))
        {
            return templateFile;
        }

        using var reader = new StreamReader(templateFile);
        var content = await reader.ReadToEndAsync();
        var nameofPlural = ProjectHelpers.Pluralize(entityName);

        return content.Replace("{rootnamespace}", _defaultNamespace)
                      .Replace("{lowerCaseHyphenatedName}", entityName.GetLowerCaseHyphenatedName())
                      .Replace("{itemname}", entityName)
                      .Replace("{nameofPlural}", ProjectHelpers.Pluralize(entityName))
                      .Replace("{newGuidId}", Guid.NewGuid().ToString())
                      ;
    }

    private static string GetEntityNamespace(string fullName)
    {
        return fullName.Substring(0, fullName.LastIndexOf('.'));
    }

    private static string NormalizeLineEndings(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        return Regex.Replace(content, @"\r\n|\n\r|\n|\r", "\r\n");
    }

    private static string AdjustForSpecific(string safeName, string extension)
    {
        if (Regex.IsMatch(safeName, "^I[A-Z].*"))
        {
            return extension += "-interface";
        }

        return extension;
    }
    private static string SplitCamelCase(string str)
    {
        var r = new Regex(@"
                (?<=[A-Z])(?=[A-Z][a-z]) |
                 (?<=[^A-Z])(?=[A-Z]) |
                 (?<=[A-Za-z])(?=[^A-Za-z])", RegexOptions.IgnorePatternWhitespace);
        return r.Replace(str, " ");
    }
    public const string PRIMARYKEY = "Id";
    private static string CreateDtoFieldDefinition(IntellisenseObject classObject)
    {
        var output = new StringBuilder();
        foreach (var property in classObject.Properties.Where(x => x.Type.IsKnownType == true || x.Type.CodeName == "System.Guid" || x.Type.CodeName == "System.Guid?"))
        {
            //output.Append($"    [Description(\"{SplitCamelCase(property.Name)}\")]\r\n");
            if (property.Name == PRIMARYKEY)
            {
                output.Append($"    public {property.Type.CodeName} {property.Name} {{get;set;}} \r\n");
            }
            else
            {
                switch (property.Type.CodeName)
                {
                    case "string" when property.Name.Equals("Name", StringComparison.OrdinalIgnoreCase):
                        output.Append($"    public {property.Type.CodeName} {property.Name} {{get;set;}} = string.Empty; \r\n");
                        break;
                    case "string" when !property.Name.Equals("Name", StringComparison.OrdinalIgnoreCase) && !property.Type.IsArray && !property.Type.IsDictionary:
                        output.Append($"    public {property.Type.CodeName}? {property.Name} {{get;set;}} \r\n");
                        break;
                    case "string" when !property.Name.Equals("Name", StringComparison.OrdinalIgnoreCase) && property.Type.IsArray:
                        output.Append($"    public HashSet<{property.Type.CodeName}>? {property.Name} {{get;set;}} \r\n");
                        break;
                    case "System.DateTime?":
                        output.Append($"    public DateTime? {property.Name} {{get;set;}} \r\n");
                        break;
                    case "System.DateTime":
                        output.Append($"    public DateTime {property.Name} {{get;set;}} \r\n");
                        break;
                    case "System.Guid?":
                        output.Append($"    public Guid? {property.Name} {{get;set;}} \r\n");
                        break;
                    case "System.Guid":
                        output.Append($"    public Guid {property.Name} {{get;set;}} \r\n");
                        break;
                    case "decimal?":
                    case "decimal":
                    case "int?":
                    case "int":
                    case "double?":
                    case "double":
                        output.Append($"    public {property.Type.CodeName} {property.Name} {{get;set;}} \r\n");
                        break;
                    default:
                        if (property.Type.CodeName.Any(x => x == '?'))
                        {
                            output.Append($"    public {property.Type.CodeName} {property.Name} {{get;set;}} \r\n");
                        }
                        else
                        {
                            if (property.Type.IsOptional)
                            {
                                output.Append($"    public {property.Type.CodeName}? {property.Name} {{get;set;}} \r\n");
                            }
                            else
                            {
                                output.Append($"    public {property.Type.CodeName} {property.Name} {{get;set;}} \r\n");
                            }
                        }
                        break;
                }

            }
        }

        var result = output.ToString();

        // Remove the first four leading spaces from the first line
        var lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None);
        if (lines.Length > 0)
        {
            lines[0] = lines[0].TrimStart();
        }

        // Join lines back into the final string
        return string.Join("\r\n", lines);
    }

    private static string CreateRecordFieldDefinition(IntellisenseObject classObject)
    {
        var output = new StringBuilder();

        foreach (var property in classObject.Properties.Where(x => x.Type.IsKnownType == true || x.Type.CodeName == "System.Guid" || x.Type.CodeName == "System.Guid?"))
        {
            if (property.Name == PRIMARYKEY)
            {
                output.Append($"    {property.Type.CodeName} {property.Name}, \r\n");
            }
            else
            {
                switch (property.Type.CodeName)
                {
                    case "string" when property.Name.Equals("Name", StringComparison.OrdinalIgnoreCase):
                        output.Append($"    {property.Type.CodeName} {property.Name}, \r\n");
                        break;
                    case "string" when !property.Name.Equals("Name", StringComparison.OrdinalIgnoreCase) && !property.Type.IsArray && !property.Type.IsDictionary:
                        output.Append($"    {property.Type.CodeName}? {property.Name}, \r\n");
                        break;
                    case "string" when !property.Name.Equals("Name", StringComparison.OrdinalIgnoreCase) && property.Type.IsArray:
                        output.Append($"    HashSet<{property.Type.CodeName}>? {property.Name}, \r\n");
                        break;
                    case "System.DateTime?":
                        output.Append($"    DateTime? {property.Name}, \r\n");
                        break;
                    case "System.DateTime":
                        output.Append($"    DateTime {property.Name}, \r\n");
                        break;
                    case "System.Guid":
                        output.Append($"    Guid {property.Name}, \r\n");
                        break;
                    case "System.Guid?":
                        output.Append($"    Guid? {property.Name}, \r\n");
                        break;
                    case "decimal?":
                    case "decimal":
                    case "int?":
                    case "int":
                    case "double?":
                    case "double":
                        output.Append($"    {property.Type.CodeName} {property.Name}, \r\n");
                        break;
                    default:
                        if (property.Type.CodeName.Contains('?'))
                        {
                            output.Append($"    {property.Type.CodeName} {property.Name}, \r\n");
                        }
                        else
                        {
                            if (property.Type.IsOptional)
                            {
                                output.Append($"    {property.Type.CodeName}? {property.Name}, \r\n");
                            }
                            else
                            {
                                output.Append($"    {property.Type.CodeName} {property.Name}, \r\n");
                            }
                        }
                        break;
                }
            }
        }

        // Convert StringBuilder to string
        var result = output.ToString();

        // Remove trailing comma and new line from the last property
        result = result.TrimEnd(new[] { ',', ' ', '\r', '\n' });

        // Remove the first four leading spaces from the first line
        var lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None);
        if (lines.Length > 0)
        {
            lines[0] = lines[0].TrimStart();
        }

        // Join lines back into the final string
        return string.Join("\r\n", lines);
    }

    private static string CreateGetByIdQuerySql(IntellisenseObject classObject, string entityPluralName)
    {
        var output = new StringBuilder();

        // Start SQL query with proper triple quotes
        output.AppendLine(@"var sql = $""""""");
        output.AppendLine("            SELECT");
        output.AppendLine($"                t.Id AS {{nameof({classObject.Name}Model.Id)}},");
        // Get the properties count for checking the last property
        var properties = classObject.Properties.Where(x => x.Type.IsKnownType || x.Type.CodeName == "System.Guid" || x.Type.CodeName == "System.Guid?").ToList();
        int propertyCount = properties.Count;
        int currentIndex = 0;

        // Iterate over properties and append SQL column mappings
        foreach (var property in properties)
        {
            currentIndex++;
            if (currentIndex < propertyCount)
            {
                output.AppendLine($"                t.{property.Name} AS {{nameof({classObject.Name}Model.{property.Name})}},");
            }
            else
            {
                output.AppendLine($"                t.{property.Name} AS {{nameof({classObject.Name}Model.{property.Name})}}");
            }
        }

        // Append FROM clause
        output.AppendLine($"            FROM dbo.{entityPluralName} AS t");

        // Append WHERE clause
        output.AppendLine("            WHERE t.Id = @Id");

        // End SQL query with proper triple quotes
        output.AppendLine("            \"\"\";");

        // Return the final SQL query as a string
        return output.ToString();
    }

    private static string CreateGetListQuerySql(IntellisenseObject classObject, string entityPluralName)
    {
        var output = new StringBuilder();

        // Start SQL query with proper triple quotes
        output.AppendLine(@"var sql = $""""""");
        output.AppendLine("            SELECT");
        output.AppendLine($"                t.Id AS {{nameof({classObject.Name}Model.Id)}},");
        // Get the properties count for checking the last property
        var properties = classObject.Properties.Where(x => x.Type.IsKnownType || x.Type.CodeName == "System.Guid" || x.Type.CodeName == "System.Guid?").ToList();
        int propertyCount = properties.Count;
        int currentIndex = 0;

        // Iterate over properties and append SQL column mappings
        foreach (var property in properties)
        {
            currentIndex++;
            if (currentIndex < propertyCount)
            {
                output.AppendLine($"                t.{property.Name} AS {{nameof({classObject.Name}Model.{property.Name})}},");
            }
            else
            {
                output.AppendLine($"                t.{property.Name} AS {{nameof({classObject.Name}Model.{property.Name})}}");
            }
        }

        // Append FROM clause
        output.AppendLine($"            FROM dbo.{entityPluralName} AS t");

        // Append WHERE clause
        output.AppendLine("            WHERE 1 = 1");

        // End SQL query with proper triple quotes
        output.AppendLine("            \"\"\";");

        // Return the final SQL query as a string
        return output.ToString();
    }

    private static string CreateFormInitializationProperties(IntellisenseObject classObject, string entityPluralName)
    {
        var output = new StringBuilder();

        // Get the properties count for checking the last property
        var properties = classObject.Properties.Where(x => x.Type.IsKnownType || x.Type.CodeName == "System.Guid" || x.Type.CodeName == "System.Guid?").ToList();
        int propertyCount = properties.Count;
        int currentIndex = 0;

        // Iterate over properties and append SQL column mappings
        foreach (var property in properties)
        {
            var propertyss =  property.Type.CodeName;
            currentIndex++;
            if (currentIndex < propertyCount && currentIndex == 1)
            {
                output.AppendLine($"{property.Name.ToCamelCase()}: [null],");
            }
            else if (currentIndex < propertyCount)
            {
                output.AppendLine($"      {property.Name.ToCamelCase()}: [null],");
            }
            else
            {
                output.AppendLine($"      {property.Name.ToCamelCase()}: [null]");
            }
        }
        return output.ToString();
    }

    private static string CreateFormControls(IntellisenseObject classObject, string entityPluralName)
    {
        var output = new StringBuilder();

        // Get the properties count for checking the last property
        var properties = classObject.Properties.Where(x => x.Type.IsKnownType || x.Type.CodeName == "System.Guid" || x.Type.CodeName == "System.Guid?").ToList();

        // Iterate over properties and append SQL column mappings
        foreach (var property in properties)
        {
            switch (property.Type.CodeName)
            {
                case "string" when property.Name.Equals("Name", StringComparison.OrdinalIgnoreCase):
                    output.AppendLine($"""
                            <div class="field col-12 md:col-6">
                                <app-input-text label="{property.Name}" formControlName="{property.Name.ToCamelCase()}" [required]="false" [readonly]="false" />
                                <app-validator-msg [control]="f['{property.Name.ToLower()}']" />
                            </div>
                        """);
                    break;
                case "System.DateTime?":
                case "System.DateTime":
                    output.AppendLine($"""
                            <div class="field col-12 md:col-6">
                              <app-input-date label="{property.Name}" formControlName="{property.Name.ToCamelCase()}" [required]="false" />
                            </div>
                        """);
                    break;
                case "System.Guid?":
                case "System.Guid":
                    output.AppendLine($"""
                            <div class="field col-12 md:col-6">
                              <app-input-select label="{property.Name}" formControlName="{property.Name.ToCamelCase()}" [options]="optionsDataSources?.['SelectList']"
                                [required]="false" />
                            </div>
                        """);
                    break;
                case "decimal?":
                case "decimal":
                case "double?":
                case "double":
                    output.AppendLine($""""
                            <div class="field col-12 md:col-6">
                              <app-input-decimal label="{property.Name}" formControlName="{property.Name.ToCamelCase()}" textAlign="right" [showButtons]="false" [required]="false" />
                            </div>
                        """");
                    break;
                case "int?":
                case "int":
                    output.AppendLine($""""
                            <div class="field col-12 md:col-6">
                              <app-input-number label="{property.Name}" formControlName="{property.Name.ToCamelCase()}" textAlign="right" [showButtons]="false" [required]="false" />
                            </div>
                        """");
                    break;
                case "bool":
                case "bool?":
                    output.AppendLine($""""
                            <div class="field col-12 md:col-6 md:flex align-items-center md:mt-4">
                              <app-input-switch label="{property.Name}" formControlName="{property.Name.ToCamelCase()}" />
                            </div>
                        """");
                    break;
                default:
                    output.AppendLine($"""
                            <div class="field col-12 md:col-6">
                              <app-input-text label="{property.Name}" formControlName="{property.Name.ToCamelCase()}" [required]="false" [readonly]="false" />
                              <app-validator-msg [control]="f['{property.Name.ToCamelCase()}']"></app-validator-msg>
                            </div>
                        """);
                    break;
            }
        }
        return output.ToString();
    }

    private static string CreateConfigurationContent(IntellisenseObject classObject, string entityPluralName)
    {
        var output = new StringBuilder();

        // Get the properties count for checking the last property
        var properties = classObject.Properties.Where(x => x.Type.IsKnownType || x.Type.CodeName == "System.Guid" || x.Type.CodeName == "System.Guid?").ToList();

        // Iterate over properties and append SQL column mappings
        foreach (var property in properties)
        {
            switch (property.Type.CodeName)
            {
                case "string" when property.Name.Equals("Name", StringComparison.OrdinalIgnoreCase):
                    output.AppendLine();
                    output.AppendLine($"""
                            builder.Property(t => t.{property.Name})
                                .HasMaxLength(250)
                                .IsRequired(false);
                        """);
                    break;
                case "decimal?":
                case "decimal":
                    output.AppendLine();
                    output.AppendLine($""""
                            builder.Property(t => t.{property.Name})
                                .HasColumnType("decimal(18, 2)")
                                .IsRequired(false);
                        """");
                    break;
                default:
                    break;
            }
        }
        return output.ToString();
    }

    private static string CreateImportFuncExpression(IntellisenseObject classObject)
    {
        var output = new StringBuilder();
        foreach (var property in classObject.Properties.Where(x => x.Type.IsKnownType == true))
        {
            if (property.Name == PRIMARYKEY) continue;
            if (property.Type.CodeName.StartsWith("bool"))
            {
                output.Append($"{{ _localizer[_dto.GetMemberDescription(x=>x.{property.Name})], (row, item) => item.{property.Name} =Convert.ToBoolean(row[_localizer[_dto.GetMemberDescription(x=>x.{property.Name})]]) }}, \r\n");
            }
            else
            {
                output.Append($"{{ _localizer[_dto.GetMemberDescription(x=>x.{property.Name})], (row, item) => item.{property.Name} = row[_localizer[_dto.GetMemberDescription(x=>x.{property.Name})]].ToString() }}, \r\n");
            }
        }
        return output.ToString();
    }
    private static string CreateTemplateFieldDefinition(IntellisenseObject classObject)
    {
        var output = new StringBuilder();
        foreach (var property in classObject.Properties.Where(x => x.Type.IsKnownType == true))
        {
            if (property.Name == PRIMARYKEY) continue;
            output.Append($"_localizer[_dto.GetMemberDescription(x=>x.{property.Name})], \r\n");
        }
        return output.ToString();
    }
    private static string CreateExportFuncExpression(IntellisenseObject classObject)
    {
        var output = new StringBuilder();
        foreach (var property in classObject.Properties.Where(x => x.Type.IsKnownType == true))
        {
            output.Append($"{{_localizer[_dto.GetMemberDescription(x=>x.{property.Name})],item => item.{property.Name}}}, \r\n");
        }
        return output.ToString();
    }

    #region MudBlazor
    private static string CreateMudTdHeaderDefinition(IntellisenseObject classObject)
    {
        var output = new StringBuilder();
        var defaultfieldName = new string[] { "Name", "Description" };
        if (classObject.Properties.Where(x => x.Type.IsKnownType == true && defaultfieldName.Contains(x.Name)).Any())
        {
            output.Append($"<PropertyColumn Property=\"x => x.Name\" Title=\"@L[_currentDto.GetMemberDescription(x=>x.Name)]\"> \r\n");
            output.Append("   <CellTemplate>\r\n");
            output.Append($"      <div class=\"d-flex flex-column\">\r\n");
            if (classObject.Properties.Where(x => x.Type.IsKnownType == true && x.Name == defaultfieldName.First()).Any())
            {
                output.Append($"        <MudText Typo=\"Typo.body2\">@context.Item.Name</MudText>\r\n");
            }
            if (classObject.Properties.Where(x => x.Type.IsKnownType == true && x.Name == defaultfieldName.Last()).Any())
            {
                output.Append($"        <MudText Typo=\"Typo.body2\" Class=\"mud-text-secondary\">@context.Item.Description</MudText>\r\n");
            }
            output.Append($"     </div>\r\n");
            output.Append("    </CellTemplate>\r\n");
            output.Append($"</PropertyColumn>\r\n");
        }
        foreach (var property in classObject.Properties.Where(x => !defaultfieldName.Contains(x.Name)))
        {
            if (property.Name == PRIMARYKEY) continue;
            output.Append("                ");
            output.Append($"<PropertyColumn Property=\"x => x.{property.Name}\" Title=\"@L[_currentDto.GetMemberDescription(x=>x.{property.Name})]\" />\r\n");
        }
        return output.ToString();
    }

    private static string CreateMudTdDefinition(IntellisenseObject classObject)
    {
        var output = new StringBuilder();
        var defaultfieldName = new string[] { "Name", "Description" };
        if (classObject.Properties.Where(x => x.Type.IsKnownType == true && defaultfieldName.Contains(x.Name)).Any())
        {
            output.Append($"<MudTd HideSmall=\"false\" DataLabel=\"@L[_currentDto.GetMemberDescription(x=>x.Name)]\"> \r\n");
            output.Append("                ");
            output.Append($"    <div class=\"d-flex flex-column\">\r\n");
            if (classObject.Properties.Where(x => x.Type.IsKnownType == true && x.Name == defaultfieldName.First()).Any())
            {
                output.Append("                ");
                output.Append($"        <MudText>@context.Name</MudText>\r\n");
            }
            if (classObject.Properties.Where(x => x.Type.IsKnownType == true && x.Name == defaultfieldName.Last()).Any())
            {
                output.Append("                ");
                output.Append($"        <MudText Typo=\"Typo.body2\" Class=\"mud-text-secondary\">@context.Description</MudText>\r\n");
            }
            output.Append("                ");
            output.Append($"    </div>\r\n");
            output.Append("                ");
            output.Append($"</MudTd>\r\n");
        }
        foreach (var property in classObject.Properties.Where(x => x.Type.IsKnownType == true && !defaultfieldName.Contains(x.Name)))
        {
            if (property.Name == PRIMARYKEY) continue;
            output.Append("                ");
            if (property.Type.CodeName.StartsWith("bool", StringComparison.OrdinalIgnoreCase))
            {
                output.Append($"        <MudTd HideSmall=\"false\" DataLabel=\"@L[_currentDto.GetMemberDescription(x=>x.{property.Name})]\" ><MudCheckBox Checked=\"@context.{property.Name}\" ReadOnly></MudCheckBox></MudTd> \r\n");
            }
            else if (property.Type.CodeName.Equals("System.DateTime", StringComparison.OrdinalIgnoreCase))
            {
                output.Append($"        <MudTd HideSmall=\"false\" DataLabel=\"@L[_currentDto.GetMemberDescription(x=>x.{property.Name}))]\" >@context.{property.Name}.Date.ToString(\"d\")</MudTd> \r\n");
            }
            else if (property.Type.CodeName.Equals("System.DateTime?", StringComparison.OrdinalIgnoreCase))
            {
                output.Append($"        <MudTd HideSmall=\"false\" DataLabel=\"@L[_currentDto.GetMemberDescription(x=>x{property.Name})]\" >@context.{property.Name}?.Date.ToString(\"d\")</MudTd> \r\n");
            }
            else
            {
                output.Append($"        <MudTd HideSmall=\"false\" DataLabel=\"@L[_currentDto.GetMemberDescription(x=>.{property.Name})]\" >@context.{property.Name}</MudTd> \r\n");
            }

        }
        return output.ToString();
    }

    private static string CreateMudFormFieldDefinition(IntellisenseObject classObject)
    {
        var output = new StringBuilder();
        foreach (var property in classObject.Properties.Where(x => x.Type.IsKnownType == true))
        {
            if (property.Name == PRIMARYKEY) continue;
            switch (property.Type.CodeName.ToLower())
            {
                case "string" when property.Name.Equals("Name", StringComparison.OrdinalIgnoreCase):
                    output.Append($"<MudItem xs=\"12\" md=\"6\"> \r\n");
                    output.Append("                ");
                    output.Append($"        <MudTextField Label=\"@L[model.GetMemberDescription(x=>x.{property.Name})]\" @bind-Value=\"model.{property.Name}\" For=\"@(() => model.{property.Name})\" Required=\"true\" RequiredError=\"@L[\"{SplitCamelCase(property.Name).ToLower()} is required!\"]\"></MudTextField>\r\n");
                    output.Append("                ");
                    output.Append($"</MudItem> \r\n");
                    break;
                case "string" when property.Name.Equals("Description", StringComparison.OrdinalIgnoreCase):
                    output.Append($"<MudItem xs=\"12\" md=\"6\"> \r\n");
                    output.Append("                ");
                    output.Append($"        <MudTextField Label=\"@L[model.GetMemberDescription(x=>x.{property.Name})]\" Lines=\"3\" For=\"@(() => model.{property.Name})\" @bind-Value=\"model.{property.Name}\"></MudTextField>\r\n");
                    output.Append("                ");
                    output.Append($"</MudItem> \r\n");
                    break;
                case "bool?":
                case "bool":
                    output.Append($"<MudItem xs=\"12\" md=\"6\"> \r\n");
                    output.Append("                ");
                    output.Append($"        <MudCheckBox Label=\"@L[model.GetMemberDescription(x=>x.{property.Name})]\" @bind-Checked=\"model.{property.Name}\" For=\"@(() => model.{property.Name})\" ></MudCheckBox>\r\n");
                    output.Append("                ");
                    output.Append($"</MudItem> \r\n");
                    break;
                case "int?":
                case "int":
                    output.Append($"<MudItem xs=\"12\" md=\"6\"> \r\n");
                    output.Append("                ");
                    output.Append($"        <MudNumericField  Label=\"@L[model.GetMemberDescription(x=>x.{property.Name})]\" @bind-Value=\"model.{property.Name}\" For=\"@(() => model.{property.Name})\" Min=\"0\" Required=\"false\" RequiredError=\"@L[\"{SplitCamelCase(property.Name).ToLower()} is required!\"]\"></MudNumericField >\r\n");
                    output.Append("                ");
                    output.Append($"</MudItem> \r\n");
                    break;
                case "decimal?":
                case "decimal":
                    output.Append($"<MudItem xs=\"12\" md=\"6\"> \r\n");
                    output.Append("                ");
                    output.Append($"        <MudNumericField  Label=\"@L[model.GetMemberDescription(x=>x.{property.Name})]\" @bind-Value=\"model.{property.Name}\" For=\"@(() => model.{property.Name})\" Min=\"0.00m\" Required=\"false\" RequiredError=\"@L[\"{SplitCamelCase(property.Name).ToLower()} is required!\"]\"></MudNumericField >\r\n");
                    output.Append("                ");
                    output.Append($"</MudItem> \r\n");
                    break;
                case "double?":
                case "double":
                    output.Append($"<MudItem xs=\"12\" md=\"6\"> \r\n");
                    output.Append("                ");
                    output.Append($"        <MudNumericField  Label=\"@L[model.GetMemberDescription(x=>x.{property.Name})]\" @bind-Value=\"model.{property.Name}\" For=\"@(() => model.{property.Name})\" Min=\"0.00\" Required=\"false\" RequiredError=\"@L[\"{SplitCamelCase(property.Name).ToLower()} is required!\"]\"></MudNumericField >\r\n");
                    output.Append("                ");
                    output.Append($"</MudItem> \r\n");
                    break;
                case "system.datetime?":
                    output.Append($"<MudItem xs=\"12\" md=\"6\"> \r\n");
                    output.Append("                ");
                    output.Append($"        <MudDatePicker Label=\"@L[model.GetMemberDescription(x=>x.{property.Name})]\" @bind-Date=\"model.{property.Name}\" For=\"@(() => model.{property.Name})\" Required=\"false\" RequiredError=\"@L[\"{SplitCamelCase(property.Name).ToLower()} is required!\"]\"></MudDatePicker>\r\n");
                    output.Append("                ");
                    output.Append($"</MudItem> \r\n");
                    break;
                default:
                    output.Append($"<MudItem xs=\"12\" md=\"6\"> \r\n");
                    output.Append("                ");
                    output.Append($"        <MudTextField Label=\"@L[model.GetMemberDescription(x=>x.{property.Name})]\" @bind-Value=\"model.{property.Name}\" For=\"@(() => model.{property.Name})\" Required=\"false\" RequiredError=\"@L[\"{SplitCamelCase(property.Name).ToLower()} is required!\"]\"></MudTextField>\r\n");
                    output.Append("                ");
                    output.Append($"</MudItem> \r\n");
                    break;

            }

        }
        return output.ToString();
    }

    #endregion

    private static string CreateFieldAssignmentDefinition(IntellisenseObject classObject)
    {
        var output = new StringBuilder();
        foreach (var property in classObject.Properties.Where(x => x.Type.IsKnownType == true))
        {
            output.Append($"        ");
            output.Append($"        {property.Name} = dto.{property.Name}, \r\n");
        }
        return output.ToString();
    }
}
