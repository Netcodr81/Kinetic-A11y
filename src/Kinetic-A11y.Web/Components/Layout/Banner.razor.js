let documentClickHandler = null;

export function register(dotNetRef) {
    if (documentClickHandler !== null) {
        return;
    }

    documentClickHandler = (event) => {
        const button = document.getElementById("user-menu-button");
        const menu = document.getElementById("user-menu");

        const clickedInside =
            (button !== null && button.contains(event.target)) ||
            (menu !== null && menu.contains(event.target));

        if (!clickedInside) {
            dotNetRef.invokeMethodAsync("CloseMenu");
        }
    };

    document.addEventListener("click", documentClickHandler);
}

export function dispose() {
    if (documentClickHandler === null) {
        return;
    }

    document.removeEventListener("click", documentClickHandler);
    documentClickHandler = null;
}
