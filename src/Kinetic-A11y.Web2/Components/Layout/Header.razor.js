let headerRoot = null;
let documentClickHandler = null;
let rootClickHandler = null;

function closeAllDropdowns() {
    if (headerRoot === null) {
        return;
    }

    headerRoot.querySelectorAll('details[open][name="header-dropdowns"]').forEach((details) => {
        details.open = false;
    });
}

export function initialize(element) {
    if (headerRoot !== null) {
        return;
    }

    headerRoot = element;

    documentClickHandler = (event) => {
        if (!(event.target instanceof Element)) {
            return;
        }

        if (!headerRoot.contains(event.target)) {
            closeAllDropdowns();
        }
    };

    rootClickHandler = (event) => {
        if (!(event.target instanceof Element)) {
            return;
        }

        if (event.target.closest('a') !== null) {
            closeAllDropdowns();
        }
    };

    document.addEventListener('click', documentClickHandler, true);
    headerRoot.addEventListener('click', rootClickHandler, true);
}

export function dispose() {
    if (headerRoot === null) {
        return;
    }

    if (documentClickHandler !== null) {
        document.removeEventListener('click', documentClickHandler, true);
        documentClickHandler = null;
    }

    if (rootClickHandler !== null) {
        headerRoot.removeEventListener('click', rootClickHandler, true);
        rootClickHandler = null;
    }

    headerRoot = null;
}
