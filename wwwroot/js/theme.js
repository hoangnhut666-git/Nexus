(function () {
    const COOKIE = "nexus-theme";
    const MAX_AGE = 60 * 60 * 24 * 365;

    function readCookie() {
        const match = document.cookie.match(/(?:^|; )nexus-theme=(light|dark)/);
        return match ? match[1] : null;
    }

    function currentTheme() {
        const stored = readCookie();
        if (stored) {
            return stored;
        }

        return window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark";
    }

    function setCookie(theme) {
        const secure = location.protocol === "https:" ? "; Secure" : "";
        document.cookie = `${COOKIE}=${theme}; path=/; max-age=${MAX_AGE}; SameSite=Lax${secure}`;
    }

    function syncToggles(theme) {
        const next = theme === "dark" ? "light" : "dark";
        const label = next === "light" ? "Switch to light mode" : "Switch to dark mode";

        document.querySelectorAll("[data-theme-toggle]").forEach((button) => {
            button.setAttribute("title", label);
            button.setAttribute("aria-label", label);

            const icon = button.querySelector("i");
            if (icon) {
                icon.classList.remove("fa-moon", "fa-sun");
                icon.classList.add(theme === "dark" ? "fa-sun" : "fa-moon");
            }
        });
    }

    function apply(theme) {
        const root = document.documentElement;
        root.classList.remove("light", "dark");
        root.classList.add(theme);
        root.style.colorScheme = theme;
        syncToggles(theme);
    }

    function restore() {
        apply(currentTheme());
    }

    document.addEventListener("click", (event) => {
        const button = event.target.closest("[data-theme-toggle]");
        if (!button) {
            return;
        }

        const next = currentTheme() === "dark" ? "light" : "dark";
        setCookie(next);
        apply(next);
    });

    restore();

    window.matchMedia("(prefers-color-scheme: light)").addEventListener("change", (event) => {
        if (readCookie()) {
            return;
        }

        apply(event.matches ? "light" : "dark");
    });

    document.addEventListener("enhancedload", restore);

    if (window.Blazor && typeof Blazor.addEventListener === "function") {
        Blazor.addEventListener("enhancedload", restore);
    } else {
        document.addEventListener("DOMContentLoaded", () => {
            if (window.Blazor && typeof Blazor.addEventListener === "function") {
                Blazor.addEventListener("enhancedload", restore);
            }
        });
    }

    new MutationObserver(() => {
        const expected = currentTheme();
        if (!document.documentElement.classList.contains(expected)) {
            apply(expected);
        }
    }).observe(document.documentElement, { attributes: true, attributeFilter: ["class"] });
})();
