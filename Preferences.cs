using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Prism;

public partial class MainWindow
{
    internal const string WindowsStartupArgument = "--windows-startup";
    private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "Prism Crosshair";

    internal bool AllowsMotion => !_settings.ReduceMotion && SystemParameters.ClientAreaAnimation;

    private void ConfigurePreferences()
    {
        var heading = new TextBlock
        {
            Text = "Запуск и интерфейс",
            Margin = new Thickness(0, 0, 0, 9)
        };
        heading.SetResourceReference(StyleProperty, "SectionHeading");
        PreferencesHost.Children.Add(heading);

        AddWindowsStartupControl();
        AddPreferenceToggle("Запускать свёрнутым в трей", _settings.StartInTray,
            enabled => _settings.StartInTray = enabled);
        AddPreferenceToggle("Профили в меню трея", _settings.ShowTrayProfiles,
            enabled => _settings.ShowTrayProfiles = enabled);
        AddPreferenceToggle("Уменьшить анимацию", _settings.ReduceMotion,
            enabled => _settings.ReduceMotion = enabled);

        var modes = new[]
        {
            new Choice<OverlayStartupMode>(OverlayStartupMode.Restore, "Как при выходе"),
            new Choice<OverlayStartupMode>(OverlayStartupMode.Disabled, "Скрыть прицел"),
            new Choice<OverlayStartupMode>(OverlayStartupMode.Enabled, "Показать прицел")
        };
        var startupMode = new ComboBox
        {
            ItemsSource = modes,
            SelectedItem = modes.First(mode => mode.Value == _settings.OverlayAtStartup)
        };
        startupMode.SelectionChanged += (_, _) =>
        {
            if (startupMode.SelectedItem is Choice<OverlayStartupMode> choice)
            {
                _settings.OverlayAtStartup = choice.Value;
                ScheduleSave();
            }
        };
        var startupModeLabel = new TextBlock
        {
            Text = "Оверлей при запуске",
            Margin = new Thickness(0, 12, 0, 7),
            FontWeight = FontWeights.SemiBold
        };
        AutomationProperties.SetLabeledBy(startupMode, startupModeLabel);
        PreferencesHost.Children.Add(startupModeLabel);
        PreferencesHost.Children.Add(startupMode);
    }

    private void AddPreferenceToggle(string label, bool currentValue, Action<bool> update)
    {
        var toggle = new CheckBox { Content = label, IsChecked = currentValue };
        void Changed(object sender, RoutedEventArgs e)
        {
            update(toggle.IsChecked == true);
            ScheduleSave();
        }

        toggle.Checked += Changed;
        toggle.Unchecked += Changed;
        PreferencesHost.Children.Add(toggle);
    }

    private void AddWindowsStartupControl()
    {
        var platformLaunch = new CheckBox { Content = "Запускать вместе с Windows" };
        var feedback = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        feedback.SetResourceReference(StyleProperty, "Caption");
        AutomationProperties.SetLiveSetting(feedback, AutomationLiveSetting.Polite);
        PreferencesHost.Children.Add(platformLaunch);
        PreferencesHost.Children.Add(feedback);

        var executable = Environment.ProcessPath;
        if (executable is null || Path.GetFileName(executable).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            platformLaunch.IsEnabled = false;
            feedback.Text = "Откройте Prism.exe, чтобы включить автозапуск.";
            return;
        }

        string command = $"\"{executable}\" {WindowsStartupArgument}";
        if (command.Length > 260)
        {
            platformLaunch.IsEnabled = false;
            feedback.Text = "Для автозапуска переместите Prism в папку с более коротким путём.";
            return;
        }

        bool registered;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKey);
            var existing = key?.GetValue(StartupValueName) as string;
            registered = string.Equals(existing, command, StringComparison.OrdinalIgnoreCase);
            platformLaunch.IsChecked = registered;
            if (existing is not null && !registered)
                feedback.Text = "В автозапуске указана другая копия Prism. Включение заменит её текущей.";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            platformLaunch.IsEnabled = false;
            feedback.Text = "Windows не предоставила доступ к параметрам автозапуска.";
            return;
        }

        var restoring = false;
        void SetStartup(object sender, RoutedEventArgs e)
        {
            if (restoring)
                return;

            var enable = platformLaunch.IsChecked == true;
            try
            {
                if (enable)
                {
                    using var key = Registry.CurrentUser.CreateSubKey(StartupKey);
                    key.SetValue(StartupValueName, command, RegistryValueKind.String);
                }
                else
                {
                    using var key = Registry.CurrentUser.OpenSubKey(StartupKey, writable: true);
                    if (string.Equals(key?.GetValue(StartupValueName) as string, command, StringComparison.OrdinalIgnoreCase))
                        key?.DeleteValue(StartupValueName, throwOnMissingValue: false);
                }

                registered = enable;
                feedback.Text = enable ? "Prism откроется при следующем входе в Windows." : "";
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
            {
                restoring = true;
                platformLaunch.IsChecked = registered;
                restoring = false;
                feedback.Text = "Не удалось изменить автозапуск в Windows.";
            }
        }

        platformLaunch.Checked += SetStartup;
        platformLaunch.Unchecked += SetStartup;
    }

    private void ConfigureTrayProfiles(Forms.ContextMenuStrip trayMenu)
    {
        var profiles = new Forms.ToolStripMenuItem("Профили");
        trayMenu.Items.Add(profiles);
        trayMenu.Opening += (_, _) =>
        {
            profiles.Visible = _settings.ShowTrayProfiles;
            if (!_settings.ShowTrayProfiles)
                return;

            while (profiles.DropDownItems.Count > 0)
            {
                var obsolete = profiles.DropDownItems[0];
                profiles.DropDownItems.RemoveAt(0);
                obsolete.Dispose();
            }

            var options = BuiltIns.Concat(_settings.Presets).ToList();
            var matching = options.Where(preset => preset.Reticle == _settings.Reticle);
            var selected = matching.FirstOrDefault(preset => preset.Name == _activePresetName) ?? matching.FirstOrDefault();
            foreach (var preset in options)
            {
                var name = preset.Name.Replace("&", "&&").Replace('\r', ' ').Replace('\n', ' ');
                var item = new Forms.ToolStripMenuItem(name)
                {
                    Checked = ReferenceEquals(preset, selected)
                };
                item.Click += (_, _) => ApplyPreset(preset, navigateToEditor: false);
                profiles.DropDownItems.Add(item);
            }
        };
    }
}
