using System.Globalization;
using System.Text;

namespace Condor.Cli.Tui;

/// <summary>Elemento comun a las listas interactivas de la TUI.</summary>
public record SelectItem(
    string Id,
    string Label,
    string Description,
    IReadOnlyList<string> Tags,
    bool Disabled,
    string? DisabledReason,
    string? Shortcut)
{
    public bool IsCurrent { get; init; }
}

/// <summary>Compatibilidad temporal mientras TuiInput migra al nuevo modelo.</summary>
public sealed record SelectListItem : SelectItem
{
    public SelectListItem(
        string Id,
        string Label,
        string Description = "",
        IReadOnlyList<string>? Tags = null,
        bool Disabled = false,
        string? DisabledReason = null,
        string? Shortcut = null,
        bool IsCurrent = false)
        : base(Id, Label, Description, Tags ?? Array.Empty<string>(), Disabled, DisabledReason, Shortcut)
    {
        this.IsCurrent = IsCurrent;
    }
}

/// <summary>Estado inmutable de filtro, seleccion y desplazamiento de una lista.</summary>
public sealed record SelectListState(
    IReadOnlyList<SelectItem> Items,
    string Query,
    IReadOnlyList<int> Filtered,
    int SelectedIndex,
    int ScrollOffset,
    int VisibleRows)
{
    public SelectItem? Selected =>
        SelectedIndex >= 0 && SelectedIndex < Filtered.Count
            ? Items[Filtered[SelectedIndex]]
            : null;

    public SelectItem? SelectedItem => Selected;

    public SelectListState SetQuery(string query) => SelectList.SetQuery(this, query);

    public SelectListState MoveNext() => SelectList.Move(this, 1);

    public SelectListState MovePrev() => SelectList.Move(this, -1);
}

public enum SelectListPlacement
{
    AboveInput,
    Centered
}

/// <summary>Tokens de color usados por el renderer; se pueden sustituir por tema.</summary>
public sealed record SelectListTheme(
    string Accent,
    string Text,
    string Muted,
    string Disabled,
    string PopupBackground,
    string SelectionBackground,
    string SelectionForeground)
{
    public static SelectListTheme Default { get; } = Ansi.ColorEnabled
        ? new SelectListTheme(
            Ansi.FgDorado,
            Ansi.FgBlanco,
            Ansi.FgGris,
            Ansi.FgGris,
            Ansi.Esc + "48;5;235m",
            Ansi.Esc + "48;5;179m",
            Ansi.Esc + "38;5;16m")
        : new SelectListTheme("", "", "", "", "", "", "");
}

public sealed record SelectListRenderOptions(
    string? Title = null,
    bool IncludeSearchField = false,
    bool TwoColumns = false,
    int? LabelColumnWidth = null,
    bool ShowShortcuts = false,
    bool ShowScrollbar = true);

/// <summary>Lineas ya formateadas para que TuiHost las posicione y pinte por diff.</summary>
public sealed record SelectListOverlay(
    IReadOnlyList<string> Lines,
    SelectListPlacement Placement,
    int Width,
    int Column);

/// <summary>Operaciones puras para filtrar y navegar listas reutilizables.</summary>
public static class SelectList
{
    public const int DefaultVisibleRows = 8;
    public const int MaximumVisibleRows = 10;

    public static SelectListState Create(
        IEnumerable<SelectItem> items,
        string query = "",
        int visibleRows = DefaultVisibleRows,
        string? selectedId = null)
    {
        var materialized = items?.ToArray() ?? Array.Empty<SelectItem>();
        var filtered = Filter(materialized, query);
        var rows = Math.Clamp(visibleRows, 1, MaximumVisibleRows);
        var selected = FindInitialSelection(materialized, filtered, selectedId);
        return EnsureVisible(new SelectListState(materialized, query ?? "", filtered, selected, 0, rows));
    }

    public static SelectListState SetQuery(SelectListState state, string query)
    {
        ArgumentNullException.ThrowIfNull(state);
        var filtered = Filter(state.Items, query);
        var previousId = state.Selected?.Id;
        var selected = FindInitialSelection(state.Items, filtered, previousId);
        return EnsureVisible(state with
        {
            Query = query ?? "",
            Filtered = filtered,
            SelectedIndex = selected,
            ScrollOffset = 0
        });
    }

    public static SelectListState Move(SelectListState state, int delta)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (delta == 0 || state.Filtered.Count == 0)
        {
            return state;
        }

        var start = state.SelectedIndex;
        if (start < 0 || start >= state.Filtered.Count)
        {
            start = delta > 0 ? -1 : 0;
        }

        for (var step = 1; step <= state.Filtered.Count; step++)
        {
            var candidate = Mod(start + delta * step, state.Filtered.Count);
            if (!state.Items[state.Filtered[candidate]].Disabled)
            {
                return EnsureVisible(state with { SelectedIndex = candidate });
            }
        }

        return state with { SelectedIndex = -1 };
    }

    public static SelectListState MoveTo(SelectListState state, int index)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (index < 0 || index >= state.Filtered.Count || state.Items[state.Filtered[index]].Disabled)
        {
            return state;
        }

        return EnsureVisible(state with { SelectedIndex = index });
    }

    public static IReadOnlyList<int> Filter(IReadOnlyList<SelectItem> items, string? query)
    {
        var normalizedQuery = (query ?? "").Trim();
        if (normalizedQuery.StartsWith('/'))
        {
            normalizedQuery = normalizedQuery[1..];
        }

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Enumerable.Range(0, items.Count).ToArray();
        }

        return items
            .Select((item, index) => new
            {
                Index = index,
                Score = FuzzyMatcher.Score(normalizedQuery, SearchText(item))
            })
            .Where(result => result.Score is not null)
            .OrderBy(result => result.Score)
            .ThenBy(result => result.Index)
            .Select(result => result.Index)
            .ToArray();
    }

    private static SelectListState EnsureVisible(SelectListState state)
    {
        if (state.Filtered.Count == 0 || state.SelectedIndex < 0)
        {
            return state with { ScrollOffset = 0 };
        }

        var maxOffset = Math.Max(0, state.Filtered.Count - state.VisibleRows);
        var offset = Math.Clamp(state.ScrollOffset, 0, maxOffset);
        if (state.SelectedIndex < offset)
        {
            offset = state.SelectedIndex;
        }
        else if (state.SelectedIndex >= offset + state.VisibleRows)
        {
            offset = state.SelectedIndex - state.VisibleRows + 1;
        }

        return state with { ScrollOffset = Math.Clamp(offset, 0, maxOffset) };
    }

    private static int FindInitialSelection(
        IReadOnlyList<SelectItem> items,
        IReadOnlyList<int> filtered,
        string? selectedId)
    {
        if (selectedId is not null)
        {
            for (var index = 0; index < filtered.Count; index++)
            {
                var item = items[filtered[index]];
                if (!item.Disabled && string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
        }

        for (var index = 0; index < filtered.Count; index++)
        {
            if (!items[filtered[index]].Disabled && items[filtered[index]].IsCurrent)
            {
                return index;
            }
        }

        for (var index = 0; index < filtered.Count; index++)
        {
            if (!items[filtered[index]].Disabled)
            {
                return index;
            }
        }

        return -1;
    }

    private static string SearchText(SelectItem item) => string.Join(' ', new[]
        {
            item.Label,
            item.Description,
            string.Join(' ', item.Tags)
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static int Mod(int value, int length) => (value % length + length) % length;
}

/// <summary>Coincidencia difusa pura con puntajes mas bajos para coincidencias mejores.</summary>
public static class FuzzyMatcher
{
    public static int? Score(string query, string text)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(text);

        var queryRunes = Normalize(query).Where(rune => !Rune.IsWhiteSpace(rune)).ToArray();
        if (queryRunes.Length == 0)
        {
            return 0;
        }

        var textRunes = Normalize(text);
        var score = 0;
        var cursor = 0;
        var previous = -2;

        foreach (var queryRune in queryRunes)
        {
            var position = -1;
            for (var index = cursor; index < textRunes.Length; index++)
            {
                if (textRunes[index] == queryRune)
                {
                    position = index;
                    break;
                }
            }

            if (position < 0)
            {
                return null;
            }

            if (position == 0)
            {
                score -= 20;
            }
            else if (!Rune.IsLetterOrDigit(textRunes[position - 1]))
            {
                score -= 4;
            }

            if (position == previous + 1)
            {
                score -= 6;
            }
            else
            {
                score += position - cursor;
            }

            previous = position;
            cursor = position + 1;
        }

        return score;
    }

    private static Rune[] Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        return decomposed.EnumerateRunes()
            .Where(rune => Rune.GetUnicodeCategory(rune) is not
                (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark))
            .Select(Rune.ToLowerInvariant)
            .ToArray();
    }
}

/// <summary>Render puro de filas SelectList sin coordenadas ni efectos de consola.</summary>
public static class SelectListRenderer
{
    public static IReadOnlyList<string> Render(SelectListState state, int width) =>
        Render(state, width, new SelectListRenderOptions(), SelectListTheme.Default);

    public static IReadOnlyList<string> Render(
        SelectListState state,
        int width,
        SelectListRenderOptions options,
        SelectListTheme theme)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(theme);
        width = Math.Max(1, width);

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.Title))
        {
            lines.Add(RenderSimple(options.Title, width, theme.Accent, theme));
        }

        if (options.IncludeSearchField)
        {
            lines.Add(RenderSimple("Buscar: " + state.Query + "▏", width, theme.Text, theme));
        }

        if (state.Filtered.Count == 0)
        {
            lines.Add(RenderSimple("Sin resultados", width, theme.Muted, theme));
            return lines;
        }

        var visible = Math.Min(state.VisibleRows, state.Filtered.Count);
        var hasScrollbar = options.ShowScrollbar && state.Filtered.Count > visible;
        var rowWidth = Math.Max(1, width - (hasScrollbar ? 1 : 0));
        var shortcutWidth = options.ShowShortcuts
            ? Math.Min(12, state.Filtered
                .Select(index => Ansi.VisibleWidth(state.Items[index].Shortcut ?? ""))
                .DefaultIfEmpty(0)
                .Max())
            : 0;
        var contentWidth = Math.Max(1, rowWidth - (shortcutWidth > 0 ? shortcutWidth + 2 : 0));
        var maxLabel = state.Filtered
            .Select(index => Ansi.VisibleWidth(state.Items[index].Label))
            .DefaultIfEmpty(0)
            .Max();
        var labelWidth = options.TwoColumns
            ? Math.Clamp(options.LabelColumnWidth ?? maxLabel + 2, 1, Math.Max(1, contentWidth - 1))
            : 0;

        var thumbHeight = hasScrollbar
            ? Math.Max(1, (visible * visible) / state.Filtered.Count)
            : 0;
        var thumbStart = hasScrollbar
            ? state.ScrollOffset * Math.Max(0, visible - thumbHeight) /
              Math.Max(1, state.Filtered.Count - visible)
            : 0;

        for (var row = 0; row < visible; row++)
        {
            var filteredIndex = state.ScrollOffset + row;
            if (filteredIndex >= state.Filtered.Count)
            {
                break;
            }

            var item = state.Items[state.Filtered[filteredIndex]];
            var selected = filteredIndex == state.SelectedIndex && !item.Disabled;
            var shortcut = options.ShowShortcuts ? item.Shortcut ?? "" : "";
            var description = BuildDescription(item);
            var textWidth = contentWidth;
            var label = item.Label;
            var detail = description;
            if (options.TwoColumns)
            {
                label = PadOrClip(item.Label, labelWidth);
                detail = ClipWithEllipsis(description, Math.Max(0, contentWidth - labelWidth - 2));
                if (detail.Length > 0) detail = "  " + detail;
                textWidth = labelWidth + Math.Max(0, contentWidth - labelWidth);
            }
            else
            {
                var combined = string.IsNullOrEmpty(description) ? item.Label : item.Label + " · " + description;
                label = ClipWithEllipsis(combined, contentWidth);
                detail = "";
            }

            if (shortcutWidth > 0)
            {
                textWidth = contentWidth + 2 + shortcutWidth;
                shortcut = PadLeftOrClip(shortcut, shortcutWidth);
            }

            var line = RenderItemRow(
                label,
                detail,
                shortcut,
                textWidth,
                item,
                selected,
                theme);

            if (hasScrollbar)
            {
                var isThumb = row >= thumbStart && row < thumbStart + thumbHeight;
                var barStyle = selected
                    ? theme.SelectionBackground + theme.SelectionForeground
                    : isThumb ? theme.Accent : theme.Muted;
                line += barStyle + (isThumb ? "█" : "│") + Ansi.Reset;
            }

            lines.Add(line);
        }

        return lines;
    }

    private static string RenderSimple(string text, int width, string foreground, SelectListTheme theme)
    {
        var visible = ClipWithEllipsis(text, width);
        var padding = new string(' ', Math.Max(0, width - Ansi.VisibleWidth(visible)));
        return theme.PopupBackground + foreground + visible + padding + Ansi.Reset;
    }

    private static string RenderItemRow(
        string label,
        string detail,
        string shortcut,
        int width,
        SelectItem item,
        bool selected,
        SelectListTheme theme)
    {
        var labelWidth = Ansi.VisibleWidth(label);
        var detailWidth = Ansi.VisibleWidth(detail);
        var shortcutWidth = Ansi.VisibleWidth(shortcut);
        var shortcutGap = shortcutWidth > 0 ? 2 : 0;
        var remaining = Math.Max(0, width - labelWidth - detailWidth - shortcutWidth - shortcutGap);
        var background = selected ? theme.SelectionBackground : theme.PopupBackground;
        var foreground = selected ? theme.SelectionForeground : item.Disabled ? theme.Disabled : theme.Text;
        var detailColor = selected ? theme.SelectionForeground : item.Disabled ? theme.Disabled : theme.Muted;

        var sb = new StringBuilder(width + 64);
        sb.Append(background);
        if (item.Disabled && !selected && Ansi.ColorEnabled)
        {
            sb.Append(Ansi.Dim);
        }

        sb.Append(foreground);
        if (item.IsCurrent && !item.Disabled)
        {
            sb.Append(Ansi.Bold);
        }

        sb.Append(label);
        if (detailWidth > 0)
        {
            sb.Append(detailColor).Append(detail);
        }

        if (shortcutWidth > 0)
        {
            sb.Append(new string(' ', remaining + shortcutGap));
            sb.Append(detailColor).Append(shortcut);
        }
        else
        {
            sb.Append(new string(' ', remaining));
        }

        sb.Append(Ansi.Reset);
        return sb.ToString();
    }

    private static string BuildDescription(SelectItem item)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Description)) parts.Add(item.Description.Trim());
        if (item.Tags is not null) parts.AddRange(item.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()));
        if (!string.IsNullOrWhiteSpace(item.DisabledReason)) parts.Add(item.DisabledReason.Trim());
        return string.Join(" · ", parts);
    }

    private static string PadOrClip(string value, int width)
    {
        var clipped = ClipWithEllipsis(value, width);
        return clipped + new string(' ', Math.Max(0, width - Ansi.VisibleWidth(clipped)));
    }

    private static string PadLeftOrClip(string value, int width)
    {
        var clipped = ClipWithEllipsis(value, width);
        return new string(' ', Math.Max(0, width - Ansi.VisibleWidth(clipped))) + clipped;
    }

    private static string ClipWithEllipsis(string value, int width)
    {
        width = Math.Max(0, width);
        if (Ansi.VisibleWidth(value) <= width)
        {
            return value;
        }

        if (width <= 3)
        {
            return Ansi.ClipVisible(value, width);
        }

        return Ansi.ClipVisible(value, width - 3) + "...";
    }
}
