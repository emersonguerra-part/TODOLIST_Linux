using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TodoListApp.Services
{
    public class ThemeService
    {
        public bool IsDarkMode { get; private set; } = false;

        public event Action<bool>? ThemeChanged;

        public void ApplyTheme(bool isDark)
        {
            IsDarkMode = isDark;
            if (Application.Current == null) return;

            var dict = Application.Current.Resources;

            // Cores de Superfície e Fundo
            SetBrush(dict, "WindowBackgroundBrush", isDark ? "#222222" : "#F8FAFC");
            SetBrush(dict, "SurfaceBackgroundBrush", isDark ? "#1E293B" : "#FFFFFF");
            SetBrush(dict, "SubSurfaceBrush", isDark ? "#334155" : "#F1F5F9");
            SetBrush(dict, "BorderDefaultBrush", isDark ? "#334155" : "#E2E8F0");
            SetBrush(dict, "BorderHoverBrush", isDark ? "#475569" : "#CBD5E1");

            // Barra de Título Customizada
            SetBrush(dict, "TitleBarBackgroundBrush", isDark ? "#3B3B3B" : "#F1F5F9");
            SetBrush(dict, "TitleBarForegroundBrush", isDark ? "#808080" : "#64748B");
            SetBrush(dict, "TitleBarButtonHoverBrush", isDark ? "#4A4A4A" : "#E2E8F0");
            SetBrush(dict, "TitleBarCloseHoverBrush", "#EF4444");

            // Tipografia
            SetBrush(dict, "TextTitleBrush", isDark ? "#FFFFFF" : "#0F172A");
            SetBrush(dict, "TextPrimaryBrush", isDark ? "#F8FAFC" : "#1E293B");
            SetBrush(dict, "TextSecondaryBrush", isDark ? "#CBD5E1" : "#475569");
            SetBrush(dict, "TextSubtitleBrush", isDark ? "#94A3B8" : "#64748B");
            SetBrush(dict, "TextMutedBrush", isDark ? "#64748B" : "#94A3B8");

            // Identidade Primária (Índigo)
            SetBrush(dict, "PrimaryBrush", isDark ? "#6366F1" : "#4F46E5");
            SetBrush(dict, "PrimaryHoverBrush", isDark ? "#4F46E5" : "#4338CA");
            SetBrush(dict, "PrimaryLightBrush", isDark ? "#1E1B4B" : "#EEF2FF");
            SetBrush(dict, "PrimaryHoverLightBrush", isDark ? "#312E81" : "#E0E7FF");

            // Barra de Rolagem
            SetBrush(dict, "ScrollbarTrackBrush", isDark ? "#1E293B" : "Transparent");
            SetBrush(dict, "ScrollbarTrackThumbBrush", isDark ? "#475569" : "#A5B4FC");
            SetBrush(dict, "ScrollbarThumbHoverBrush", isDark ? "#818CF8" : "#818CF8");
            SetBrush(dict, "ScrollbarThumbDragBrush", isDark ? "#A5B4FC" : "#6366F1");

            // Estados Semânticos
            SetBrush(dict, "SuccessBrush", isDark ? "#34D399" : "#10B981");
            SetBrush(dict, "DangerBrush", isDark ? "#F87171" : "#EF4444");
            SetBrush(dict, "DangerLightBrush", isDark ? "#450A0A" : "#FEE2E2");

            // Modais e Popups
            SetBrush(dict, "ModalBackdropBrush", isDark ? "#B3000000" : "#660F172A");
            SetBrush(dict, "ModalSurfaceBrush", isDark ? "#1E293B" : "#FFFFFF");
            SetBrush(dict, "DividerBrush", isDark ? "#334155" : "#F1F5F9");

            ThemeChanged?.Invoke(isDark);
        }

        private static void SetBrush(IResourceDictionary dict, string key, string hexOrName)
        {
            Color color;
            if (string.Equals(hexOrName, "Transparent", StringComparison.OrdinalIgnoreCase))
            {
                color = Colors.Transparent;
            }
            else
            {
                color = Color.Parse(hexOrName);
            }

            var brush = new SolidColorBrush(color);
            dict[key] = brush;
        }
    }
}
