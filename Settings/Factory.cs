using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyOpenVR.Settings;

public enum EControl
{
    Checkbox,
    Label,
    Radio,
    Scale,
    Select,
    Slider,
    Toggle
}

public enum EType
{
    None,
    Int,
    Float,
    Bool
}

public class Factory
{
    private readonly Control _control = new();
    private readonly object? _defaultValue;

    public Factory(string name, EControl control, EType type, object? defaultValue)
    {
        _control.name = name;
        _control.type = type == EType.None ? null : Enum.GetName(typeof(EType), type)?.ToLower();
        _control.control = Enum.GetName(typeof(EControl), control)?.ToLower();
        _defaultValue = defaultValue;
    }
    
    public Control GetControl()
    {
        return _control;
    }

    public object? GetDefaultValue()
    {
        return _defaultValue;
    }

    public Factory Label(string labelStr)
    {
        _control.label = labelStr;
        return this;
    }

    public Factory Text(string textStr)
    {
        _control.text = textStr;
        return this;
    }

    public Factory Group(string groupStr)
    {
        _control.group = groupStr;
        return this;
    }

    public Factory Options(params Option[] optionsArr)
    {
        _control.options = optionsArr;
        return this;
    }

    public Factory RequiresRestart()
    {
        _control.requires_restart = true;
        return this;
    }

    public Factory AdvancedOnly()
    {
        _control.advanced_only = true;
        return this;
    }

    public Factory WindowsOnly()
    {
        _control.windows_only = true;
        return this;
    }

    public Factory OnLabel(string labelStr)
    {
        _control.on_label = labelStr;
        return this;
    }

    public Factory OffLabel(string labelStr)
    {
        _control.off_label = labelStr;
        return this;
    }
}