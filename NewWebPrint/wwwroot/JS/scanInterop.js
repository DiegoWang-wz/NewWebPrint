// 扫码页 JS 互操作（FA17、致优等）
window.blazorScrollScanTableToBottom = function () {
    try {
        const elements = document.querySelectorAll(".scan-table-wrap");
        elements.forEach(el => {
            el.scrollTop = el.scrollHeight;
            requestAnimationFrame(() => {
                el.scrollTop = el.scrollHeight + 1000;
            });
        });
    } catch (e) {
        console.error("blazorScrollScanTableToBottom error:", e);
    }
};

window.blazorClearScanInputs = function () {
    try {
        const wrappers = document.querySelectorAll(".scan-input-field");
        let focusTarget = null;
        wrappers.forEach(w => {
            const input = w.querySelector("input, textarea");
            if (!input) return;
            input.value = "";
            input.dispatchEvent(new Event("input", { bubbles: true }));
            input.dispatchEvent(new Event("change", { bubbles: true }));
            if (!focusTarget) focusTarget = input;
        });
        if (focusTarget) focusTarget.focus({ preventScroll: true });
    } catch (e) {
        console.error("blazorClearScanInputs error:", e);
    }
};
