using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Kinetic_A11y.Web2.Web.Components.Forms.DualListBox;

public partial class DualListBox<TItem>
{
    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Parameter]
    public IEnumerable<TItem> Items { get; set; } = [];

    [Parameter]
    public IEnumerable<TItem> SelectedItems { get; set; } = [];

    [Parameter]
    public EventCallback<IEnumerable<TItem>> SelectedItemsChanged { get; set; }

    [Parameter, EditorRequired]
    public Func<TItem, string> ItemText { get; set; } = default!;

    [Parameter]
    public string AvailableLabel { get; set; } = "Available";

    [Parameter]
    public string SelectedLabel { get; set; } = "Selected";

    [Parameter]
    public string SearchPlaceholder { get; set; } = "Search...";

    [Parameter]
    public string EmptyAvailableMessage { get; set; } =
        "No available items.";

    [Parameter]
    public string EmptySelectedMessage { get; set; } =
        "No selected items.";

    [Parameter]
    public string AddSelectedLabel { get; set; } =
        "Add selected items";

    [Parameter]
    public string RemoveSelectedLabel { get; set; } =
        "Remove selected items";
    
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; }


    private readonly string AvailableHeadingId =
        $"dual-list-available-heading-{Guid.NewGuid():N}";

    private readonly string SelectedHeadingId =
        $"dual-list-selected-heading-{Guid.NewGuid():N}";

    private readonly string AvailableSearchId =
        $"dual-list-available-search-{Guid.NewGuid():N}";

    private readonly string SelectedSearchId =
        $"dual-list-selected-search-{Guid.NewGuid():N}";

    private readonly string AvailableListId =
        $"dual-list-available-{Guid.NewGuid():N}";

    private readonly string AvailableSelectAllId =
        $"dual-list-available-select-all-{Guid.NewGuid():N}";

    private readonly string SelectedListId =
        $"dual-list-selected-{Guid.NewGuid():N}";

    private readonly string SelectedSelectAllId =
        $"dual-list-selected-select-all-{Guid.NewGuid():N}";


    private string availableSearch = string.Empty;
    private string selectedSearch = string.Empty;


    private readonly HashSet<TItem> availableSelection = [];
    private readonly HashSet<TItem> selectedSelection = [];


    private int availableFocusIndex;
    private int selectedFocusIndex;

    private int availableAnchorIndex = -1;
    private int selectedAnchorIndex = -1;


    private List<TItem> AvailableItems =>
        Items
            .Except(SelectedItems)
            .ToList();

    private List<TItem> SelectedItemList =>
        SelectedItems.ToList();


    private List<TItem> FilteredAvailableItems =>
        FilterItems(
            AvailableItems,
            availableSearch);

    private List<TItem> FilteredSelectedItems =>
        FilterItems(
            SelectedItemList,
            selectedSearch);

    private bool IsAllAvailableSelected =>
        FilteredAvailableItems.Count > 0 &&
        FilteredAvailableItems.All(availableSelection.Contains);

    private bool IsAllSelectedSelected =>
        FilteredSelectedItems.Count > 0 &&
        FilteredSelectedItems.All(selectedSelection.Contains);


    protected override void OnParametersSet()
    {
        var selected = SelectedItems.ToHashSet();

        availableSelection.RemoveWhere(
            item => !AvailableItems.Contains(item));

        selectedSelection.RemoveWhere(
            item => !selected.Contains(item));

        NormalizeFocus();
    }


    protected override async Task OnAfterRenderAsync(
        bool firstRender)
    {
        if (firstRender && FilteredAvailableItems.Count > 0)
        {
            availableFocusIndex = 0;

            await FocusAvailableOption();
        }
    }


    private List<TItem> FilterItems(
        IEnumerable<TItem> items,
        string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return items.ToList();
        }

        return items
            .Where(item =>
                ItemText(item).Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
    }


    private async Task ClickAvailableItem(int index)
    {
        var items = FilteredAvailableItems;

        if (index < 0 || index >= items.Count)
        {
            return;
        }

        availableFocusIndex = index;
        availableAnchorIndex = index;

        ToggleSelection(
            availableSelection,
            items[index]);

        await FocusAvailableOption();
    }


    private async Task ClickSelectedItem(int index)
    {
        var items = FilteredSelectedItems;

        if (index < 0 || index >= items.Count)
        {
            return;
        }

        selectedFocusIndex = index;
        selectedAnchorIndex = index;

        ToggleSelection(
            selectedSelection,
            items[index]);

        await FocusSelectedOption();
    }


    private async Task HandleAvailableKeyDown(
        KeyboardEventArgs e)
    {
        var items = FilteredAvailableItems;

        if (items.Count == 0)
        {
            return;
        }

        var index = NormalizeIndex(
            availableFocusIndex,
            items.Count);

        var ctrlOrMeta = e.CtrlKey || e.MetaKey;

        switch (e.Key)
        {
            case "ArrowDown":
                await MoveAvailableFocus(
                    index + 1,
                    e.ShiftKey);
                break;

            case "ArrowUp":
                await MoveAvailableFocus(
                    index - 1,
                    e.ShiftKey);
                break;

            case "Home":
                await MoveAvailableFocus(
                    0,
                    e.ShiftKey);
                break;

            case "End":
                await MoveAvailableFocus(
                    items.Count - 1,
                    e.ShiftKey);
                break;

            case " ":
            case "Spacebar":
                ToggleSelection(
                    availableSelection,
                    items[index]);

                availableAnchorIndex = index;

                await InvokeAsync(StateHasChanged);
                break;

            case "Enter":
                await MoveSelectedToRight();
                break;

            case "a" when ctrlOrMeta:
                SelectAllAvailable();
                break;
        }
    }


    private async Task HandleSelectedKeyDown(
        KeyboardEventArgs e)
    {
        var items = FilteredSelectedItems;

        if (items.Count == 0)
        {
            return;
        }

        var index = NormalizeIndex(
            selectedFocusIndex,
            items.Count);

        var ctrlOrMeta = e.CtrlKey || e.MetaKey;

        switch (e.Key)
        {
            case "ArrowDown":
                await MoveSelectedFocus(
                    index + 1,
                    e.ShiftKey);
                break;

            case "ArrowUp":
                await MoveSelectedFocus(
                    index - 1,
                    e.ShiftKey);
                break;

            case "Home":
                await MoveSelectedFocus(
                    0,
                    e.ShiftKey);
                break;

            case "End":
                await MoveSelectedFocus(
                    items.Count - 1,
                    e.ShiftKey);
                break;

            case " ":
            case "Spacebar":
                ToggleSelection(
                    selectedSelection,
                    items[index]);

                selectedAnchorIndex = index;

                await InvokeAsync(StateHasChanged);
                break;

            case "Enter":
                await MoveSelectedToLeft();
                break;

            case "a" when ctrlOrMeta:
                SelectAllSelected();
                break;
        }
    }


    private async Task MoveAvailableFocus(
        int newIndex,
        bool extendSelection)
    {
        var items = FilteredAvailableItems;

        newIndex = Math.Clamp(
            newIndex,
            0,
            items.Count - 1);

        if (extendSelection)
        {
            if (availableAnchorIndex == -1)
            {
                availableAnchorIndex = availableFocusIndex;
            }

            SelectRange(
                availableSelection,
                items,
                availableAnchorIndex,
                newIndex);
        }

        availableFocusIndex = newIndex;

        await InvokeAsync(StateHasChanged);
        await FocusAvailableOption();
    }


    private async Task MoveSelectedFocus(
        int newIndex,
        bool extendSelection)
    {
        var items = FilteredSelectedItems;

        newIndex = Math.Clamp(
            newIndex,
            0,
            items.Count - 1);

        if (extendSelection)
        {
            if (selectedAnchorIndex == -1)
            {
                selectedAnchorIndex = selectedFocusIndex;
            }

            SelectRange(
                selectedSelection,
                items,
                selectedAnchorIndex,
                newIndex);
        }

        selectedFocusIndex = newIndex;

        await InvokeAsync(StateHasChanged);
        await FocusSelectedOption();
    }


    private static void SelectRange(
        HashSet<TItem> selection,
        List<TItem> items,
        int start,
        int end)
    {
        var first = Math.Min(start, end);
        var last = Math.Max(start, end);

        for (var i = first; i <= last; i++)
        {
            selection.Add(items[i]);
        }
    }


    private static  void ToggleSelection(
        HashSet<TItem> selection,
        TItem item)
    {
        if (!selection.Add(item))
        {
            selection.Remove(item);
        }
    }


    private void SelectAllAvailable()
    {
        foreach (var item in FilteredAvailableItems)
        {
            availableSelection.Add(item);
        }
    }

    private void ToggleAllAvailableSelection(ChangeEventArgs _)
    {
        if (IsAllAvailableSelected)
        {
            foreach (var item in FilteredAvailableItems)
            {
                availableSelection.Remove(item);
            }

            return;
        }

        SelectAllAvailable();
    }

    private void SelectAllSelected()
    {
        foreach (var item in FilteredSelectedItems)
        {
            selectedSelection.Add(item);
        }
    }

    private void ToggleAllSelectedSelection(ChangeEventArgs _)
    {
        if (IsAllSelectedSelected)
        {
            foreach (var item in FilteredSelectedItems)
            {
                selectedSelection.Remove(item);
            }

            return;
        }

        SelectAllSelected();
    }


    private async Task MoveSelectedToRight()
    {
        if (availableSelection.Count == 0)
        {
            return;
        }

        var current = SelectedItems.ToList();

        foreach (var item in AvailableItems)
        {
            if (availableSelection.Contains(item))
            {
                current.Add(item);
            }
        }

        availableSelection.Clear();

        await SelectedItemsChanged.InvokeAsync(current);

        availableFocusIndex = Math.Min(
            availableFocusIndex,
            Math.Max(0, FilteredAvailableItems.Count - 1));

        availableAnchorIndex = -1;

        await InvokeAsync(StateHasChanged);
    }


    private async Task MoveSelectedToLeft()
    {
        if (selectedSelection.Count == 0)
        {
            return;
        }

        var current = SelectedItems
            .Where(item => !selectedSelection.Contains(item))
            .ToList();

        selectedSelection.Clear();

        await SelectedItemsChanged.InvokeAsync(current);

        selectedFocusIndex = Math.Min(
            selectedFocusIndex,
            Math.Max(0, FilteredSelectedItems.Count - 1));

        selectedAnchorIndex = -1;

        await InvokeAsync(StateHasChanged);
    }


    private async Task MoveAvailableItem(int index)
    {
        var items = FilteredAvailableItems;

        if (index < 0 || index >= items.Count)
        {
            return;
        }

        availableSelection.Clear();
        availableSelection.Add(items[index]);

        await MoveSelectedToRight();
    }


    private async Task MoveSelectedItem(int index)
    {
        var items = FilteredSelectedItems;

        if (index < 0 || index >= items.Count)
        {
            return;
        }

        selectedSelection.Clear();
        selectedSelection.Add(items[index]);

        await MoveSelectedToLeft();
    }


    private async Task FocusAvailableOption()
    {
        var itemCount = FilteredAvailableItems.Count;

        if (itemCount == 0)
        {
            return;
        }

        var index = Math.Clamp(
            availableFocusIndex,
            0,
            itemCount - 1);

        await FocusOptionById(GetAvailableOptionId(index));
    }


    private async Task FocusSelectedOption()
    {
        var itemCount = FilteredSelectedItems.Count;

        if (itemCount == 0)
        {
            return;
        }

        var index = Math.Clamp(
            selectedFocusIndex,
            0,
            itemCount - 1);

        await FocusOptionById(GetSelectedOptionId(index));
    }


    private ValueTask FocusOptionById(string id) =>
        JS.InvokeVoidAsync("eval", $"document.getElementById('{id}')?.focus()");


    private void NormalizeFocus()
    {
        availableFocusIndex = Math.Clamp(
            availableFocusIndex,
            0,
            Math.Max(0, FilteredAvailableItems.Count - 1));

        selectedFocusIndex = Math.Clamp(
            selectedFocusIndex,
            0,
            Math.Max(0, FilteredSelectedItems.Count - 1));
    }


    private static int NormalizeIndex(
        int index,
        int count)
    {
        if (count == 0)
        {
            return -1;
        }

        return Math.Clamp(
            index,
            0,
            count - 1);
    }


    private static string GetOptionClass(
        bool selected,
        bool focused)
    {
        var classes =
            "px-3 py-2 select-none cursor-pointer text-sm text-slate-700 " +
            "focus:outline-none ";

        if (selected)
        {
            classes += "bg-ocio-blue-50 text-ocio-blue-900 ";
        }

        if (focused)
        {
            classes += "ring-2 ring-inset ring-ocio-blue-500 ";
        }

        return classes;
    }


    private string GetAvailableOptionId(int index) =>
        $"{AvailableListId}-option-{index}";


    private string GetSelectedOptionId(int index) =>
        $"{SelectedListId}-option-{index}";


    private static string GetSelectionStatus(
        int selectedCount,
        int itemCount)
    {
        if (itemCount == 0)
        {
            return "No items.";
        }

        if (selectedCount == 0)
        {
            return $"{itemCount} items. None selected.";
        }

        return $"{selectedCount} of {itemCount} items selected.";
    }
}
