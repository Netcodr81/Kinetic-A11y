function normalizeAnchorId(targetId) {
    if (!targetId) {
        return "top";
    }

    return targetId.startsWith("#") ? targetId.substring(1) : targetId;
}

function focusTarget(targetElement) {
    const hadTabIndex = targetElement.hasAttribute("tabindex");

    if (!hadTabIndex) {
        targetElement.setAttribute("tabindex", "-1");
        targetElement.addEventListener(
            "blur",
            () => {
                targetElement.removeAttribute("tabindex");
            },
            { once: true }
        );
    }

    try {
        targetElement.focus({ preventScroll: true });
    } catch {
        targetElement.focus();
    }
}

function animateScroll(startPosition, targetPosition, duration, onComplete) {
    const distance = targetPosition - startPosition;
    const startTime = performance.now();

    function easeInOutCubic(t) {
        return t < 0.5
            ? 4 * t * t * t
            : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;
    }

    function animation(currentTime) {
        const timeElapsed = currentTime - startTime;
        const progress = Math.min(timeElapsed / duration, 1);
        const eased = easeInOutCubic(progress);

        window.scrollTo(0, startPosition + (distance * eased));

        if (timeElapsed < duration) {
            requestAnimationFrame(animation);
            return;
        }

        onComplete();
    }

    requestAnimationFrame(animation);
}

export function scrollToAnchor(targetId) {
    const normalizedTargetId = normalizeAnchorId(targetId);
    const targetElement = document.getElementById(normalizedTargetId);

    if (!targetElement) {
        return;
    }

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const startPosition = window.pageYOffset;
    const targetPosition = targetElement.getBoundingClientRect().top + window.pageYOffset;
    const complete = () => {
        history.pushState(null, "", `#${normalizedTargetId}`);
        focusTarget(targetElement);
    };

    if (reducedMotion) {
        window.scrollTo(0, targetPosition);
        complete();
        return;
    }

    animateScroll(startPosition, targetPosition, 1200, complete);
}
