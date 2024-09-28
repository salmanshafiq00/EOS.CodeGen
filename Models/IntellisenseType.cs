﻿using System.Collections.Generic;
using System.Globalization;

namespace EOS.CodeGen.Models;

public sealed class IntellisenseType
{
    /// <summary>
    /// This is the name of this type as it appears in the source code
    /// </summary>
    public string CodeName { get; set; }

    /// <summary>
    /// Indicates whether this type is array. Is this property is true, then all other properties
    /// describe not the type itself, but rather the type of the array's elements.
    /// </summary>
    public bool IsArray { get; set; }

    public bool IsDictionary { get; set; }

    public bool IsOptional { get { return CodeName.EndsWith("?"); } }

    /// <summary>
    /// If this type is itself part of a source code file that has a .d.ts definitions file attached,
    /// this property will contain the full (namespace-qualified) client-side name of that type.
    /// Otherwise, this property is null.
    /// </summary>
    public string ClientSideReferenceName { get; set; }

    /// <summary>
    /// This is TypeScript-formed shape of the type (i.e. inline type definition). It is used for the case where
    /// the type is not primitive, but does not have its own named client-side definition.
    /// </summary>
    public IEnumerable<IntellisenseProperty> Shape { get; set; }

    public bool IsKnownType
    {
        get { return TypeScriptName != "any"; }
    }
    
    public string TypeScriptName
    {
        get
        {
            if (IsDictionary) return GetKVPTypes();
            return GetTargetName(CodeName, false);
        }
    }

    private string GetTargetName(string codeName, bool js)
    {
        var t = codeName.ToLowerInvariant().TrimEnd('?');
        return t switch
        {
            "int16" or "int32" or "int64" or "short" or "int" or "long" or "float" or "double" or "decimal" or "biginteger" => js ? "Number" : "number",
            "datetime" or "datetimeoffset" or "system.datetime" or "system.datetimeoffset" => "Date",
            "string" => js ? "String" : "string",
            "bool" or "boolean" => js ? "Boolean" : "boolean",
            _ => js ? "Object" : GetComplexTypeScriptName(),
        };
    }

    private string GetComplexTypeScriptName()
    {
        return ClientSideReferenceName ?? "any";
    }

    private string GetKVPTypes()
    {
        var type = CodeName.ToLowerInvariant().TrimEnd('?');
        var types = type.Split('<', '>')[1].Split(',');
        string keyType = GetTargetName(types[0].Trim(), false);

        if (keyType != "string" && keyType != "number")
        { // only string or number are allowed for keys
            keyType = "string";
        }

        string valueType = GetTargetName(types[1].Trim(), false);

        return string.Format(CultureInfo.CurrentCulture, "{{ [index: {0}]: {1} }}", keyType, valueType);
    }
}