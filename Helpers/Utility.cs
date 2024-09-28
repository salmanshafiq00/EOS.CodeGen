using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace EOS.CodeGen.Helpers;

internal static class Utility
{
    public static string CamelCaseClassName(string name)
    {
        return CamelCase(name);
    }

    public static string CamelCaseEnumValue(string name)
    {
        return CamelCase(name);
    }

    public static string CamelCasePropertyName(string name)
    {
        return CamelCase(name);
    }

    public static string ToCamelCase(this string name)
    {
        return CamelCase(name);
    }

    private static string CamelCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }
        return name[0].ToString(CultureInfo.CurrentCulture).ToLower(CultureInfo.CurrentCulture) + name.Substring(1);
    }

    public static string GetCapitalizedHyphenatedName(this string input)
    {
        // Inserts a hyphen between "Abdur" and "Rahman" and capitalizes both parts
        string result = AddHyphenBetweenWords(input);
        return result; // Output will be like: Abdur-Rahman
    }

    public static string GetLowerCaseHyphenatedName(this string input)
    {
        // Inserts a hyphen and makes the entire string lowercase
        string result = AddHyphenBetweenWords(input);
        return result.ToLower(); // Output will be like: abdur-rahman
    }

    // Helper function to add a hyphen between the words
    private static string AddHyphenBetweenWords(string input)
    {
        // Detect the case change and insert hyphen before the uppercase letter
        for (int i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
            {
                return input.Substring(0, i) + "-" + input.Substring(i);
            }
        }
        return input; // If no uppercase letter found, return original input
    }

    public static string GetTemplatesFolderPath()
    {
        // Get the path of the executing assembly (the VSIX project)
        string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        // Combine it with the relative path to the "Templates" folder
        string templatesFolderPath = Path.Combine(assemblyLocation, "Templates");

        return templatesFolderPath;
    }

}
