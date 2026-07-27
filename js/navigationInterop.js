let dotNetHelper = null;
let keydownListener = null;

window.navigationInterop = {
    initialize: function (helper) {
        dotNetHelper = helper;

        keydownListener = function (e) {
            // Prevent default browser scrolling when using PageDown/PageUp for navigation
            if (e.key === 'PageDown') {
                e.preventDefault();
                dotNetHelper.invokeMethodAsync('NavigateNext');
            } else if (e.key === 'PageUp') {
                e.preventDefault();
                dotNetHelper.invokeMethodAsync('NavigatePrevious');
            }
        };

        window.addEventListener('keydown', keydownListener);
    },
    dispose: function () {
        if (keydownListener) {
            window.removeEventListener('keydown', keydownListener);
            keydownListener = null;
        }
        dotNetHelper = null;
    }
};
