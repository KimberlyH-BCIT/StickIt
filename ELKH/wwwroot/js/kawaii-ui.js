document.addEventListener("DOMContentLoaded", () => {
    initTabs();
    initToggleChips();
    initAddToCartFeedback();
    initSearchClear();
});

function initTabs() {
    const tabGroups = document.querySelectorAll("[data-tab-group]");

    tabGroups.forEach(group => {
        const tabs = group.querySelectorAll("[data-tab]");
        const panels = group.querySelectorAll("[data-tab-panel]");

        tabs.forEach(tab => {
            tab.addEventListener("click", () => {
                const target = tab.getAttribute("data-tab");

                tabs.forEach(t => {
                    t.classList.remove("active");
                    t.setAttribute("aria-selected", "false");
                });

                panels.forEach(panel => {
                    panel.hidden = panel.getAttribute("data-tab-panel") !== target;
                });

                tab.classList.add("active");
                tab.setAttribute("aria-selected", "true");
            });
        });
    });
}

function initToggleChips() {
    const chips = document.querySelectorAll("[data-chip-toggle]");

    chips.forEach(chip => {
        chip.addEventListener("click", () => {
            chip.classList.toggle("active");

            const pressed = chip.classList.contains("active");
            chip.setAttribute("aria-pressed", pressed.toString());
        });
    });
}

function initAddToCartFeedback() {
    const buttons = document.querySelectorAll("[data-add-to-cart]");

    buttons.forEach(button => {
        button.addEventListener("click", () => {
            const originalText = button.dataset.originalText || button.textContent;

            button.dataset.originalText = originalText;
            button.textContent = "Added!";
            button.classList.add("is-added");

            setTimeout(() => {
                button.textContent = originalText;
                button.classList.remove("is-added");
            }, 1400);
        });
    });
}

function initSearchClear() {
    const searchWrappers = document.querySelectorAll("[data-search-box]");

    searchWrappers.forEach(wrapper => {
        const input = wrapper.querySelector("input");
        const clearButton = wrapper.querySelector("[data-search-clear]");

        if (!input || !clearButton) return;

        clearButton.addEventListener("click", () => {
            input.value = "";
            input.focus();
        });

        input.addEventListener("input", () => {
            clearButton.hidden = input.value.trim() === "";
        });

        clearButton.hidden = input.value.trim() === "";
    });
}