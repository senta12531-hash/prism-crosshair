using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Prism;

public enum ReticleShape { Cross, TShape, Dot, Ring, Diagonal, Chevron, Brackets, Cat }
public enum PlacementMode { ActiveMonitor, FixedMonitor }
public enum OverlayStartupMode { Restore, Enabled, Disabled }

[Flags]
public enum ReticleArms
{
    None = 0,
    Right = 1,
    Bottom = 2,
    Left = 4,
    Top = 8,
    All = Right | Bottom | Left | Top
}

public sealed record Reticle
{
    private const string DefaultColor = "#FAFCFF";
    public ReticleShape Shape { get; init; } = ReticleShape.Cross;
    public string Color { get; init; } = DefaultColor;
    public int Length { get; init; } = 9;
    public int? VerticalLength { get; init; }
    public int Rotation { get; init; }
    public bool RoundedEnds { get; init; }
    public bool SquareDot { get; init; }
    public string OutlineColor { get; init; } = "#000000";
    public int OutlineOpacity { get; init; } = 100;
    public int Gap { get; init; } = 5;
    public int? VerticalGap { get; init; }
    public ReticleArms VisibleArms { get; init; } = ReticleArms.All;
    public int Thickness { get; init; } = 2;
    public bool Outline { get; init; } = true;
    public int OutlineWidth { get; init; } = 1;
    public bool CenterDot { get; init; } = true;
    public int DotSize { get; init; } = 2;
    public string? DotColor { get; init; }
    public int DotOpacity { get; init; } = 100;
    public int Opacity { get; init; } = 100;
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }

    public Reticle Normalize() => this with
    {
        Shape = Enum.IsDefined(Shape) ? Shape : ReticleShape.Cross,
        Color = NormalizeColor(Color) ?? DefaultColor,
        Length = Math.Clamp(Length, 1, 36),
        VerticalLength = VerticalLength is int length ? Math.Clamp(length, 1, 36) : null,
        Rotation = Math.Clamp(Rotation, -180, 180),
        OutlineColor = NormalizeColor(OutlineColor) ?? "#000000",
        OutlineOpacity = Math.Clamp(OutlineOpacity, 0, 100),
        Gap = Math.Clamp(Gap, 0, 24),
        VerticalGap = VerticalGap is int gap ? Math.Clamp(gap, 0, 24) : null,
        VisibleArms = VisibleArms & ReticleArms.All,
        Thickness = Math.Clamp(Thickness, 1, 10),
        OutlineWidth = Math.Clamp(OutlineWidth, 1, 4),
        DotSize = Math.Clamp(DotSize, 1, 14),
        DotColor = NormalizeColor(DotColor),
        DotOpacity = Math.Clamp(DotOpacity, 10, 100),
        Opacity = Math.Clamp(Opacity, 10, 100),
        OffsetX = Math.Clamp(OffsetX, -100, 100),
        OffsetY = Math.Clamp(OffsetY, -100, 100)
    };

    public static string? NormalizeColor(string? value)
    {
        var hex = value?.Trim().TrimStart('#');
        return hex?.Length == 6 && hex.All(Uri.IsHexDigit)
            ? "#" + hex.ToUpperInvariant()
            : null;
    }
}

public sealed record Preset(string Name, string Description, Reticle Reticle);

public sealed class Settings
{
    public Reticle Reticle { get; set; } = new();
    public List<Preset> Presets { get; set; } = new();
    public List<string> FavoriteProfileNames { get; set; } = new();
    public bool OverlayEnabled { get; set; }
    public PlacementMode Placement { get; set; }
    public string? MonitorDevice { get; set; }
    public int HotkeyModifiers { get; set; } = 6;
    public int HotkeyKey { get; set; } = 0x77;
    public bool MinimizeToTray { get; set; } = true;
    public bool CheckUpdates { get; set; } = true;
    public bool StartInTray { get; set; }
    public bool ReduceMotion { get; set; }
    public bool ShowTrayProfiles { get; set; } = true;
    public OverlayStartupMode OverlayAtStartup { get; set; }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Prism Crosshair", "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new();
            settings.Reticle = settings.Reticle?.Normalize() ?? new();
            settings.Presets = (settings.Presets ?? new()).Where(preset =>
                preset is { Reticle: not null } && !string.IsNullOrWhiteSpace(preset.Name))
                .Select(preset => preset with { Reticle = preset.Reticle.Normalize() }).ToList();
            settings.FavoriteProfileNames = (settings.FavoriteProfileNames ?? new())
                .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (!Enum.IsDefined(settings.Placement))
                settings.Placement = PlacementMode.ActiveMonitor;
            if (!Enum.IsDefined(settings.OverlayAtStartup))
                settings.OverlayAtStartup = OverlayStartupMode.Restore;
            if (settings.HotkeyModifiers is not (3 or 5 or 6))
                settings.HotkeyModifiers = 6;
            settings.HotkeyKey = Math.Clamp(settings.HotkeyKey, 0x70, 0x7B);
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Settings();
        }
    }

    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(temporaryPath, SettingsPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
