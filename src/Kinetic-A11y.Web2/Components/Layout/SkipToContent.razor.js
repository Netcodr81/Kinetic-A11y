const skipLinkSelector = ".skip-link[href^='#']";

function getTargetId(skipLink) {
    const href = skipLink.getAttribute("href");
    if (!href || href.length <= 1) {
        return null;
    }

    return href.slice(1);
}

function getTargetElement(skipLink) {
    const targetId = getTargetId(skipLink);
    if (!targetId) {
        return null;
    }

    const anchorTarget = document.getElementById(targetId);
    if (!anchorTarget) {
        return null;
    }

    const mainContent = anchorTarget.closest("main") ?? document.getElementById("main-content");
    const firstHeading = mainContent?.querySelector("h1");

    return firstHeading ?? mainContent ?? anchorTarget;
}

function focusElement(element) {
    const hasTabIndex = element.hasAttribute("tabindex");

    if (!hasTabIndex) {
        element.setAttribute("tabindex", "-1");
        element.addEventListener(
            "blur",
            () => {
                element.removeAttribute("tabindex");
            },
            { once: true }
        );
    }

    element.focus({ preventScroll: true });
}

document.addEventListener("click", event => {
    const skipLink = event.target instanceof Element
        ? event.target.closest(skipLinkSelector)
        : null;

    if (!skipLink) {
        return;
    }

    const targetId = getTargetId(skipLink);
    if (!targetId) {
        return;
    }

    const anchorTarget = document.getElementById(targetId);
    if (!anchorTarget) {
        return;
    }

    event.preventDefault();
    history.pushState(null, "", `#${targetId}`);
    anchorTarget.scrollIntoView();

    requestAnimationFrame(() => {
        const targetElement = getTargetElement(skipLink);
        if (!targetElement) {
            return;
        }

        focusElement(targetElement);
    });
});
