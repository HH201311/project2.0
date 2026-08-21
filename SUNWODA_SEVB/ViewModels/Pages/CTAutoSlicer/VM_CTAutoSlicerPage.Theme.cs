using Microsoft.WindowsAPICodePack.Dialogs;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Services.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.ViewModels.Pages.CTAutoSlicer
{
    /// <summary>
    /// CT 自动化切片页面 ViewModel（partial：Theme）。
    /// </summary>
    public partial class VM_CTAutoSlicerPage
    {
        private void ExecuteSelectThemeColorSlot(object? parameter)
        {
            var index = parameter switch
            {
                int i => i,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => -1
            };
            if (index is < 0 or > 3)
            {
                return;
            }

            ThemeEditSlot = index;
        }

        private void SetThemeColor(ref string field, string value, string propName, string brushPropName)
        {
            var normalized = NormalizeHexColor(value);
            if (string.Equals(field, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            field = normalized;
            OnPropertyChanged(propName);
            OnPropertyChanged(brushPropName);
            if (!_themeEditSyncing && GetHexForSlot(ThemeEditSlot) == normalized)
            {
                OnPropertyChanged(nameof(ThemeEditHex));
                OnPropertyChanged(nameof(ThemeEditRgbText));
                OnPropertyChanged(nameof(ThemeEditPreviewBrush));
            }

            RefreshThemeDirtyState();
            if (!_themeEditSyncing)
            {
                ThemeColorsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void LoadThemeEditFromSlot()
        {
            if (!TryParseHexColor(GetHexForSlot(ThemeEditSlot), out var color))
            {
                return;
            }

            _themeEditSyncing = true;
            try
            {
                _themeEditR = color.R;
                _themeEditG = color.G;
                _themeEditB = color.B;
            }
            finally
            {
                _themeEditSyncing = false;
            }

            OnPropertyChanged(nameof(ThemeEditR));
            OnPropertyChanged(nameof(ThemeEditG));
            OnPropertyChanged(nameof(ThemeEditB));
            OnPropertyChanged(nameof(ThemeEditHex));
            OnPropertyChanged(nameof(ThemeEditRgbText));
            OnPropertyChanged(nameof(ThemeEditPreviewBrush));
            OnPropertyChanged(nameof(ThemeEditColor));
        }

        private void CommitThemeEditRgb()
        {
            if (_themeEditSyncing)
            {
                return;
            }

            var hex = ThemeEditHex;
            _themeEditSyncing = true;
            try
            {
                switch (ThemeEditSlot)
                {
                    case 0:
                        ThemePageBgColor = hex;
                        break;
                    case 1:
                        ThemeOuterCardColor = hex;
                        break;
                    case 2:
                        ThemeInnerCardColor = hex;
                        break;
                    case 3:
                        ThemeSidebarColor = hex;
                        break;
                }
            }
            finally
            {
                _themeEditSyncing = false;
            }

            OnPropertyChanged(nameof(ThemeEditHex));
            OnPropertyChanged(nameof(ThemeEditRgbText));
            OnPropertyChanged(nameof(ThemeEditPreviewBrush));
            OnPropertyChanged(nameof(ThemeEditColor));
            ThemeColorsChanged?.Invoke(this, EventArgs.Empty);
        }

        private string GetHexForSlot(int slot) => slot switch
        {
            1 => ThemeOuterCardColor,
            2 => ThemeInnerCardColor,
            3 => ThemeSidebarColor,
            _ => ThemePageBgColor,
        };

        private void ExecuteApplyThemePreset(object? parameter)
        {
            var id = parameter as string;
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var preset = ThemeColorPresets.FirstOrDefault(p =>
                string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (preset == null)
            {
                return;
            }

            ApplyThemePresetPreview(new CTAutoSlicerSettings.ThemeColorPreset(
                preset.Id,
                preset.Name,
                preset.Description,
                preset.PageBg,
                preset.OuterCard,
                preset.InnerCard,
                preset.Sidebar,
                preset.IsBuiltIn));
            MessageShower = $"已预览「{preset.Name}」，请点「保存主题」后下次启动生效";
        }

        private void ApplyThemePresetPreview(CTAutoSlicerSettings.ThemeColorPreset preset)
        {
            _themeEditSyncing = true;
            try
            {
                ThemePageBgColor = preset.PageBg;
                ThemeOuterCardColor = preset.OuterCard;
                ThemeInnerCardColor = preset.InnerCard;
                ThemeSidebarColor = preset.Sidebar;
            }
            finally
            {
                _themeEditSyncing = false;
            }

            LoadThemeEditFromSlot();
            RefreshThemeDirtyState();
            ThemeColorsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SaveThemeColors()
        {
            _savedThemePageBgColor = ThemePageBgColor;
            _savedThemeOuterCardColor = ThemeOuterCardColor;
            _savedThemeInnerCardColor = ThemeInnerCardColor;
            _savedThemeSidebarColor = ThemeSidebarColor;

            SaveSetting(nameof(ThemePageBgColor), _savedThemePageBgColor);
            SaveSetting(nameof(ThemeOuterCardColor), _savedThemeOuterCardColor);
            SaveSetting(nameof(ThemeInnerCardColor), _savedThemeInnerCardColor);
            SaveSetting(nameof(ThemeSidebarColor), _savedThemeSidebarColor);

            RefreshThemeDirtyState();
            MessageShower = "主题色已保存，下次启动将沿用当前配色";
        }

        private void RevertThemeColorsToSaved(bool showMessage)
        {
            _themeEditSyncing = true;
            try
            {
                ThemePageBgColor = _savedThemePageBgColor;
                ThemeOuterCardColor = _savedThemeOuterCardColor;
                ThemeInnerCardColor = _savedThemeInnerCardColor;
                ThemeSidebarColor = _savedThemeSidebarColor;
            }
            finally
            {
                _themeEditSyncing = false;
            }

            LoadThemeEditFromSlot();
            RefreshThemeDirtyState();
            ThemeColorsChanged?.Invoke(this, EventArgs.Empty);
            if (showMessage)
            {
                MessageShower = "已还原为上次保存的主题色";
            }
        }

        private void ResetThemeColorsToDefault()
        {
            var preset = CTAutoSlicerSettings.ThemeColorPresets[0];
            ApplyThemePresetPreview(preset);
            MessageShower = $"已预览「{preset.Name}」，请点「保存主题」后下次启动生效";
        }

        private void SaveCurrentColorsAsPreset()
        {
            var name = (NewThemePresetName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageShower = "请先填写新预设名称";
                return;
            }

            if (ThemeColorPresets.Any(p =>
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageShower = "预设名称已存在，请换一个名称";
                return;
            }

            var id = "custom-" + Guid.NewGuid().ToString("N")[..8];
            var preset = new CTAutoSlicerSettings.ThemeColorPreset(
                id,
                name,
                "自定义预设",
                ThemePageBgColor,
                ThemeOuterCardColor,
                ThemeInnerCardColor,
                ThemeSidebarColor,
                isBuiltIn: false);
            ThemeColorPresets.Add(new ThemePresetItem(preset));
            PersistCustomThemePresets();
            NewThemePresetName = string.Empty;
            MessageShower = $"已保存自定义预设「{name}」";
        }

        private void ExecuteDeleteThemePreset(object? parameter)
        {
            var id = parameter as string;
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var item = ThemeColorPresets.FirstOrDefault(p =>
                string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                return;
            }

            if (item.IsBuiltIn)
            {
                MessageShower = "内置预设不可删除";
                return;
            }

            ThemeColorPresets.Remove(item);
            PersistCustomThemePresets();
            MessageShower = $"已删除自定义预设「{item.Name}」";
        }

        private void RebuildThemePresetList(IEnumerable<CTAutoSlicerSettings.ThemeCustomPresetDto>? customs = null)
        {
            ThemeColorPresets.Clear();
            foreach (var builtIn in CTAutoSlicerSettings.ThemeColorPresets)
            {
                ThemeColorPresets.Add(new ThemePresetItem(builtIn));
            }

            if (customs == null)
            {
                return;
            }

            foreach (var dto in customs)
            {
                if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Name))
                {
                    continue;
                }

                if (CTAutoSlicerSettings.ThemeColorPresets.Any(p =>
                        string.Equals(p.Id, dto.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                ThemeColorPresets.Add(new ThemePresetItem(new CTAutoSlicerSettings.ThemeColorPreset(
                    dto.Id,
                    dto.Name,
                    string.IsNullOrWhiteSpace(dto.Description) ? "自定义预设" : dto.Description,
                    NormalizeHexColor(dto.PageBg),
                    NormalizeHexColor(dto.OuterCard),
                    NormalizeHexColor(dto.InnerCard),
                    NormalizeHexColor(dto.Sidebar),
                    isBuiltIn: false)));
            }
        }

        private void PersistCustomThemePresets()
        {
            var customs = ThemeColorPresets
                .Where(p => !p.IsBuiltIn)
                .Select(p => new CTAutoSlicerSettings.ThemeCustomPresetDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    PageBg = p.PageBg,
                    OuterCard = p.OuterCard,
                    InnerCard = p.InnerCard,
                    Sidebar = p.Sidebar,
                })
                .ToList();
            var json = JsonSerializer.Serialize(customs);
            SaveSetting("ThemeCustomPresetsJson", json);
        }

        private void LoadCustomThemePresets()
        {
            var json = LoadStringSetting("ThemeCustomPresetsJson", "[]");
            List<CTAutoSlicerSettings.ThemeCustomPresetDto>? customs = null;
            try
            {
                customs = JsonSerializer.Deserialize<List<CTAutoSlicerSettings.ThemeCustomPresetDto>>(json);
            }
            catch
            {
                customs = null;
            }

            RebuildThemePresetList(customs);
        }

        private void RefreshThemeDirtyState()
        {
            HasUnsavedThemeChanges =
                !HexEquals(_themePageBgColor, _savedThemePageBgColor)
                || !HexEquals(_themeOuterCardColor, _savedThemeOuterCardColor)
                || !HexEquals(_themeInnerCardColor, _savedThemeInnerCardColor)
                || !HexEquals(_themeSidebarColor, _savedThemeSidebarColor);
        }

        private static bool HexEquals(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static Brush CreateBrush(string hex)
        {
            var brush = new SolidColorBrush(TryParseHexColor(hex, out var c) ? c : Colors.White);
            brush.Freeze();
            return brush;
        }

        private static string NormalizeHexColor(string? value)
        {
            if (TryParseHexColor(value, out var c))
            {
                return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }

            return CTAutoSlicerSettings.DefaultThemePageBgColor;
        }

        private static bool TryParseHexColor(string? value, out Color color)
        {
            color = Colors.White;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var s = value.Trim();
            if (!s.StartsWith('#'))
            {
                s = "#" + s;
            }

            try
            {
                var converted = ColorConverter.ConvertFromString(s);
                if (converted is Color c)
                {
                    color = Color.FromRgb(c.R, c.G, c.B);
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }
    }
}
