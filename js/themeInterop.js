window.kairosTheme = {
    applyTheme: function (theme) {
        var selectedTheme = theme === "dark" ? "dark" : "light";
        document.documentElement.setAttribute("data-theme", selectedTheme);
    }
};
